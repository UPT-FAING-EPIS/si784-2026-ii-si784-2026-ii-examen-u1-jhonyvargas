using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Subasta.Api.Data;
using Subasta.Api.Domain;
using Subasta.Api.Hubs;

namespace Subasta.Api.Services;

public interface IAuctionLifecycleService
{
    /// <summary>Activa subastas programadas y cierra/adjudica las vencidas. Devuelve la cantidad de subastas cerradas.</summary>
    Task<int> ProcessDueAuctionsAsync(CancellationToken ct);

    /// <summary>Cierre manual (administrador).</summary>
    Task CloseAsync(Guid auctionId, CancellationToken ct);
}

public sealed record AuctionClosedEvent(Guid AuctionId, Guid? WinnerId, string? WinnerName, decimal FinalPrice);

public sealed class AuctionLifecycleService : IAuctionLifecycleService
{
    public const string AuctionClosedEventName = "AuctionClosed";
    public const string AuctionStartedEventName = "AuctionStarted";

    private readonly AppDbContext _db;
    private readonly TimeProvider _time;
    private readonly INotificationService _notifications;
    private readonly IHubContext<AuctionHub> _hub;
    private readonly ILogger<AuctionLifecycleService> _logger;

    public AuctionLifecycleService(AppDbContext db, TimeProvider time, INotificationService notifications,
        IHubContext<AuctionHub> hub, ILogger<AuctionLifecycleService> logger)
    {
        _db = db;
        _time = time;
        _notifications = notifications;
        _hub = hub;
        _logger = logger;
    }

    public async Task<int> ProcessDueAuctionsAsync(CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;

        var starting = await _db.Auctions
            .Where(a => a.Status == AuctionStatus.Scheduled && a.StartAt <= now && a.EndAt > now)
            .ToListAsync(ct);
        foreach (var auction in starting)
        {
            auction.Status = AuctionStatus.Active;
        }

        if (starting.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
            foreach (var auction in starting)
            {
                await _hub.Clients.Group(AuctionHub.AuctionGroup(auction.Id))
                    .SendAsync(AuctionStartedEventName, new { auctionId = auction.Id }, ct);
            }
        }

        var dueIds = await _db.Auctions
            .Where(a => (a.Status == AuctionStatus.Active || a.Status == AuctionStatus.Scheduled) && a.EndAt <= now)
            .Select(a => a.Id)
            .ToListAsync(ct);

        var closed = 0;
        foreach (var id in dueIds)
        {
            try
            {
                await CloseInternalAsync(id, ct);
                closed++;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Conflicto de concurrencia al cerrar la subasta {AuctionId}; se reintentará.", id);
                _db.ChangeTracker.Clear();
            }
        }

        return closed;
    }

    public Task CloseAsync(Guid auctionId, CancellationToken ct) => CloseInternalAsync(auctionId, ct);

    private async Task CloseInternalAsync(Guid auctionId, CancellationToken ct)
    {
        var auction = await _db.Auctions.FirstOrDefaultAsync(a => a.Id == auctionId, ct)
                      ?? throw new KeyNotFoundException("Subasta no encontrada.");

        var highest = await _db.Bids
            .Where(b => b.AuctionId == auctionId)
            .OrderByDescending(b => b.Amount).ThenBy(b => b.CreatedAt)
            .FirstOrDefaultAsync(ct);

        auction.Close(highest, _time.GetUtcNow().UtcDateTime);

        var created = new List<Notification>();
        string? winnerName = null;
        if (highest is not null)
        {
            winnerName = await _db.Users.Where(u => u.Id == highest.BidderId).Select(u => u.UserName).FirstAsync(ct);
            created.Add(_notifications.Enqueue(highest.BidderId, NotificationType.AuctionWon,
                $"¡Felicitaciones! Ganó la subasta \"{auction.Title}\" por {highest.Amount:0.00}.", auction.Id));
            created.Add(_notifications.Enqueue(auction.SellerId, NotificationType.AuctionSold,
                $"Su subasta \"{auction.Title}\" fue adjudicada a {winnerName} por {highest.Amount:0.00}.", auction.Id));

            var losers = await _db.Bids
                .Where(b => b.AuctionId == auctionId && b.BidderId != highest.BidderId)
                .Select(b => b.BidderId).Distinct().ToListAsync(ct);
            created.AddRange(losers.Select(uid => _notifications.Enqueue(uid, NotificationType.AuctionClosed,
                $"La subasta \"{auction.Title}\" finalizó. Precio final: {highest.Amount:0.00}.", auction.Id)));
        }
        else
        {
            created.Add(_notifications.Enqueue(auction.SellerId, NotificationType.AuctionClosed,
                $"Su subasta \"{auction.Title}\" finalizó sin ofertas.", auction.Id));
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Subasta {AuctionId} cerrada. Ganador: {WinnerId}", auction.Id, auction.WinnerId);

        await _hub.Clients.Group(AuctionHub.AuctionGroup(auction.Id)).SendAsync(AuctionClosedEventName,
            new AuctionClosedEvent(auction.Id, auction.WinnerId, winnerName, auction.CurrentPrice), ct);
        await _notifications.PublishAsync(created, ct);
    }
}

/// <summary>Proceso en segundo plano que revisa periódicamente las subastas vencidas.</summary>
public sealed class AuctionClosingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuctionClosingBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public AuctionClosingBackgroundService(IServiceScopeFactory scopeFactory, IConfiguration configuration,
        ILogger<AuctionClosingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(Math.Max(1, configuration.GetValue("AuctionClosing:IntervalSeconds", 10)));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Servicio de cierre de subastas detenido.");
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IAuctionLifecycleService>();
            await service.ProcessDueAuctionsAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error al procesar el cierre de subastas.");
        }
    }
}
