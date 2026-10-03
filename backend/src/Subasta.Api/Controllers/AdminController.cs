using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Subasta.Api.Dtos;
using Subasta.Api.Infrastructure;
using Subasta.Api.Services;

namespace Subasta.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("admin")]
public sealed class AdminController : ControllerBase
{
    private readonly IAdminService _admin;
    private readonly IAuctionService _auctions;
    private readonly IAuctionLifecycleService _lifecycle;

    public AdminController(IAdminService admin, IAuctionService auctions, IAuctionLifecycleService lifecycle)
    {
        _admin = admin;
        _auctions = auctions;
        _lifecycle = lifecycle;
    }

    /// <summary>Indicadores globales de la plataforma.</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsDto>> Stats(CancellationToken ct) => Ok(await _admin.GetStatsAsync(ct));

    /// <summary>Listado de usuarios.</summary>
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> Users([FromQuery] string? search, CancellationToken ct) =>
        Ok(await _admin.GetUsersAsync(search, ct));

    /// <summary>Cambia el rol o el estado de un usuario.</summary>
    [HttpPatch("users/{id:guid}")]
    public async Task<ActionResult<UserDto>> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct) =>
        Ok(await _admin.UpdateUserAsync(User.GetUserId(), id, request, ct));

    /// <summary>Listado global de subastas (todos los estados).</summary>
    [HttpGet("auctions")]
    public async Task<ActionResult<PagedResult<AuctionSummaryDto>>> Auctions([FromQuery] AuctionQuery query, CancellationToken ct) =>
        Ok(await _auctions.SearchAsync(query, ct));

    /// <summary>Cierra y adjudica manualmente una subasta.</summary>
    [HttpPost("auctions/{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        await _lifecycle.CloseAsync(id, ct);
        return NoContent();
    }

    /// <summary>Cancela una subasta.</summary>
    [HttpPost("auctions/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await _auctions.CancelAsync(id, User.GetUserId(), true, ct);
        return NoContent();
    }
}
