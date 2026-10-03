using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Subasta.Api.Data;
using Subasta.Api.Hubs;

namespace Subasta.UnitTests;

public sealed class FakeTimeProvider : TimeProvider
{
    public FakeTimeProvider(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; set; }

    public override DateTimeOffset GetUtcNow() => new(UtcNow, TimeSpan.Zero);

    public void Advance(TimeSpan span) => UtcNow = UtcNow.Add(span);
}

/// <summary>Registra los mensajes enviados por SignalR para poder verificarlos.</summary>
public sealed class RecordingHubContext : IHubContext<AuctionHub>
{
    public RecordingHubContext()
    {
        Clients = new RecordingClients(this);
        Groups = new NoopGroups();
    }

    public List<(string Target, string Method, object?[] Args)> Sent { get; } = new();

    public IHubClients Clients { get; }
    public IGroupManager Groups { get; }

    private sealed class RecordingClients : IHubClients
    {
        private readonly RecordingHubContext _owner;

        public RecordingClients(RecordingHubContext owner) => _owner = owner;

        public IClientProxy All => Proxy("all");
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy("all");
        public IClientProxy Client(string connectionId) => Proxy(connectionId);
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy("clients");
        public IClientProxy Group(string groupName) => Proxy(groupName);
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy(groupName);
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy("groups");
        public IClientProxy User(string userId) => Proxy(userId);
        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy("users");

        private RecordingProxy Proxy(string target) => new(_owner, target);
    }

    private sealed class NoopGroups : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingProxy : IClientProxy
    {
        private readonly RecordingHubContext _owner;
        private readonly string _target;

        public RecordingProxy(RecordingHubContext owner, string target)
        {
            _owner = owner;
            _target = target;
        }

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            _owner.Sent.Add((_target, method, args));
            return Task.CompletedTask;
        }
    }
}

/// <summary>Base de datos SQLite en memoria, aislada por prueba.</summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
