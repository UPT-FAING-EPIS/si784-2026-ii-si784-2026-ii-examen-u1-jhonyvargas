using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Subasta.Api.Data;
using Subasta.Api.Domain;
using Subasta.Api.Hubs;
using Subasta.Api.Infrastructure;
using Subasta.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

// ---------- Persistencia ----------
// PostgreSQL en la nube; "Database:Provider=Sqlite" habilita un modo local sin Docker (demo/desarrollo).
var connectionString = ConnectionStrings.Normalize(builder.Configuration.GetConnectionString("Default"));
var useSqlite = string.Equals(builder.Configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useSqlite)
    {
        options.UseSqlite(string.IsNullOrWhiteSpace(connectionString) ? "Data Source=subasta-local.db" : connectionString);
    }
    else
    {
        options.UseNpgsql(connectionString);
    }
});

// ---------- Seguridad (JWT) ----------
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.Key))
{
    if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
    {
        throw new InvalidOperationException("Debe configurar Jwt__Key (mínimo 32 caracteres).");
    }

    // En desarrollo se genera una clave efímera si no se configuró ninguna.
    jwtOptions.Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
}

if (Encoding.UTF8.GetByteCount(jwtOptions.Key) < 32)
{
    throw new InvalidOperationException("Jwt__Key debe tener al menos 32 caracteres.");
}

builder.Services.Configure<JwtOptions>(o =>
{
    o.Issuer = jwtOptions.Issuer;
    o.Audience = jwtOptions.Audience;
    o.Key = jwtOptions.Key;
    o.ExpirationMinutes = jwtOptions.ExpirationMinutes;
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        // SignalR envía el token por query string al abrir el WebSocket.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments(AuctionHub.Path))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// ---------- CORS ----------
const string CorsPolicy = "frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .WithMethods("GET", "POST", "PATCH", "PUT", "DELETE")
    .WithHeaders("Authorization", "Content-Type", "X-Requested-With", "X-SignalR-User-Agent")
    .AllowCredentials()));

// ---------- Servicios de aplicación ----------
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IAuctionService, AuctionService>();
builder.Services.AddScoped<IBidService, BidService>();
builder.Services.AddScoped<IAuctionLifecycleService, AuctionLifecycleService>();
builder.Services.AddScoped<IAdminService, AdminService>();
if (builder.Configuration.GetValue("AuctionClosing:Enabled", true))
{
    builder.Services.AddHostedService<AuctionClosingBackgroundService>();
}

builder.Services.AddSignalR();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapControllers();
app.MapHub<AuctionHub>(AuctionHub.Path);
app.MapHealthChecks("/health");

if (app.Configuration.GetValue("Database:InitializeOnStartup", true))
{
    await DbInitializer.InitializeAsync(app.Services, app.Configuration, app.Logger);
}

await app.RunAsync();

/// <summary>Punto de entrada expuesto para las pruebas de integración.</summary>
public partial class Program
{
    protected Program()
    {
    }
}
