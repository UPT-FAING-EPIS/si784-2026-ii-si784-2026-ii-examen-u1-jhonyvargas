using Subasta.Api.Domain;

namespace Subasta.UnitTests;

public class AuctionDomainTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Seller = Guid.NewGuid();
    private static readonly Guid BidderA = Guid.NewGuid();
    private static readonly Guid BidderB = Guid.NewGuid();

    private static Auction NewAuction(DateTime? start = null, DateTime? end = null) => new()
    {
        Title = "Bicicleta",
        Description = "Bicicleta de montaña",
        SellerId = Seller,
        StartingPrice = 100m,
        CurrentPrice = 100m,
        MinIncrement = 5m,
        StartAt = start ?? Now.AddHours(-1),
        EndAt = end ?? Now.AddHours(1),
        Status = AuctionStatus.Active
    };

    [Fact]
    public void FirstBid_EqualToStartingPrice_IsAccepted()
    {
        var auction = NewAuction();

        var bid = auction.PlaceBid(BidderA, 100m, Now);

        Assert.Equal(100m, auction.CurrentPrice);
        Assert.Equal(1, auction.BidCount);
        Assert.Equal(BidderA, bid.BidderId);
        Assert.Equal(105m, auction.NextMinimumBid());
    }

    [Fact]
    public void FirstBid_BelowStartingPrice_IsRejected()
    {
        var auction = NewAuction();

        var ex = Assert.Throws<DomainException>(() => auction.PlaceBid(BidderA, 99.99m, Now));
        Assert.Contains("100.00", ex.Message);
    }

    [Fact]
    public void NextBid_MustRespectMinimumIncrement()
    {
        var auction = NewAuction();
        auction.PlaceBid(BidderA, 100m, Now);

        Assert.Throws<DomainException>(() => auction.PlaceBid(BidderB, 104m, Now));
        auction.PlaceBid(BidderB, 105m, Now);

        Assert.Equal(105m, auction.CurrentPrice);
        Assert.Equal(2, auction.BidCount);
    }

    [Fact]
    public void Seller_CannotBidOnOwnAuction()
    {
        var auction = NewAuction();

        Assert.Throws<DomainException>(() => auction.PlaceBid(Seller, 200m, Now));
    }

    [Fact]
    public void Bid_OnUpcomingAuction_IsRejected()
    {
        var auction = NewAuction(start: Now.AddHours(1), end: Now.AddHours(2));
        auction.Status = AuctionStatus.Scheduled;

        Assert.Throws<DomainException>(() => auction.PlaceBid(BidderA, 150m, Now));
    }

    [Fact]
    public void Bid_AfterEndDate_IsRejected()
    {
        var auction = NewAuction(start: Now.AddHours(-3), end: Now.AddSeconds(-1));

        Assert.Throws<DomainException>(() => auction.PlaceBid(BidderA, 150m, Now));
    }

    [Fact]
    public void Bid_IncrementsConcurrencyVersion()
    {
        var auction = NewAuction();
        var before = auction.Version;

        auction.PlaceBid(BidderA, 120m, Now);

        Assert.Equal(before + 1, auction.Version);
    }

    [Theory]
    [InlineData(-2, -1, AuctionStatus.Finished)]
    [InlineData(-1, 1, AuctionStatus.Active)]
    [InlineData(1, 2, AuctionStatus.Scheduled)]
    public void EffectiveStatus_DependsOnDates(int startHours, int endHours, AuctionStatus expected)
    {
        var auction = NewAuction(Now.AddHours(startHours), Now.AddHours(endHours));
        auction.Status = AuctionStatus.Scheduled;

        Assert.Equal(expected, auction.GetEffectiveStatus(Now));
    }

    [Fact]
    public void Cancelled_IsAlwaysCancelled()
    {
        var auction = NewAuction();
        auction.Cancel(Now);

        Assert.Equal(AuctionStatus.Cancelled, auction.GetEffectiveStatus(Now.AddDays(5)));
        Assert.Throws<DomainException>(() => auction.Cancel(Now));
        Assert.Throws<DomainException>(() => auction.PlaceBid(BidderA, 200m, Now));
    }

    [Fact]
    public void Close_WithBids_AwardsHighestBidder()
    {
        var auction = NewAuction();
        auction.PlaceBid(BidderA, 100m, Now);
        var best = auction.PlaceBid(BidderB, 150m, Now);

        auction.Close(best, Now.AddHours(2));

        Assert.Equal(AuctionStatus.Finished, auction.Status);
        Assert.Equal(BidderB, auction.WinnerId);
        Assert.Equal(best.Id, auction.WinningBidId);
        Assert.Equal(150m, auction.CurrentPrice);
        Assert.NotNull(auction.ClosedAt);
    }

    [Fact]
    public void Close_WithoutBids_HasNoWinner_AndCannotCloseTwice()
    {
        var auction = NewAuction();

        auction.Close(null, Now);

        Assert.Null(auction.WinnerId);
        Assert.Throws<DomainException>(() => auction.Close(null, Now));
    }
}
