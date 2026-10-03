using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Subasta.Api.Dtos;
using Subasta.Api.Infrastructure;
using Subasta.Api.Services;

namespace Subasta.Api.Controllers;

[ApiController]
[Authorize]
[Route("user")]
public sealed class UserController : ControllerBase
{
    private readonly IAuctionService _auctions;
    private readonly IBidService _bids;
    private readonly INotificationService _notifications;

    public UserController(IAuctionService auctions, IBidService bids, INotificationService notifications)
    {
        _auctions = auctions;
        _bids = bids;
        _notifications = notifications;
    }

    /// <summary>Subastas del usuario: published (publicadas), participating (en las que puja) o won (ganadas).</summary>
    [HttpGet("auctions")]
    public async Task<ActionResult<IReadOnlyList<AuctionSummaryDto>>> Auctions([FromQuery] string type = "published",
        CancellationToken ct = default)
    {
        if (type is not ("published" or "participating" or "won"))
        {
            ModelState.AddModelError(nameof(type), "Tipo no válido: use published, participating o won.");
            return ValidationProblem(ModelState);
        }

        return Ok(await _auctions.GetUserAuctionsAsync(User.GetUserId(), type, ct));
    }

    /// <summary>Historial de pujas del usuario.</summary>
    [HttpGet("bids")]
    public async Task<ActionResult<IReadOnlyList<UserBidDto>>> Bids(CancellationToken ct) =>
        Ok(await _bids.GetUserBidsAsync(User.GetUserId(), ct));

    /// <summary>Notificaciones del usuario.</summary>
    [HttpGet("notifications")]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> Notifications([FromQuery] bool unreadOnly = false,
        CancellationToken ct = default) =>
        Ok(await _notifications.GetForUserAsync(User.GetUserId(), unreadOnly, ct));

    /// <summary>Cantidad de notificaciones no leídas.</summary>
    [HttpGet("notifications/unread-count")]
    public async Task<ActionResult<object>> UnreadCount(CancellationToken ct) =>
        Ok(new { count = await _notifications.CountUnreadAsync(User.GetUserId(), ct) });

    [HttpPost("notifications/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await _notifications.MarkAsReadAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await _notifications.MarkAllAsReadAsync(User.GetUserId(), ct);
        return NoContent();
    }
}
