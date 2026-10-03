using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Subasta.Api.Domain;

namespace Subasta.Api.Data;

public static class DbInitializer
{
    /// <summary>
    /// Aplica migraciones (PostgreSQL) o crea el esquema (SQLite en pruebas) y crea el administrador
    /// inicial a partir de la configuración Seed:AdminEmail / Seed:AdminPassword (variables de entorno o secretos).
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Database.IsNpgsql())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }

        var adminEmail = configuration["Seed:AdminEmail"]?.Trim().ToLowerInvariant();
        var adminSecret = configuration["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminSecret))
        {
            return;
        }

        if (await db.Users.AnyAsync(u => u.Email == adminEmail))
        {
            return;
        }

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var admin = new User
        {
            UserName = configuration["Seed:AdminUserName"] ?? "admin",
            Email = adminEmail,
            Role = UserRole.Admin
        };
        admin.PasswordHash = hasher.HashPassword(admin, adminSecret);
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        logger.LogInformation("Administrador inicial creado: {Email}", adminEmail);
    }
}
