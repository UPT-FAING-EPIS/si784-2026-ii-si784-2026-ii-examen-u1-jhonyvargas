using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Subasta.Api.Data;
using Subasta.Api.Dtos;

namespace Subasta.IntegrationTests;

/// <summary>Levanta la API completa en memoria con una base SQLite aislada.</summary>
public sealed class SubastaApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@subasta.test";
    public static readonly string AdminSecret = $"Adm-{Guid.NewGuid():N}";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SubastaApiFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Key", Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Guid.NewGuid().ToString("N"));
        builder.UseSetting("AuctionClosing:Enabled", "false");
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminSecret);
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:5173");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }

    public async Task<(HttpClient Client, AuthResponse Auth)> CreateUserClientAsync(string? prefix = null)
    {
        var name = $"{prefix ?? "user"}{Guid.NewGuid():N}"[..20];
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            UserName = name,
            Email = $"{name}@test.com",
            Password = "Clave-Segura-123"
        });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, auth);
    }

    public async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = AdminEmail, Password = AdminSecret });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }
}

public static class TestJson
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web);
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<SubastaApiFactory>
{
    public const string Name = "api";
}
