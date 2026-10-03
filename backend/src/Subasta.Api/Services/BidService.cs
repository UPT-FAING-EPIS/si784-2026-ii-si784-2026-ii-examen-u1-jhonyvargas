using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Subasta.Api.Data;
using Subasta.Api.Domain;
using Subasta.Api.Dtos;
using Subasta.Api.Hubs;

namespace Subasta.Api.Services;

public interface IBidService
{
    Task<BidDto> PlaceBidAsync(Guid bidderId, PlaceBidRequest request, CancellationToken ct);
    Task<IReadOnlyList<BidDto>> GetAuctionBidsAsync(Guid auctionId, CancellationToken ct);
    Task<IReadOnlyList<UserBidDto>> GetUserBidsAsync(Guid userId, CancellationToken ct);
}

/// <summary>Evento enviado por WebSocket al grupo de la subasta cuando se registra una puja.</summary>
public sealed record BidPlacedEvent(Guid AuctionId, BidDto Bid, decimal CurrentPrice, decimal NextMinimumBid, int BidCount);

public sealed class BidService : IBidService
{
    public const string BidPlacedEventName = "BidPlaced";
    private const int MaxConcurrencyRetries = 3;

    private readonly AppDbContext _db;
    private readonly TimeProvider _time;
    private readonly INotificationService _notifications;
    private readonly IHubContext<AuctionHub> _hub;

    public BidService(AppDbContext db, TimeProvider time, INotificationService notifications, IHubContext<AuctionHub> hub)
    {
        _db = db;
        _time = time;
        _notifications = notifications;
        _hub = hub;
    }

    public async Task<BidDto> PlaceBidAsync(Guid bidderId, PlaceBidRequest request, CancellationToken ct)
    {
        var auctionId = request.AuctionId ?? throw new DomainException("La subasta es obligatoria.");
        var bidder = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == bidderId && u.IsActive, ct)
                     ?? throw new UnauthorizedAccessException("Usuario no válido.");

        for (var attempt = 1; ; attempt++)
        {
            var auction = await _db.Auctions.FirstOrDefaultAsync(a => a.Id == auctionId, ct)
                          ?? throw new KeyNotFoundException("Subasta no encontrada.");

            var previousLeader = await _db.Bids.AsNoTracking()
                .Where(b => b.AuctionId == auctionId)
                .OrderByDescending(b => b.Amount).ThenBy(b => b.CreatedAt)
                .Select(b => (Guid?)b.BidderId)
                .FirstOrDefaultAsync(ct);

            var bid = auction.PlaceBid(bidderId, request.Amount, _time.GetUtcNow().UtcDateTime);
            _db.Bids.Add(bid);

            var created = new List<Notification>
            {
                _notifications.Enqueue(auction.SellerId, NotificationType.NewBid,
                    $"{bidder.UserName} ofertó {bid.Amount:0.00} en \"{auction.Title}\".", auction.Id)
            };
            if (previousLeader.HasValue && previousLeader.Value != bidderId)
            {
                created.Add(_notifications.Enqueue(previousLeader.Value, NotificationType.Outbid,
                    $"Su oferta en \"{auction.Title}\" fue superada. Nueva oferta: {bid.Amount:0.00}.", auction.Id));
            }

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                // Otra puja se registró al mismo tiempo: se recarga el estado y se revalida.
                _db.ChangeTracker.Clear();
                continue;
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new DomainException("La subasta recibió otra oferta simultánea. Intente nuevamente.");
            }

            var dto = new BidDto(bid.Id, auction.Id, auction.Title, bidderId, bidder.UserName, bid.Amount, bid.CreatedAt);
            await _hub.Clients.Group(AuctionHub.AuctionGroup(auction.Id)).SendAsync(
                BidPlacedEventName,
                new BidPlacedEvent(auction.Id, dto, auction.CurrentPrice, auction.NextMinimumBid(), auction.BidCount),
                ct);
            await _notifications.PublishAsync(created, ct);
            return dto;
        }
    }

    public async Task<IReadOnlyList<BidDto>> GetAuctionBidsAsync(Guid auctionId, CancellationToken ct)
    {
        if (!await _db.Auctions.AnyAsync(a => a.Id == auctionId, ct))
        {
            throw new KeyNotFoundException("Subasta no encontrada.");
        }

        return await _db.Bids.AsNoTracking()
            .Where(b => b.AuctionId == auctionId)
            .OrderByDescending(b => b.Amount).ThenBy(b => b.CreatedAt)
            .Select(b => new BidDto(b.Id, b.AuctionId, b.Auction!.Title, b.BidderId, b.Bidder!.UserName, b.Amount, b.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UserBidDto>> GetUserBidsAsync(Guid userId, CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var rows = await _db.Bids.AsNoTracking()
            .Where(b => b.BidderId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .Take(500)
            .Select(b => new
            {
                b.Id,
                b.AuctionId,
                b.Auction!.Title,
                b.Amount,
                b.CreatedAt,
                b.Auction.CurrentPrice,
                b.Auction.Status,
                b.Auction.StartAt,
                b.Auction.EndAt,
                b.Auction.WinningBidId,
                IsTop = !b.Auction.Bids.Any(o => o.Amount > b.Amount)
            })
            .ToListAsync(ct);

        return rows.Select(r =>
        {
            var status = new Auction { Status = r.Status, StartAt = r.StartAt, EndAt = r.EndAt }.GetEffectiveStatus(now);
            var isWinning = status == AuctionStatus.Finished ? r.WinningBidId == r.Id : r.IsTop && status == AuctionStatus.Active;
            return new UserBidDto(r.Id, r.AuctionId, r.Title, r.Amount, r.CreatedAt, r.CurrentPrice,
                DtoMappings.StatusName(status), isWinning);
        }).ToList();
    }
}
