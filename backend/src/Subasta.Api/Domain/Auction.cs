namespace Subasta.Api.Domain;

public class Auction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public decimal StartingPrice { get; set; }
    public decimal MinIncrement { get; set; } = 1m;
    public decimal CurrentPrice { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public AuctionStatus Status { get; set; } = AuctionStatus.Scheduled;
    public Guid SellerId { get; set; }
    public Guid? WinnerId { get; set; }
    public Guid? WinningBidId { get; set; }
    public int BidCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    /// <summary>Token de concurrencia optimista: se incrementa con cada puja.</summary>
    public int Version { get; set; }

    public Category? Category { get; set; }
    public User? Seller { get; set; }
    public User? Winner { get; set; }
    public ICollection<Bid> Bids { get; set; } = new List<Bid>();
    public ICollection<AuctionImage> Images { get; set; } = new List<AuctionImage>();

    /// <summary>Estado efectivo según la fecha actual (el estado persistido se actualiza en segundo plano).</summary>
    public AuctionStatus GetEffectiveStatus(DateTime nowUtc)
    {
        if (Status is AuctionStatus.Cancelled or AuctionStatus.Finished)
        {
            return Status;
        }

        if (nowUtc < StartAt)
        {
            return AuctionStatus.Scheduled;
        }

        return nowUtc < EndAt ? AuctionStatus.Active : AuctionStatus.Finished;
    }

    /// <summary>Monto mínimo aceptado para la siguiente puja.</summary>
    public decimal NextMinimumBid() => BidCount == 0 ? StartingPrice : CurrentPrice + MinIncrement;

    /// <summary>Valida las reglas de negocio y registra una nueva puja.</summary>
    public Bid PlaceBid(Guid bidderId, decimal amount, DateTime nowUtc)
    {
        if (GetEffectiveStatus(nowUtc) != AuctionStatus.Active)
        {
            throw new DomainException("La subasta no está activa.");
        }

        if (bidderId == SellerId)
        {
            throw new DomainException("No puede pujar en su propia subasta.");
        }

        var minimum = NextMinimumBid();
        if (amount < minimum)
        {
            throw new DomainException($"La puja debe ser mayor o igual a {minimum:0.00}.");
        }

        var bid = new Bid
        {
            AuctionId = Id,
            BidderId = bidderId,
            Amount = amount,
            CreatedAt = nowUtc
        };

        CurrentPrice = amount;
        BidCount++;
        Version++;
        Status = AuctionStatus.Active;
        Bids.Add(bid);
        return bid;
    }

    /// <summary>Cierra la subasta y adjudica al mejor postor (si existe).</summary>
    public void Close(Bid? highestBid, DateTime nowUtc)
    {
        if (Status is AuctionStatus.Finished or AuctionStatus.Cancelled)
        {
            throw new DomainException("La subasta ya se encuentra cerrada.");
        }

        Status = AuctionStatus.Finished;
        ClosedAt = nowUtc;
        Version++;
        if (highestBid is not null)
        {
            WinnerId = highestBid.BidderId;
            WinningBidId = highestBid.Id;
            CurrentPrice = highestBid.Amount;
        }
    }

    public void Cancel(DateTime nowUtc)
    {
        if (Status is AuctionStatus.Finished or AuctionStatus.Cancelled)
        {
            throw new DomainException("La subasta ya se encuentra cerrada.");
        }

        Status = AuctionStatus.Cancelled;
        ClosedAt = nowUtc;
        Version++;
    }
}
