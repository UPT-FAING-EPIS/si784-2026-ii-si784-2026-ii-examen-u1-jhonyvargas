using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Subasta.Api.Domain;

namespace Subasta.Api.Infrastructure;

/// <summary>Traduce excepciones conocidas a respuestas ProblemDetails (RFC 7807) sin exponer detalles internos.</summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            DomainException e => (StatusCodes.Status400BadRequest, "Regla de negocio", e.Message),
            KeyNotFoundException e => (StatusCodes.Status404NotFound, "No encontrado", e.Message),
            UnauthorizedAccessException e when httpContext.User.Identity?.IsAuthenticated == true =>
                (StatusCodes.Status403Forbidden, "Prohibido", e.Message),
            UnauthorizedAccessException e => (StatusCodes.Status401Unauthorized, "No autorizado", e.Message),
            _ => (StatusCodes.Status500InternalServerError, "Error interno", "Ocurrió un error inesperado.")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Error no controlado. TraceId: {TraceId}", httpContext.TraceIdentifier);
        }

        httpContext.Response.StatusCode = status;
        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }
}
