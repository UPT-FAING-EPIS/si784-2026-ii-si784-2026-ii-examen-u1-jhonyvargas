using Npgsql;

namespace Subasta.Api.Infrastructure;

public static class ConnectionStrings
{
    /// <summary>
    /// Acepta cadenas en formato Npgsql (Host=...;Database=...) o en formato URL
    /// (postgresql://usuario:clave@host:puerto/bd?sslmode=require), como las que entrega Render.
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var uri = new Uri(trimmed);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null
        };

        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        var sslMode = query.Select(p => p.Split('=', 2))
            .FirstOrDefault(p => p.Length == 2 && p[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase))?[1];
        if (sslMode is not null && Enum.TryParse<SslMode>(sslMode.Replace("-", ""), ignoreCase: true, out var mode))
        {
            builder.SslMode = mode;
        }

        return builder.ConnectionString;
    }
}
