using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Subasta.Api.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserIdOrNull(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                    ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        principal.GetUserIdOrNull() ?? throw new UnauthorizedAccessException("Usuario no autenticado.");
}
