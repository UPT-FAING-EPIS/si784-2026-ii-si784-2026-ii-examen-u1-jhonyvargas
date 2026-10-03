using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Subasta.Api.Dtos;

namespace Subasta.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ApiTests
{
    private readonly SubastaApiFactory _factory;

    public ApiTests(SubastaApiFactory factory)
    {
        _factory = factory;
    }

    private static CreateAuctionRequest NewAuction(string title = "Reloj antiguo", DateTime? startAt = null) => new()
    {
        Title = title,
        Description = "Reloj de bolsillo de colección en buen estado.",
        CategoryId = 5,
        StartingPrice = 50m,
        MinIncrement = 5m,
        StartAt = startAt,
        EndAt = (startAt ?? DateTime.UtcNow).AddHours(2)
    };

    private static async Task<AuctionDetailDto> CreateAuctionAsync(HttpClient client, CreateAuctionRequest? request = null)
    {
        var response = await client.PostAsJsonAsync("/auctions", request ?? NewAuction());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuctionDetailDto>(TestJson.Options))!;
    }

    [Fact]
    public async Task Health_IsHealthy()
    {
        var response = await _factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_Login_And_Me()
    {
        var (client, auth) = await _factory.CreateUserClientAsync();

        var me = await client.GetFromJsonAsync<UserDto>("/auth/me", TestJson.Options);
        Assert.Equal(auth.User.Id, me!.Id);

        var login = await _factory.CreateClient().PostAsJsonAsync("/auth/login",
            new LoginRequest { Email = auth.User.Email, Password = "Clave-Segura-123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var badLogin = await _factory.CreateClient().PostAsJsonAsync("/auth/login",
            new LoginRequest { Email = auth.User.Email, Password = "incorrecta" });
        Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidData_Returns400()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/auth/register",
            new RegisterRequest { UserName = "x", Email = "no-email", Password = "123" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(TestJson.Options);
        Assert.True(problem!.Errors.Count >= 3);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400()
    {
        var (_, auth) = await _factory.CreateUserClientAsync();
        var response = await _factory.CreateClient().PostAsJsonAsync("/auth/register",
            new RegisterRequest { UserName = "otro_nombre_1", Email = auth.User.Email, Password = "Clave-Segura-123" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateAuction_RequiresAuthentication()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/auctions", NewAuction());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAuction_WithInvalidDates_Returns400()
    {
        var (client, _) = await _factory.CreateUserClientAsync();
        var request = NewAuction();
        request.EndAt = DateTime.UtcNow.AddMinutes(-5);

        var response = await client.PostAsJsonAsync("/auctions", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateAuction_WithUnknownCategory_Returns400()
    {
        var (client, _) = await _factory.CreateUserClientAsync();
        var request = NewAuction();
        request.CategoryId = 999;

        var response = await client.PostAsJsonAsync("/auctions", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuctionAndBidFlow_WorksEndToEnd()
    {
        var (seller, _) = await _factory.CreateUserClientAsync("seller");
        var (bidder, bidderAuth) = await _factory.CreateUserClientAsync("bidder");
        var auction = await CreateAuctionAsync(seller);
        Assert.Equal("active", auction.Status);

        // Detalle y listado públicos
        var detail = await _factory.CreateClient().GetFromJsonAsync<AuctionDetailDto>($"/auctions/{auction.Id}", TestJson.Options);
        Assert.Equal(auction.Title, detail!.Title);
        var list = await _factory.CreateClient().GetFromJsonAsync<PagedResult<AuctionSummaryDto>>("/auctions?status=active&pageSize=100", TestJson.Options);
        Assert.Contains(list!.Items, a => a.Id == auction.Id);

        // Reglas de puja
        var low = await bidder.PostAsJsonAsync("/bids", new PlaceBidRequest { AuctionId = auction.Id, Amount = 10m });
        Assert.Equal(HttpStatusCode.BadRequest, low.StatusCode);
        var own = await seller.PostAsJsonAsync("/bids", new PlaceBidRequest { AuctionId = auction.Id, Amount = 100m });
        Assert.Equal(HttpStatusCode.BadRequest, own.StatusCode);
        var ok = await bidder.PostAsJsonAsync("/bids", new PlaceBidRequest { AuctionId = auction.Id, Amount = 60m });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);

        // Historiales
        var bids = await _factory.CreateClient().GetFromJsonAsync<List<BidDto>>($"/bids?auctionId={auction.Id}", TestJson.Options);
        Assert.Equal(60m, Assert.Single(bids!).Amount);
        var userBids = await bidder.GetFromJsonAsync<List<UserBidDto>>("/user/bids", TestJson.Options);
        Assert.True(Assert.Single(userBids!).IsWinning);
        var participating = await bidder.GetFromJsonAsync<List<AuctionSummaryDto>>("/user/auctions?type=participating", TestJson.Options);
        Assert.Contains(participating!, a => a.Id == auction.Id);
        var published = await seller.GetFromJsonAsync<List<AuctionSummaryDto>>("/user/auctions", TestJson.Options);
        Assert.Contains(published!, a => a.Id == auction.Id);

        // Notificación al vendedor
        var notifications = await seller.GetFromJsonAsync<List<NotificationDto>>("/user/notifications", TestJson.Options);
        Assert.Contains(notifications!, n => n.Type == "NewBid" && n.AuctionId == auction.Id);

        // Cierre por el administrador => adjudicación
        var admin = await _factory.CreateAdminClientAsync();
        var close = await admin.PostAsync($"/admin/auctions/{auction.Id}/close", null);
        Assert.Equal(HttpStatusCode.NoContent, close.StatusCode);

        var closed = await _factory.CreateClient().GetFromJsonAsync<AuctionDetailDto>($"/auctions/{auction.Id}", TestJson.Options);
        Assert.Equal("finished", closed!.Status);
        Assert.Equal(bidderAuth.User.Id, closed.WinnerId);
        var won = await bidder.GetFromJsonAsync<List<AuctionSummaryDto>>("/user/auctions?type=won", TestJson.Options);
        Assert.Contains(won!, a => a.Id == auction.Id);
        var late = await bidder.PostAsJsonAsync("/bids", new PlaceBidRequest { AuctionId = auction.Id, Amount = 500m });
        Assert.Equal(HttpStatusCode.BadRequest, late.StatusCode);
    }

    [Fact]
    public async Task Bids_WithoutAuctionId_Returns400_AndUnknownAuction_Returns404()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/bids")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/bids?auctionId={Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/auctions/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task UpcomingAuctions_AreListedSeparately()
    {
        var (seller, _) = await _factory.CreateUserClientAsync();
        var upcoming = await CreateAuctionAsync(seller, NewAuction("Pintura al óleo", DateTime.UtcNow.AddDays(1)));
        Assert.Equal("upcoming", upcoming.Status);

        var list = await _factory.CreateClient().GetFromJsonAsync<PagedResult<AuctionSummaryDto>>("/auctions?status=upcoming&pageSize=100", TestJson.Options);
        Assert.Contains(list!.Items, a => a.Id == upcoming.Id);
        var invalid = await _factory.CreateClient().GetAsync("/auctions?status=desconocido");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task UploadImage_ValidatesContent()
    {
        var (seller, _) = await _factory.CreateUserClientAsync();
        var (other, _) = await _factory.CreateUserClientAsync();
        var auction = await CreateAuctionAsync(seller);
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

        var ok = await seller.PostAsync($"/auctions/{auction.Id}/images", Multipart(png, "image/png"));
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);

        var fake = await seller.PostAsync($"/auctions/{auction.Id}/images", Multipart("hola"u8.ToArray(), "image/png"));
        Assert.Equal(HttpStatusCode.BadRequest, fake.StatusCode);

        var forbidden = await other.PostAsync($"/auctions/{auction.Id}/images", Multipart(png, "image/png"));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var detail = await _factory.CreateClient().GetFromJsonAsync<AuctionDetailDto>($"/auctions/{auction.Id}", TestJson.Options);
        var image = await _factory.CreateClient().GetAsync(Assert.Single(detail!.ImageUrls));
        Assert.Equal("image/png", image.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task AdminEndpoints_RequireAdminRole()
    {
        var (user, _) = await _factory.CreateUserClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/admin/stats")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/admin/stats")).StatusCode);

        var admin = await _factory.CreateAdminClientAsync();
        var stats = await admin.GetFromJsonAsync<AdminStatsDto>("/admin/stats", TestJson.Options);
        Assert.True(stats!.TotalUsers >= 1);
        var users = await admin.GetFromJsonAsync<List<UserDto>>("/admin/users", TestJson.Options);
        Assert.Contains(users!, u => u.Role == "Admin");
        var all = await admin.GetAsync("/admin/auctions?status=all");
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
    }

    [Fact]
    public async Task Admin_CanDeactivateUser_WhoThenCannotLogin()
    {
        var (_, auth) = await _factory.CreateUserClientAsync();
        var admin = await _factory.CreateAdminClientAsync();

        var response = await admin.PatchAsJsonAsync($"/admin/users/{auth.User.Id}", new UpdateUserRequest { IsActive = false });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login = await _factory.CreateClient().PostAsJsonAsync("/auth/login",
            new LoginRequest { Email = auth.User.Email, Password = "Clave-Segura-123" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task WebSocketHub_PushesBidsAndNotificationsInRealTime()
    {
        var (seller, sellerAuth) = await _factory.CreateUserClientAsync("seller");
        var (bidder, _) = await _factory.CreateUserClientAsync("bidder");
        var auction = await CreateAuctionAsync(seller);

        var bidReceived = new TaskCompletionSource<decimal>(TaskCreationOptions.RunContinuationsAsynchronously);
        var notificationReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/auctions"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(sellerAuth.Token);
            })
            .Build();

        connection.On<System.Text.Json.JsonElement>("BidPlaced", e => bidReceived.TrySetResult(e.GetProperty("currentPrice").GetDecimal()));
        connection.On<NotificationDto>("Notification", n => notificationReceived.TrySetResult(n.Type));

        await connection.StartAsync();
        await connection.InvokeAsync("JoinAuction", auction.Id);

        var response = await bidder.PostAsJsonAsync("/bids", new PlaceBidRequest { AuctionId = auction.Id, Amount = 75m });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        Assert.Equal(75m, await bidReceived.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal("NewBid", await notificationReceived.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    private static MultipartFormDataContent Multipart(byte[] data, string contentType)
    {
        var file = new ByteArrayContent(data);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", "foto.png" } };
    }
}
