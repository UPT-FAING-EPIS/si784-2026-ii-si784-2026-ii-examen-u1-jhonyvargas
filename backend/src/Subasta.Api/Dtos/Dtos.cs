using System.ComponentModel.DataAnnotations;
using Subasta.Api.Domain;

namespace Subasta.Api.Dtos;

// ---------- Autenticación ----------

public sealed class RegisterRequest
{
    [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "El nombre de usuario debe tener entre 3 y 50 caracteres.")]
    [RegularExpression("^[a-zA-Z0-9_.-]+$", ErrorMessage = "Solo se permiten letras, números, '.', '_' y '-'.", MatchTimeoutInMilliseconds = 1000)]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener entre 8 y 100 caracteres.")]
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    public string Password { get; set; } = string.Empty;
}

public sealed record UserDto(Guid Id, string UserName, string Email, string Role, bool IsActive, DateTime CreatedAt);

public sealed record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);

// ---------- Subastas ----------

public sealed class CreateAuctionRequest : IValidatableObject
{
    [Required(ErrorMessage = "El título es obligatorio.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "El título debe tener entre 3 y 120 caracteres.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(4000, MinimumLength = 10, ErrorMessage = "La descripción debe tener entre 10 y 4000 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar una categoría.")]
    public int CategoryId { get; set; }

    [Range(typeof(decimal), "0.01", "99999999", ErrorMessage = "El precio inicial debe ser mayor a 0.")]
    public decimal StartingPrice { get; set; }

    [Range(typeof(decimal), "0.01", "1000000", ErrorMessage = "El incremento mínimo debe ser mayor a 0.")]
    public decimal MinIncrement { get; set; } = 1m;

    /// <summary>Opcional: si no se envía, la subasta inicia inmediatamente.</summary>
    public DateTime? StartAt { get; set; }

    [Required(ErrorMessage = "La fecha de cierre es obligatoria.")]
    public DateTime? EndAt { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var now = DateTime.UtcNow;
        var start = StartAt?.ToUniversalTime() ?? now;
        var end = EndAt?.ToUniversalTime();

        if (StartAt.HasValue && start < now.AddMinutes(-1))
        {
            yield return new ValidationResult("La fecha de inicio no puede estar en el pasado.", [nameof(StartAt)]);
        }

        if (end.HasValue && end.Value <= start.AddMinutes(1))
        {
            yield return new ValidationResult("La fecha de cierre debe ser posterior al inicio (mínimo 1 minuto).", [nameof(EndAt)]);
        }

        if (end.HasValue && end.Value > start.AddDays(90))
        {
            yield return new ValidationResult("La subasta no puede durar más de 90 días.", [nameof(EndAt)]);
        }

        if (decimal.Round(StartingPrice, 2) != StartingPrice || decimal.Round(MinIncrement, 2) != MinIncrement)
        {
            yield return new ValidationResult("Los montos admiten como máximo 2 decimales.", [nameof(StartingPrice)]);
        }
    }
}

public sealed class AuctionQuery
{
    /// <summary>active | upcoming | finished | all</summary>
    [RegularExpression("^(active|upcoming|finished|cancelled|all)$", ErrorMessage = "Estado no válido.", MatchTimeoutInMilliseconds = 1000)]
    public string? Status { get; set; } = "active";

    [StringLength(100)]
    public string? Search { get; set; }

    public int? CategoryId { get; set; }

    [Range(typeof(decimal), "0", "99999999")]
    public decimal? MinPrice { get; set; }

    [Range(typeof(decimal), "0", "99999999")]
    public decimal? MaxPrice { get; set; }

    [RegularExpression("^(endingSoon|newest|priceAsc|priceDesc)$", ErrorMessage = "Orden no válido.", MatchTimeoutInMilliseconds = 1000)]
    public string? Sort { get; set; } = "endingSoon";

    [Range(1, 10000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 12;
}

public sealed record AuctionSummaryDto(
    Guid Id,
    string Title,
    string Category,
    int CategoryId,
    decimal StartingPrice,
    decimal CurrentPrice,
    decimal NextMinimumBid,
    int BidCount,
    DateTime StartAt,
    DateTime EndAt,
    string Status,
    string SellerName,
    Guid SellerId,
    Guid? WinnerId,
    string? ImageUrl);

public sealed record AuctionDetailDto(
    Guid Id,
    string Title,
    string Description,
    string Category,
    int CategoryId,
    decimal StartingPrice,
    decimal MinIncrement,
    decimal CurrentPrice,
    decimal NextMinimumBid,
    int BidCount,
    DateTime StartAt,
    DateTime EndAt,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    string Status,
    Guid SellerId,
    string SellerName,
    Guid? WinnerId,
    string? WinnerName,
    IReadOnlyList<string> ImageUrls);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

// ---------- Pujas ----------

public sealed class PlaceBidRequest
{
    [Required(ErrorMessage = "La subasta es obligatoria.")]
    public Guid? AuctionId { get; set; }

    [Range(typeof(decimal), "0.01", "99999999", ErrorMessage = "El monto debe ser mayor a 0.")]
    public decimal Amount { get; set; }
}

public sealed record BidDto(Guid Id, Guid AuctionId, string AuctionTitle, Guid BidderId, string BidderName, decimal Amount, DateTime CreatedAt);

public sealed record UserBidDto(
    Guid Id,
    Guid AuctionId,
    string AuctionTitle,
    decimal Amount,
    DateTime CreatedAt,
    decimal AuctionCurrentPrice,
    string AuctionStatus,
    bool IsWinning);

// ---------- Notificaciones ----------

public sealed record NotificationDto(Guid Id, string Type, string Message, Guid? AuctionId, bool IsRead, DateTime CreatedAt);

// ---------- Administración ----------

public sealed class UpdateUserRequest
{
    [RegularExpression("^(User|Admin)$", ErrorMessage = "Rol no válido.", MatchTimeoutInMilliseconds = 1000)]
    public string? Role { get; set; }

    public bool? IsActive { get; set; }
}

public sealed record AdminStatsDto(
    int TotalUsers,
    int TotalAuctions,
    int ActiveAuctions,
    int UpcomingAuctions,
    int FinishedAuctions,
    int CancelledAuctions,
    int TotalBids,
    decimal TotalAwardedAmount);

public sealed record CategoryDto(int Id, string Name);

internal static class DtoMappings
{
    public static UserDto ToDto(this User u) => new(u.Id, u.UserName, u.Email, u.Role.ToString(), u.IsActive, u.CreatedAt);

    public static NotificationDto ToDto(this Notification n) =>
        new(n.Id, n.Type.ToString(), n.Message, n.AuctionId, n.IsRead, n.CreatedAt);

    public static string StatusName(AuctionStatus status) => status switch
    {
        AuctionStatus.Scheduled => "upcoming",
        AuctionStatus.Active => "active",
        AuctionStatus.Finished => "finished",
        _ => "cancelled"
    };
}
