namespace Subasta.Api.Domain;

public enum UserRole
{
    User = 0,
    Admin = 1
}

public enum AuctionStatus
{
    Scheduled = 0,
    Active = 1,
    Finished = 2,
    Cancelled = 3
}

public enum NotificationType
{
    NewBid = 0,
    Outbid = 1,
    AuctionClosed = 2,
    AuctionWon = 3,
    AuctionSold = 4,
    AuctionCancelled = 5
}
