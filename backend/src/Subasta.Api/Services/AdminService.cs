using Microsoft.EntityFrameworkCore;
using Subasta.Api.Data;
using Subasta.Api.Domain;
using Subasta.Api.Dtos;

namespace Subasta.Api.Services;

public interface IAdminService
{
    Task<AdminStatsDto> GetStatsAsync(CancellationToken ct);
    Task<IReadOnlyList<UserDto>> GetUsersAsync(string? search, CancellationToken ct);
    Task<UserDto> UpdateUserAsync(Guid currentAdminId, Guid userId, UpdateUserRequest request, CancellationToken ct);
}

public sealed class AdminService : IAdminService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public AdminService(AppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<AdminStatsDto> GetStatsAsync(CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var open = _db.Auctions.Where(a => a.Status != AuctionStatus.Cancelled && a.Status != AuctionStatus.Finished);

        var awarded = await _db.Auctions
            .Where(a => a.Status == AuctionStatus.Finished && a.WinnerId != null)
            .Select(a => a.CurrentPrice)
            .ToListAsync(ct);

        return new AdminStatsDto(
            TotalUsers: await _db.Users.CountAsync(ct),
            TotalAuctions: await _db.Auctions.CountAsync(ct),
            ActiveAuctions: await open.CountAsync(a => a.StartAt <= now && a.EndAt > now, ct),
            UpcomingAuctions: await open.CountAsync(a => a.StartAt > now, ct),
            FinishedAuctions: await _db.Auctions.CountAsync(a => a.Status == AuctionStatus.Finished
                                                                  || (a.Status != AuctionStatus.Cancelled && a.EndAt <= now), ct),
            CancelledAuctions: await _db.Auctions.CountAsync(a => a.Status == AuctionStatus.Cancelled, ct),
            TotalBids: await _db.Bids.CountAsync(ct),
            TotalAwardedAmount: awarded.Sum());
    }

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(string? search, CancellationToken ct)
    {
        var q = _db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            q = q.Where(u => u.UserName.ToLower().Contains(term) || u.Email.Contains(term));
        }

        var users = await q.OrderBy(u => u.UserName).Take(500).ToListAsync(ct);
        return users.Select(u => u.ToDto()).ToList();
    }

    public async Task<UserDto> UpdateUserAsync(Guid currentAdminId, Guid userId, UpdateUserRequest request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new KeyNotFoundException("Usuario no encontrado.");

        if (userId == currentAdminId && (request.IsActive == false || request.Role == nameof(UserRole.User)))
        {
            throw new DomainException("No puede desactivarse ni quitarse el rol de administrador a sí mismo.");
        }

        if (request.Role is not null)
        {
            user.Role = Enum.Parse<UserRole>(request.Role);
        }

        if (request.IsActive.HasValue)
        {
            user.IsActive = request.IsActive.Value;
        }

        await _db.SaveChangesAsync(ct);
        return user.ToDto();
    }
}
