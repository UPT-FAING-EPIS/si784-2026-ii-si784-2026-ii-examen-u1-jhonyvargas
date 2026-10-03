using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Subasta.Api.Dtos;
using Subasta.Api.Infrastructure;
using Subasta.Api.Services;

namespace Subasta.Api.Controllers;

[ApiController]
[Route("bids")]
public sealed class BidsController : ControllerBase
{
    private readonly IBidService _bids;

    public BidsController(IBidService bids)
    {
        _bids = bids;
    }

    /// <summary>Realiza una oferta sobre una subasta activa.</summary>
    [Authorize]
    [HttpPost]
    [ProducesResponseType<BidDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BidDto>> Place([FromBody] PlaceBidRequest request, CancellationToken ct)
    {
        var bid = await _bids.PlaceBidAsync(User.GetUserId(), request, ct);
        return StatusCode(StatusCodes.Status201Created, bid);
    }

    /// <summary>Historial de pujas de una subasta.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BidDto>>> ByAuction([FromQuery] Guid auctionId, CancellationToken ct)
    {
        if (auctionId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(auctionId), "El parámetro auctionId es obligatorio.");
            return ValidationProblem(ModelState);
        }

        return Ok(await _bids.GetAuctionBidsAsync(auctionId, ct));
    }
}
