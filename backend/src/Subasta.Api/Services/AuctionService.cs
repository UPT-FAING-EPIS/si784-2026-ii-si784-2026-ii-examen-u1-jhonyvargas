using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Subasta.Api.Data;
using Subasta.Api.Domain;
using Subasta.Api.Dtos;
using Subasta.Api.Hubs;

namespace Subasta.Api.Services;

public interface IAuctionService
{
    Task<AuctionDetailDto> CreateAsync(Guid sellerId, CreateAuctionRequest request, CancellationToken ct);
    Task<PagedResult<AuctionSummaryDto>> SearchAsync(AuctionQuery query, CancellationToken ct);
    Task<AuctionDetailDto> GetAsync(Guid id, CancellationToken ct);
    Task<string> AddImageAsync(Guid auctionId, Guid userId, IFormFile file, CancellationToken ct);
    Task<AuctionImage> GetImageAsync(Guid auctionId, Guid imageId, CancellationToken ct);
    Task CancelAsync(Guid auctionId, Guid userId, bool isAdmin, CancellationToken ct);
    Task<IReadOnlyList<AuctionSummaryDto>> GetUserAuctionsAsync(Guid userId, string type, CancellationToken ct);
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct);
}

public sealed class AuctionService : IAuctionService
{
    public const int MaxImagesPerAuction = 5;
    public const long MaxImageBytes = 2 * 1024 * 1024;
    public const string AuctionCancelledEvent = "AuctionCancelled";

