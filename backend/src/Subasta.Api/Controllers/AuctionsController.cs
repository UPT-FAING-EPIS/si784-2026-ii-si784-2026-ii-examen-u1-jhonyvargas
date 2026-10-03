using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Subasta.Api.Domain;
using Subasta.Api.Dtos;
using Subasta.Api.Infrastructure;
using Subasta.Api.Services;

namespace Subasta.Api.Controllers;

[ApiController]
[Route("auctions")]
public sealed class AuctionsController : ControllerBase
{
    private readonly IAuctionService _auctions;

    public AuctionsController(IAuctionService auctions)
    {
        _auctions = auctions;
    }

    /// <summary>Publica una nueva subasta.</summary>
    [Authorize]
    [HttpPost]
    [ProducesResponseType<AuctionDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuctionDetailDto>> Create([FromBody] CreateAuctionRequest request, CancellationToken ct)
    {
        var created = await _auctions.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Lista subastas (por defecto activas) con filtros y paginación.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AuctionSummaryDto>>> List([FromQuery] AuctionQuery query, CancellationToken ct) =>
        Ok(await _auctions.SearchAsync(query, ct));

    /// <summary>Detalle de una subasta.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<AuctionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuctionDetailDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await _auctions.GetAsync(id, ct));

    /// <summary>Agrega una imagen (JPG, PNG, GIF o WEBP, máx. 2 MB) a la subasta.</summary>
    [Authorize]
    [HttpPost("{id:guid}/images")]
    [RequestSizeLimit(AuctionService.MaxImageBytes + 64 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<object>> UploadImage(Guid id, IFormFile file, CancellationToken ct)
    {
        var url = await _auctions.AddImageAsync(id, User.GetUserId(), file, ct);
        return StatusCode(StatusCodes.Status201Created, new { url });
    }

    /// <summary>Descarga una imagen de la subasta.</summary>
    [HttpGet("{id:guid}/images/{imageId:guid}")]
    [ResponseCache(Duration = 86400)]
    public async Task<IActionResult> GetImage(Guid id, Guid imageId, CancellationToken ct)
    {
        var image = await _auctions.GetImageAsync(id, imageId, ct);
        return File(image.Data, image.ContentType);
    }

    /// <summary>Cancela la subasta (vendedor sin pujas o administrador).</summary>
    [Authorize]
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await _auctions.CancelAsync(id, User.GetUserId(), User.IsInRole(nameof(UserRole.Admin)), ct);
        return NoContent();
    }

    /// <summary>Catálogo de categorías.</summary>
    [HttpGet("/categories")]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> Categories(CancellationToken ct) =>
        Ok(await _auctions.GetCategoriesAsync(ct));
}
