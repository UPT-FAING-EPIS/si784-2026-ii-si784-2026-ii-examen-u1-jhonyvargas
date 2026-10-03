using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Subasta.Api.Data;
using Subasta.Api.Domain;
using Subasta.Api.Dtos;

namespace Subasta.Api.Services;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "subasta-api";
    public string Audience { get; set; } = "subasta-web";
    public string Key { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 480;
}

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    string CreateToken(User user, out DateTime expiresAt);
}

public sealed class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly JwtOptions _options;
    private readonly TimeProvider _time;

    public AuthService(AppDbContext db, IPasswordHasher<User> hasher, IOptions<JwtOptions> options, TimeProvider time)
    {
        _db = db;
        _hasher = hasher;
        _options = options.Value;
        _time = time;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var userName = request.UserName.Trim();

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
        {
            throw new DomainException("El correo ya se encuentra registrado.");
        }

        if (await _db.Users.AnyAsync(u => u.UserName == userName, ct))
        {
            throw new DomainException("El nombre de usuario no está disponible.");
        }

        var user = new User
        {
            UserName = userName,
            Email = email,
            Role = UserRole.User,
            CreatedAt = _time.GetUtcNow().UtcDateTime
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        var token = CreateToken(user, out var expiresAt);
        return new AuthResponse(token, expiresAt, user.ToDto());
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("Credenciales inválidas.");
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException("Credenciales inválidas.");
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
            await _db.SaveChangesAsync(ct);
        }

        var token = CreateToken(user, out var expiresAt);
        return new AuthResponse(token, expiresAt, user.ToDto());
    }

    public string CreateToken(User user, out DateTime expiresAt)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        expiresAt = now.AddMinutes(_options.ExpirationMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
