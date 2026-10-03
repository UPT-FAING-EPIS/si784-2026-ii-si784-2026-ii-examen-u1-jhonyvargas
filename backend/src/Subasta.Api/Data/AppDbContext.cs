using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Subasta.Api.Domain;

namespace Subasta.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Auction> Auctions => Set<Auction>();
    public DbSet<AuctionImage> AuctionImages => Set<AuctionImage>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureUsers(modelBuilder);
        ConfigureCategories(modelBuilder);
        ConfigureAuctions(modelBuilder);
        ConfigureImages(modelBuilder);
        ConfigureBids(modelBuilder);
        ConfigureNotifications(modelBuilder);

        ApplyUtcDateTimeConversion(modelBuilder);
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            ApplySqliteDecimalConversion(modelBuilder);
        }
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users", t => t.HasComment("Usuarios registrados de la plataforma"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasComment("Identificador único del usuario");
            e.Property(x => x.UserName).HasMaxLength(50).IsRequired().HasComment("Nombre de usuario visible (único)");
            e.Property(x => x.Email).HasMaxLength(150).IsRequired().HasComment("Correo electrónico (único)");
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired().HasComment("Hash PBKDF2 de la contraseña");
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20).HasComment("Rol: User o Admin");
            e.Property(x => x.IsActive).HasComment("Indica si la cuenta está habilitada");
            e.Property(x => x.CreatedAt).HasComment("Fecha de registro (UTC)");
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.UserName).IsUnique();
        });
    }

    private static void ConfigureCategories(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("categories", t => t.HasComment("Categorías de artículos"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasComment("Identificador de la categoría");
            e.Property(x => x.Name).HasMaxLength(60).IsRequired().HasComment("Nombre de la categoría");
            e.HasIndex(x => x.Name).IsUnique();
            e.HasData(
                new Category { Id = 1, Name = "Electrónica" },
                new Category { Id = 2, Name = "Hogar" },
                new Category { Id = 3, Name = "Moda" },
                new Category { Id = 4, Name = "Vehículos" },
                new Category { Id = 5, Name = "Arte y colecciones" },
                new Category { Id = 6, Name = "Deportes" },
                new Category { Id = 7, Name = "Otros" });
        });
    }

    private static void ConfigureAuctions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Auction>(e =>
        {
            e.ToTable("auctions", t => t.HasComment("Artículos publicados para subasta"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasComment("Identificador de la subasta");
            e.Property(x => x.Title).HasMaxLength(120).IsRequired().HasComment("Título del artículo");
            e.Property(x => x.Description).HasMaxLength(4000).IsRequired().HasComment("Descripción del artículo");
            e.Property(x => x.CategoryId).HasComment("Categoría del artículo");
            e.Property(x => x.StartingPrice).HasPrecision(18, 2).HasComment("Precio inicial");
            e.Property(x => x.MinIncrement).HasPrecision(18, 2).HasComment("Incremento mínimo entre pujas");
            e.Property(x => x.CurrentPrice).HasPrecision(18, 2).HasComment("Precio actual (mejor puja)");
            e.Property(x => x.StartAt).HasComment("Fecha/hora de inicio (UTC)");
            e.Property(x => x.EndAt).HasComment("Fecha/hora de cierre (UTC)");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20)
                .HasComment("Estado: Scheduled, Active, Finished, Cancelled");
            e.Property(x => x.SellerId).HasComment("Usuario que publica la subasta");
            e.Property(x => x.WinnerId).HasComment("Usuario adjudicatario");
            e.Property(x => x.WinningBidId).HasComment("Puja ganadora");
            e.Property(x => x.BidCount).HasComment("Cantidad de pujas recibidas");
            e.Property(x => x.CreatedAt).HasComment("Fecha de publicación (UTC)");
            e.Property(x => x.ClosedAt).HasComment("Fecha de cierre/adjudicación (UTC)");
            e.Property(x => x.Version).IsConcurrencyToken().HasComment("Control de concurrencia optimista");
            e.HasOne(x => x.Category).WithMany(c => c.Auctions).HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Seller).WithMany(u => u.Auctions).HasForeignKey(x => x.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Winner).WithMany().HasForeignKey(x => x.WinnerId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.Status, x.EndAt });
            e.HasIndex(x => x.StartAt);
            e.HasIndex(x => x.SellerId);
        });
    }

    private static void ConfigureImages(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuctionImage>(e =>
        {
            e.ToTable("auction_images", t => t.HasComment("Imágenes de los artículos subastados"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasComment("Identificador de la imagen");
            e.Property(x => x.AuctionId).HasComment("Subasta a la que pertenece");
            e.Property(x => x.FileName).HasMaxLength(200).IsRequired().HasComment("Nombre original del archivo");
            e.Property(x => x.ContentType).HasMaxLength(50).IsRequired().HasComment("Tipo MIME de la imagen");
            e.Property(x => x.Data).IsRequired().HasComment("Contenido binario de la imagen");
            e.Property(x => x.SortOrder).HasComment("Orden de visualización");
            e.Property(x => x.CreatedAt).HasComment("Fecha de carga (UTC)");
            e.HasOne(x => x.Auction).WithMany(a => a.Images).HasForeignKey(x => x.AuctionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureBids(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bid>(e =>
        {
            e.ToTable("bids", t => t.HasComment("Ofertas (pujas) realizadas"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasComment("Identificador de la puja");
            e.Property(x => x.AuctionId).HasComment("Subasta pujada");
            e.Property(x => x.BidderId).HasComment("Usuario que puja");
            e.Property(x => x.Amount).HasPrecision(18, 2).HasComment("Monto ofertado");
            e.Property(x => x.CreatedAt).HasComment("Fecha/hora de la puja (UTC)");
            e.HasOne(x => x.Auction).WithMany(a => a.Bids).HasForeignKey(x => x.AuctionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Bidder).WithMany(u => u.Bids).HasForeignKey(x => x.BidderId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.AuctionId, x.Amount });
            e.HasIndex(x => x.BidderId);
        });
    }

    private static void ConfigureNotifications(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(e =>
        {
            e.ToTable("notifications", t => t.HasComment("Notificaciones enviadas a los usuarios"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasComment("Identificador de la notificación");
            e.Property(x => x.UserId).HasComment("Usuario destinatario");
            e.Property(x => x.AuctionId).HasComment("Subasta relacionada");
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).HasComment("Tipo de notificación");
            e.Property(x => x.Message).HasMaxLength(500).IsRequired().HasComment("Mensaje mostrado al usuario");
            e.Property(x => x.IsRead).HasComment("Indica si fue leída");
            e.Property(x => x.CreatedAt).HasComment("Fecha de creación (UTC)");
            e.HasOne(x => x.User).WithMany(u => u.Notifications).HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Auction).WithMany().HasForeignKey(x => x.AuctionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.IsRead });
        });
    }

    /// <summary>Garantiza que las fechas se materialicen como UTC en cualquier proveedor.</summary>
    private static void ApplyUtcDateTimeConversion(ModelBuilder modelBuilder)
    {
        var utc = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var nullableUtc = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
        {
            if (property.ClrType == typeof(DateTime))
            {
                property.SetValueConverter(utc);
            }
            else if (property.ClrType == typeof(DateTime?))
            {
                property.SetValueConverter(nullableUtc);
            }
        }
    }

    /// <summary>SQLite (usado en pruebas) no soporta comparaciones de decimal: se almacenan como double.</summary>
    private static void ApplySqliteDecimalConversion(ModelBuilder modelBuilder)
    {
        var converter = new ValueConverter<decimal, double>(v => (double)v, v => (decimal)v);
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal)))
        {
            property.SetValueConverter(converter);
        }
    }
}
