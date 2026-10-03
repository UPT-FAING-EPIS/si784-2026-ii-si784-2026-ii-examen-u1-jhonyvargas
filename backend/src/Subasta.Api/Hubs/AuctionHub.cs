using Microsoft.AspNetCore.SignalR;
using Subasta.Api.Infrastructure;

namespace Subasta.Api.Hubs;

/// <summary>
/// Hub de WebSockets (SignalR) para actualizaciones en tiempo real.
/// Los clientes se suscriben al grupo de una subasta para recibir pujas y cierres;
/// los usuarios autenticados reciben además sus notificaciones personales.
/// </summary>
public sealed class AuctionHub : Hub
{
    public const string Path = "/hubs/auctions";

    public static string AuctionGroup(Guid auctionId) => $"auction:{auctionId:N}";

    public static string UserGroup(Guid userId) => $"user:{userId:N}";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.GetUserIdOrNull();
        if (userId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId.Value));
        }

        await base.OnConnectedAsync();
    }

    public Task JoinAuction(Guid auctionId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, AuctionGroup(auctionId));

    public Task LeaveAuction(Guid auctionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, AuctionGroup(auctionId));
}
