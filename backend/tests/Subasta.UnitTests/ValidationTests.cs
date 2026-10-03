using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Subasta.Api.Domain;
using Subasta.Api.Dtos;
using Subasta.Api.Services;

namespace Subasta.UnitTests;

public class ValidationTests
{
    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    private static CreateAuctionRequest ValidAuction() => new()
    {
        Title = "Cámara réflex",
        Description = "Cámara en excelente estado, con lente 18-55.",
        CategoryId = 1,
        StartingPrice = 250m,
        MinIncrement = 10m,
        EndAt = DateTime.UtcNow.AddDays(2)
    };

    [Fact]
    public void ValidAuction_HasNoErrors()
    {
        Assert.Empty(Validate(ValidAuction()));
    }

    [Fact]
    public void Auction_WithEndBeforeStart_IsInvalid()
    {
        var request = ValidAuction();
        request.StartAt = DateTime.UtcNow.AddDays(3);
        request.EndAt = DateTime.UtcNow.AddDays(2);

        Assert.Contains(Validate(request), r => r.MemberNames.Contains(nameof(CreateAuctionRequest.EndAt)));
    }

    [Fact]
    public void Auction_WithPastStart_IsInvalid()
    {
        var request = ValidAuction();
        request.StartAt = DateTime.UtcNow.AddHours(-2);

        Assert.Contains(Validate(request), r => r.MemberNames.Contains(nameof(CreateAuctionRequest.StartAt)));
    }

    [Fact]
    public void Auction_LongerThan90Days_IsInvalid()
    {
        var request = ValidAuction();
        request.EndAt = DateTime.UtcNow.AddDays(120);

        Assert.NotEmpty(Validate(request));
    }

    [Theory]
    [InlineData("", "Descripción válida del artículo", 10)]
    [InlineData("Título válido", "corta", 10)]
    [InlineData("Título válido", "Descripción válida del artículo", 0)]
    public void Auction_RequiredFields_AreValidated(string title, string description, decimal price)
    {
        var request = ValidAuction();
        request.Title = title;
        request.Description = description;
        request.StartingPrice = price;

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void Auction_WithMoreThanTwoDecimals_IsInvalid()
    {
        var request = ValidAuction();
        request.StartingPrice = 10.123m;

        Assert.NotEmpty(Validate(request));
    }

    [Theory]
    [InlineData("ab", "a@b.com", "password1")]
    [InlineData("usuario con espacios", "a@b.com", "password1")]
    [InlineData("usuario", "no-es-correo", "password1")]
    [InlineData("usuario", "a@b.com", "corta")]
    public void Register_InvalidData_IsRejected(string userName, string email, string password)
    {
        var request = new RegisterRequest { UserName = userName, Email = email, Password = password };

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void PlaceBid_RequiresAuctionAndPositiveAmount()
    {
        Assert.Equal(2, Validate(new PlaceBidRequest { AuctionId = null, Amount = 0 }).Count);
    }

    [Fact]
    public void ToUtc_TreatsUnspecifiedAsUtc()
    {
        var value = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Unspecified);

        var utc = AuctionService.ToUtc(value);

        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(value.Ticks, utc.Ticks);
    }

    [Fact]
    public async Task Image_WithValidPngSignature_IsAccepted()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];
        var data = await AuctionService.ReadAndValidateImageAsync(FormFile(png, "image/png"), CancellationToken.None);

        Assert.Equal(png, data);
    }

    [Fact]
    public async Task Image_WithSpoofedContentType_IsRejected()
    {
        byte[] notAnImage = "<script>alert(1)</script>"u8.ToArray();

        await Assert.ThrowsAsync<DomainException>(() =>
            AuctionService.ReadAndValidateImageAsync(FormFile(notAnImage, "image/png"), CancellationToken.None));
    }

    [Fact]
    public async Task Image_WithUnsupportedType_IsRejected()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            AuctionService.ReadAndValidateImageAsync(FormFile([1, 2, 3], "application/pdf"), CancellationToken.None));
    }

    [Fact]
    public void ConnectionString_InUrlFormat_IsConvertedToNpgsql()
    {
        var result = Subasta.Api.Infrastructure.ConnectionStrings.Normalize(
            "postgresql://subasta:p%40ss@dpg-abc123-a.oregon-postgres.render.com:6543/subasta?sslmode=require");

        var builder = new Npgsql.NpgsqlConnectionStringBuilder(result);
        Assert.Equal("dpg-abc123-a.oregon-postgres.render.com", builder.Host);
        Assert.Equal(6543, builder.Port);
        Assert.Equal("subasta", builder.Database);
        Assert.Equal("subasta", builder.Username);
        Assert.Equal("p@ss", builder.Password);
        Assert.Equal(Npgsql.SslMode.Require, builder.SslMode);
    }

    [Fact]
    public void ConnectionString_WithoutPort_UsesDefault_AndKeyValueIsUnchanged()
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(
            Subasta.Api.Infrastructure.ConnectionStrings.Normalize("postgres://u:p@dpg-internal-a/db"));
        Assert.Equal(5432, builder.Port);
        Assert.Equal("Host=x;Database=y", Subasta.Api.Infrastructure.ConnectionStrings.Normalize("Host=x;Database=y"));
    }

    private static FormFile FormFile(byte[] content, string contentType)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", "archivo.bin")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
