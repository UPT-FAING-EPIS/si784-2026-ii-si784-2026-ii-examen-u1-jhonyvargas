using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Subasta.Api.Data;
using Subasta.Api.Domain;
using Subasta.Api.Dtos;
using Subasta.Api.Services;

namespace Subasta.UnitTests;

public sealed class ServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly FakeTimeProvider _time = new(new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc));
    private readonly RecordingHubContext _hub = new();
    private readonly AppDbContext _db;

    private readonly User _seller = new() { UserName = "vendedor", Email = "vendedor@test.com", PasswordHash = "x" };
    private readonly User _alice = new() { UserName = "alice", Email = "alice@test.com", PasswordHash = "x" };
    private readonly User _bob = new() { UserName = "bob", Email = "bob@test.com", PasswordHash = "x" };

    public ServiceTests()
    {
        _db = _database.CreateContext();
        _db.Users.AddRange(_seller, _alice, _bob);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private NotificationService Notifications() => new(_db, _hub, _time);

    private BidService Bids() => new(_db, _time, Notifications(), _hub);

    private AuctionService Auctions() => new(_db, _time, Notifications(), _hub);

    private AuctionLifecycleService Lifecycle() =>
        new(_db, _time, Notifications(), _hub, NullLogger<AuctionLifecycleService>.Instance);

    private async Task<Guid> CreateAuctionAsync(TimeSpan duration, TimeSpan? startsIn = null)
    {
        var dto = await Auctions().CreateAsync(_seller.Id, new CreateAuctionRequest
        {
            Title = "Guitarra eléctrica",
            Description = "Guitarra eléctrica con amplificador",
            CategoryId = 1,
            StartingPrice = 100m,
            MinIncrement = 10m,
            StartAt = startsIn.HasValue ? _time.UtcNow.Add(startsIn.Value) : null,
            EndAt = _time.UtcNow.Add((startsIn ?? TimeSpan.Zero) + duration)
        }, CancellationToken.None);
        return dto.Id;
    }

    [Fact]
    public async Task PlaceBid_NotifiesSellerAndOutbidUser_AndBroadcasts()
    {
        var auctionId = await CreateAuctionAsync(TimeSpan.FromHours(1));

        await Bids().PlaceBidAsync(_alice.Id, new PlaceBidRequest { AuctionId = auctionId, Amount = 100m }, CancellationToken.None);
        await Bids().PlaceBidAsync(_bob.Id, new PlaceBidRequest { AuctionId = auctionId, Amount = 120m }, CancellationToken.None);

        var notifications = await _db.Notifications.AsNoTracking().ToListAsync();
        Assert.Equal(2, notifications.Count(n => n.UserId == _seller.Id && n.Type == NotificationType.NewBid));
        Assert.Single(notifications, n => n.UserId == _alice.Id && n.Type == NotificationType.Outbid);
        Assert.Equal(2, _hub.Sent.Count(m => m.Method == BidService.BidPlacedEventName));

        var auction = await _db.Auctions.AsNoTracking().SingleAsync(a => a.Id == auctionId);
        Assert.Equal(120m, auction.CurrentPrice);
        Assert.Equal(2, auction.BidCount);
    }

    [Fact]
    public async Task PlaceBid_OnUnknownAuction_Throws()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            Bids().PlaceBidAsync(_alice.Id, new PlaceBidRequest { AuctionId = Guid.NewGuid(), Amount = 10m }, CancellationToken.None));
    }

    [Fact]
    public async Task Lifecycle_ClosesDueAuctions_AndAwardsWinner()
    {
        var auctionId = await CreateAuctionAsync(TimeSpan.FromMinutes(30));
        await Bids().PlaceBidAsync(_alice.Id, new PlaceBidRequest { AuctionId = auctionId, Amount = 100m }, CancellationToken.None);
        await Bids().PlaceBidAsync(_bob.Id, new PlaceBidRequest { AuctionId = auctionId, Amount = 150m }, CancellationToken.None);

        _time.Advance(TimeSpan.FromMinutes(31));
        _db.ChangeTracker.Clear();
        var closed = await Lifecycle().ProcessDueAuctionsAsync(CancellationToken.None);

        Assert.Equal(1, closed);
        var auction = await _db.Auctions.AsNoTracking().SingleAsync(a => a.Id == auctionId);
        Assert.Equal(AuctionStatus.Finished, auction.Status);
        Assert.Equal(_bob.Id, auction.WinnerId);

        var notifications = await _db.Notifications.AsNoTracking().ToListAsync();
        Assert.Contains(notifications, n => n.UserId == _bob.Id && n.Type == NotificationType.AuctionWon);
        Assert.Contains(notifications, n => n.UserId == _seller.Id && n.Type == NotificationType.AuctionSold);
        Assert.Contains(notifications, n => n.UserId == _alice.Id && n.Type == NotificationType.AuctionClosed);
        Assert.Contains(_hub.Sent, m => m.Method == AuctionLifecycleService.AuctionClosedEventName);
    }

    [Fact]
    public async Task Lifecycle_ActivatesScheduledAuctions()
    {
        var auctionId = await CreateAuctionAsync(TimeSpan.FromHours(1), startsIn: TimeSpan.FromMinutes(10));
        Assert.Equal("upcoming", (await Auctions().GetAsync(auctionId, CancellationToken.None)).Status);

        _time.Advance(TimeSpan.FromMinutes(11));
        await Lifecycle().ProcessDueAuctionsAsync(CancellationToken.None);

        var auction = await _db.Auctions.AsNoTracking().SingleAsync(a => a.Id == auctionId);
        Assert.Equal(AuctionStatus.Active, auction.Status);
    }

    [Fact]
    public async Task Lifecycle_WithoutBids_NotifiesSellerOnly()
    {
        var auctionId = await CreateAuctionAsync(TimeSpan.FromMinutes(5));
        _time.Advance(TimeSpan.FromMinutes(6));

        await Lifecycle().ProcessDueAuctionsAsync(CancellationToken.None);

        var notification = await _db.Notifications.AsNoTracking().SingleAsync();
        Assert.Equal(_seller.Id, notification.UserId);
        Assert.Null((await _db.Auctions.AsNoTracking().SingleAsync(a => a.Id == auctionId)).WinnerId);
    }

    [Fact]
    public async Task Search_FiltersByStatusTextAndPrice()
    {
        await CreateAuctionAsync(TimeSpan.FromHours(1));
        await CreateAuctionAsync(TimeSpan.FromHours(1), startsIn: TimeSpan.FromHours(1));

        var active = await Auctions().SearchAsync(new AuctionQuery { Status = "active" }, CancellationToken.None);
        var upcoming = await Auctions().SearchAsync(new AuctionQuery { Status = "upcoming" }, CancellationToken.None);
        var byText = await Auctions().SearchAsync(new AuctionQuery { Status = "all", Search = "GUITARRA" }, CancellationToken.None);
        var byPrice = await Auctions().SearchAsync(new AuctionQuery { Status = "all", MinPrice = 500m }, CancellationToken.None);

        Assert.Equal(1, active.TotalCount);
        Assert.Equal(1, upcoming.TotalCount);
        Assert.Equal(2, byText.TotalCount);
        Assert.Equal(0, byPrice.TotalCount);
    }

    [Fact]
    public async Task Cancel_BySellerWithBids_IsRejected_ButAdminCanCancel()
    {
        var auctionId = await CreateAuctionAsync(TimeSpan.FromHours(1));
        await Bids().PlaceBidAsync(_alice.Id, new PlaceBidRequest { AuctionId = auctionId, Amount = 100m }, CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(() => Auctions().CancelAsync(auctionId, _seller.Id, false, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Auctions().CancelAsync(auctionId, _bob.Id, false, CancellationToken.None));

        await Auctions().CancelAsync(auctionId, Guid.NewGuid(), true, CancellationToken.None);

        Assert.Equal("cancelled", (await Auctions().GetAsync(auctionId, CancellationToken.None)).Status);
        Assert.Contains(await _db.Notifications.AsNoTracking().ToListAsync(),
            n => n.UserId == _alice.Id && n.Type == NotificationType.AuctionCancelled);
    }

    [Fact]
    public async Task UserBids_ReportWinningState()
    {
        var auctionId = await CreateAuctionAsync(TimeSpan.FromHours(1));
        await Bids().PlaceBidAsync(_alice.Id, new PlaceBidRequest { AuctionId = auctionId, Amount = 100m }, CancellationToken.None);
        await Bids().PlaceBidAsync(_bob.Id, new PlaceBidRequest { AuctionId = auctionId, Amount = 110m }, CancellationToken.None);

        var aliceBids = await Bids().GetUserBidsAsync(_alice.Id, CancellationToken.None);
        var bobBids = await Bids().GetUserBidsAsync(_bob.Id, CancellationToken.None);

        Assert.False(Assert.Single(aliceBids).IsWinning);
        Assert.True(Assert.Single(bobBids).IsWinning);
    }

    [Fact]
    public async Task Notifications_CanBeMarkedAsRead()
    {
        var service = Notifications();
        service.Enqueue(_alice.Id, NotificationType.NewBid, "uno", null);
        service.Enqueue(_alice.Id, NotificationType.NewBid, "dos", null);
        await _db.SaveChangesAsync();

        Assert.Equal(2, await service.CountUnreadAsync(_alice.Id, CancellationToken.None));
        var first = (await service.GetForUserAsync(_alice.Id, true, CancellationToken.None))[0];
        await service.MarkAsReadAsync(_alice.Id, first.Id, CancellationToken.None);
        Assert.Equal(1, await service.CountUnreadAsync(_alice.Id, CancellationToken.None));

        await service.MarkAllAsReadAsync(_alice.Id, CancellationToken.None);
        Assert.Equal(0, await service.CountUnreadAsync(_alice.Id, CancellationToken.None));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.MarkAsReadAsync(_bob.Id, first.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Admin_CannotDemoteSelf_AndStatsAreComputed()
    {
        var admin = new AdminService(_db, _time);

        await Assert.ThrowsAsync<DomainException>(() =>
            admin.UpdateUserAsync(_seller.Id, _seller.Id, new UpdateUserRequest { IsActive = false }, CancellationToken.None));

        var updated = await admin.UpdateUserAsync(_seller.Id, _alice.Id, new UpdateUserRequest { Role = "Admin" }, CancellationToken.None);
        Assert.Equal("Admin", updated.Role);

        await CreateAuctionAsync(TimeSpan.FromHours(1));
        var stats = await admin.GetStatsAsync(CancellationToken.None);
        Assert.Equal(3, stats.TotalUsers);
        Assert.Equal(1, stats.ActiveAuctions);
    }
}