    private static readonly Dictionary<string, byte[][]> AllowedImageSignatures = new()
    {
        ["image/jpeg"] = [[0xFF, 0xD8, 0xFF]],
        ["image/png"] = [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]],
        ["image/gif"] = [[0x47, 0x49, 0x46, 0x38]],
        ["image/webp"] = [[0x52, 0x49, 0x46, 0x46]]
    };

    private readonly AppDbContext _db;
    private readonly TimeProvider _time;
    private readonly INotificationService _notifications;
    private readonly IHubContext<AuctionHub> _hub;

    public AuctionService(AppDbContext db, TimeProvider time, INotificationService notifications, IHubContext<AuctionHub> hub)
    {
        _db = db;
        _time = time;
        _notifications = notifications;
        _hub = hub;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public async Task<AuctionDetailDto> CreateAsync(Guid sellerId, CreateAuctionRequest request, CancellationToken ct)
    {
        if (!await _db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            throw new DomainException("La categoría seleccionada no existe.");
        }

        var now = Now;
        var start = request.StartAt.HasValue ? ToUtc(request.StartAt.Value) : now;
        if (start < now)
        {
            start = now;
        }

        var auction = new Auction
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            CategoryId = request.CategoryId,
            StartingPrice = request.StartingPrice,
            MinIncrement = request.MinIncrement,
            CurrentPrice = request.StartingPrice,
            StartAt = start,
            EndAt = ToUtc(request.EndAt ?? throw new DomainException("La fecha de cierre es obligatoria.")),
            SellerId = sellerId,
            CreatedAt = now,
            Status = start <= now ? AuctionStatus.Active : AuctionStatus.Scheduled
        };

        _db.Auctions.Add(auction);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(auction.Id, ct);
    }

    public async Task<PagedResult<AuctionSummaryDto>> SearchAsync(AuctionQuery query, CancellationToken ct)
    {
        var now = Now;
        var q = _db.Auctions.AsNoTracking().AsQueryable();

        q = (query.Status ?? "active") switch
        {
            "active" => q.Where(a => a.Status != AuctionStatus.Cancelled && a.Status != AuctionStatus.Finished
                                     && a.StartAt <= now && a.EndAt > now),
            "upcoming" => q.Where(a => a.Status != AuctionStatus.Cancelled && a.Status != AuctionStatus.Finished
                                       && a.StartAt > now),
            "finished" => q.Where(a => a.Status == AuctionStatus.Finished
                                       || (a.Status != AuctionStatus.Cancelled && a.EndAt <= now)),
            "cancelled" => q.Where(a => a.Status == AuctionStatus.Cancelled),
            _ => q
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(a => a.Title.ToLower().Contains(term) || a.Description.ToLower().Contains(term));
        }

        if (query.CategoryId.HasValue)
        {
            q = q.Where(a => a.CategoryId == query.CategoryId.Value);
        }

        if (query.MinPrice.HasValue)
        {
            q = q.Where(a => a.CurrentPrice >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            q = q.Where(a => a.CurrentPrice <= query.MaxPrice.Value);
        }

        q = query.Sort switch
        {
            "newest" => q.OrderByDescending(a => a.CreatedAt),
            "priceAsc" => q.OrderBy(a => a.CurrentPrice),
            "priceDesc" => q.OrderByDescending(a => a.CurrentPrice),
            _ => q.OrderBy(a => a.EndAt)
        };

        var total = await q.CountAsync(ct);
        var items = await ProjectSummary(q.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize))
            .ToListAsync(ct);

        return new PagedResult<AuctionSummaryDto>(items.Select(i => i.ToDto(now)).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<AuctionDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var a = await _db.Auctions.AsNoTracking()
                    .Include(x => x.Category)
                    .Include(x => x.Seller)
                    .Include(x => x.Winner)
                    .FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new KeyNotFoundException("Subasta no encontrada.");

        var imageIds = await _db.AuctionImages.AsNoTracking()
            .Where(i => i.AuctionId == id)
            .OrderBy(i => i.SortOrder)
            .Select(i => i.Id)
            .ToListAsync(ct);

        return new AuctionDetailDto(
            a.Id, a.Title, a.Description, a.Category!.Name, a.CategoryId,
            a.StartingPrice, a.MinIncrement, a.CurrentPrice, a.NextMinimumBid(), a.BidCount,
            a.StartAt, a.EndAt, a.CreatedAt, a.ClosedAt,
            DtoMappings.StatusName(a.GetEffectiveStatus(Now)),
            a.SellerId, a.Seller!.UserName, a.WinnerId, a.Winner?.UserName,
            imageIds.Select(imageId => ImageUrl(a.Id, imageId)).ToList());
    }

    public async Task<string> AddImageAsync(Guid auctionId, Guid userId, IFormFile file, CancellationToken ct)
    {
        var auction = await _db.Auctions.FirstOrDefaultAsync(a => a.Id == auctionId, ct)
                      ?? throw new KeyNotFoundException("Subasta no encontrada.");
        if (auction.SellerId != userId)
        {
            throw new UnauthorizedAccessException("Solo el vendedor puede agregar imágenes.");
        }

        if (auction.GetEffectiveStatus(Now) is AuctionStatus.Finished or AuctionStatus.Cancelled)
        {
            throw new DomainException("No se pueden agregar imágenes a una subasta cerrada.");
        }

        var count = await _db.AuctionImages.CountAsync(i => i.AuctionId == auctionId, ct);
        if (count >= MaxImagesPerAuction)
        {
            throw new DomainException($"Se permiten como máximo {MaxImagesPerAuction} imágenes por subasta.");
        }

        var data = await ReadAndValidateImageAsync(file, ct);
        var image = new AuctionImage
        {
            AuctionId = auctionId,
            FileName = SanitizeFileName(file.FileName),
            ContentType = file.ContentType.ToLowerInvariant(),
            Data = data,
            SortOrder = count,
            CreatedAt = Now
        };
        _db.AuctionImages.Add(image);
        await _db.SaveChangesAsync(ct);
        return ImageUrl(auctionId, image.Id);
    }

    public async Task<AuctionImage> GetImageAsync(Guid auctionId, Guid imageId, CancellationToken ct) =>
        await _db.AuctionImages.AsNoTracking().FirstOrDefaultAsync(i => i.Id == imageId && i.AuctionId == auctionId, ct)
        ?? throw new KeyNotFoundException("Imagen no encontrada.");

    public async Task CancelAsync(Guid auctionId, Guid userId, bool isAdmin, CancellationToken ct)
    {
        var auction = await _db.Auctions.FirstOrDefaultAsync(a => a.Id == auctionId, ct)
                      ?? throw new KeyNotFoundException("Subasta no encontrada.");

        if (!isAdmin)
        {
            if (auction.SellerId != userId)
            {
                throw new UnauthorizedAccessException("Solo el vendedor o un administrador pueden cancelar la subasta.");
            }

            if (auction.BidCount > 0)
            {
                throw new DomainException("No se puede cancelar una subasta que ya tiene pujas.");
            }
        }

        auction.Cancel(Now);

        var bidderIds = await _db.Bids.Where(b => b.AuctionId == auctionId).Select(b => b.BidderId).Distinct().ToListAsync(ct);
        var created = bidderIds
            .Append(auction.SellerId)
            .Distinct()
            .Select(uid => _notifications.Enqueue(uid, NotificationType.AuctionCancelled,
                $"La subasta \"{auction.Title}\" fue cancelada.", auction.Id))
            .ToList();

        await _db.SaveChangesAsync(ct);
        await _notifications.PublishAsync(created, ct);
        await _hub.Clients.Group(AuctionHub.AuctionGroup(auctionId)).SendAsync(AuctionCancelledEvent, new { auctionId }, ct);
    }

    public async Task<IReadOnlyList<AuctionSummaryDto>> GetUserAuctionsAsync(Guid userId, string type, CancellationToken ct)
    {
        var q = _db.Auctions.AsNoTracking();
        q = type switch
        {
            "won" => q.Where(a => a.WinnerId == userId),
            "participating" => q.Where(a => a.Bids.Any(b => b.BidderId == userId)),
            _ => q.Where(a => a.SellerId == userId)
        };

        var now = Now;
        var items = await ProjectSummary(q.OrderByDescending(a => a.CreatedAt).Take(200)).ToListAsync(ct);
        return items.Select(i => i.ToDto(now)).ToList();
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct) =>
        await _db.Categories.AsNoTracking().OrderBy(c => c.Id).Select(c => new CategoryDto(c.Id, c.Name)).ToListAsync(ct);

    internal static string ImageUrl(Guid auctionId, Guid imageId) => $"/auctions/{auctionId}/images/{imageId}";

    internal static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    internal static async Task<byte[]> ReadAndValidateImageAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
        {
            throw new DomainException("El archivo está vacío.");
        }

        if (file.Length > MaxImageBytes)
        {
            throw new DomainException("La imagen no puede superar los 2 MB.");
        }

        var contentType = file.ContentType.ToLowerInvariant();
        if (!AllowedImageSignatures.TryGetValue(contentType, out var signatures))
        {
            throw new DomainException("Formato no permitido. Use JPG, PNG, GIF o WEBP.");
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var data = ms.ToArray();

        if (!signatures.Any(sig => data.Length >= sig.Length && data.AsSpan(0, sig.Length).SequenceEqual(sig)))
        {
            throw new DomainException("El contenido del archivo no corresponde a una imagen válida.");
        }

        return data;
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = System.IO.Path.GetFileName(fileName);
        var cleaned = new string(name.Where(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_').ToArray());
        if (cleaned.Length == 0)
        {
            cleaned = "imagen";
        }

        return cleaned.Length > 200 ? cleaned[..200] : cleaned;
    }

    private static IQueryable<AuctionRow> ProjectSummary(IQueryable<Auction> q) =>
        q.Select(a => new AuctionRow
        {
            Id = a.Id,
            Title = a.Title,
            Category = a.Category!.Name,
            CategoryId = a.CategoryId,
            StartingPrice = a.StartingPrice,
            MinIncrement = a.MinIncrement,
            CurrentPrice = a.CurrentPrice,
            BidCount = a.BidCount,
            StartAt = a.StartAt,
            EndAt = a.EndAt,
            Status = a.Status,
            SellerName = a.Seller!.UserName,
            SellerId = a.SellerId,
            WinnerId = a.WinnerId,
            FirstImageId = a.Images.OrderBy(i => i.SortOrder).Select(i => (Guid?)i.Id).FirstOrDefault()
        });

    private sealed class AuctionRow
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public int CategoryId { get; init; }
        public decimal StartingPrice { get; init; }
        public decimal MinIncrement { get; init; }
        public decimal CurrentPrice { get; init; }
        public int BidCount { get; init; }
        public DateTime StartAt { get; init; }
        public DateTime EndAt { get; init; }
        public AuctionStatus Status { get; init; }
        public string SellerName { get; init; } = string.Empty;
        public Guid SellerId { get; init; }
        public Guid? WinnerId { get; init; }
        public Guid? FirstImageId { get; init; }

        public AuctionSummaryDto ToDto(DateTime now)
        {
            var auction = new Auction
            {
                StartAt = StartAt, EndAt = EndAt, Status = Status, BidCount = BidCount,
                StartingPrice = StartingPrice, CurrentPrice = CurrentPrice, MinIncrement = MinIncrement
            };
            return new AuctionSummaryDto(
                Id, Title, Category, CategoryId, StartingPrice, CurrentPrice, auction.NextMinimumBid(), BidCount,
                StartAt, EndAt, DtoMappings.StatusName(auction.GetEffectiveStatus(now)), SellerName, SellerId, WinnerId,
                FirstImageId.HasValue ? ImageUrl(Id, FirstImageId.Value) : null);
        }
    }
}
