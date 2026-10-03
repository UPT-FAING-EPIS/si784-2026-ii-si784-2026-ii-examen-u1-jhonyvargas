using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Subasta.Api.Data;
using Subasta.Api.Domain;
using Subasta.Api.Dtos;
using Subasta.Api.Hubs;

namespace Subasta.Api.Services;

public interface INotificationService
{
    /// <summary>Agrega notificaciones al contexto (se persisten con el siguiente SaveChanges).</summary>
    Notification Enqueue(Guid userId, NotificationType type, string message, Guid? auctionId);

    /// <summary>Envía en tiempo real las notificaciones ya persistidas.</summary>
    Task PublishAsync(IEnumerable<Notification> notifications, CancellationToken ct);

    Task<IReadOnlyList<NotificationDto>> GetForUserAsync(Guid userId, bool unreadOnly, CancellationToken ct);
    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct);
    Task MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken ct);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct);
}

public sealed class NotificationService : INotificationService
{
    public const string NotificationEvent = "Notification";

    private readonly AppDbContext _db;
    private readonly IHubContext<AuctionHub> _hub;
    private readonly TimeProvider _time;

    public NotificationService(AppDbContext db, IHubContext<AuctionHub> hub, TimeProvider time)
    {
        _db = db;
        _hub = hub;
        _time = time;
    }

    public Notification Enqueue(Guid userId, NotificationType type, string message, Guid? auctionId)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Message = message.Length > 500 ? message[..500] : message,
            AuctionId = auctionId,
            CreatedAt = _time.GetUtcNow().UtcDateTime
        };
        _db.Notifications.Add(notification);
        return notification;
    }

    public async Task PublishAsync(IEnumerable<Notification> notifications, CancellationToken ct)
    {
        foreach (var n in notifications)
        {
            await _hub.Clients.Group(AuctionHub.UserGroup(n.UserId)).SendAsync(NotificationEvent, n.ToDto(), ct);
        }
    }

    public async Task<IReadOnlyList<NotificationDto>> GetForUserAsync(Guid userId, bool unreadOnly, CancellationToken ct)
    {
        var query = _db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var items = await query.OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync(ct);
        return items.Select(n => n.ToDto()).ToList();
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken ct) =>
        _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public async Task MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken ct)
    {
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct)
                           ?? throw new KeyNotFoundException("Notificación no encontrada.");
        notification.IsRead = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct)
    {
        var pending = await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync(ct);
        pending.ForEach(n => n.IsRead = true);
        await _db.SaveChangesAsync(ct);
    }
}
