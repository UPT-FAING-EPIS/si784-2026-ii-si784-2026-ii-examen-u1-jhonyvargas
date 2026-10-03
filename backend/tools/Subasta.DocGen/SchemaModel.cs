using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using Subasta.Api.Data;

namespace Subasta.DocGen;

internal sealed record ColumnInfo(string Name, string Type, bool Nullable, string? Default, string? Comment, bool IsPrimaryKey);

internal sealed record ForeignKeyInfo(string Name, IReadOnlyList<string> Columns, string PrincipalTable, IReadOnlyList<string> PrincipalColumns, string OnDelete);

internal sealed record IndexInfo(string Name, IReadOnlyList<string> Columns, bool Unique);

internal sealed record TableInfo(string Name, string? Comment, List<ColumnInfo> Columns, List<ForeignKeyInfo> ForeignKeys, List<IndexInfo> Indexes);

internal sealed record SchemaInfo(string Source, List<TableInfo> Tables);

/// <summary>Obtiene la estructura de la base de datos desde PostgreSQL real o, en su defecto, desde el modelo EF Core.</summary>
internal static class SchemaReader
{
    public static SchemaInfo FromEfModel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost").Options;
        using var db = new AppDbContext(options);
        var model = db.GetService<IDesignTimeModel>().Model.GetRelationalModel();

        var tables = model.Tables
            .OrderBy(t => t.Name)
            .Select(t => new TableInfo(
                t.Name,
                t.Comment,
                t.Columns.OrderBy(c => c.Order ?? int.MaxValue).ThenBy(c => ColumnOrder(t, c)).Select(c => new ColumnInfo(
                    c.Name, c.StoreType, c.IsNullable, c.DefaultValueSql ?? c.DefaultValue?.ToString(), c.Comment,
                    t.PrimaryKey?.Columns.Contains(c) == true)).ToList(),
                t.ForeignKeyConstraints.OrderBy(f => f.Name).Select(f => new ForeignKeyInfo(
                    f.Name, f.Columns.Select(c => c.Name).ToList(), f.PrincipalTable.Name,
                    f.PrincipalColumns.Select(c => c.Name).ToList(), f.OnDeleteAction.ToString().ToUpperInvariant())).ToList(),
                t.Indexes.OrderBy(i => i.Name).Select(i => new IndexInfo(i.Name, i.Columns.Select(c => c.Name).ToList(), i.IsUnique)).ToList()))
            .ToList();

        return new SchemaInfo("Modelo EF Core (Npgsql)", tables);
    }

    private static int ColumnOrder(ITable table, IColumn column)
    {
        // Respeta el orden de declaración de las propiedades de la entidad
        var mapping = table.EntityTypeMappings.First().TypeBase;
        var names = mapping.GetProperties().Select(p => p.GetColumnName()).ToList();
        var index = names.IndexOf(column.Name);
        return index < 0 ? int.MaxValue : index;
    }

    public static async Task<SchemaInfo> FromPostgresAsync(string connectionString)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var tables = new Dictionary<string, TableInfo>();

        const string tablesSql = """
            SELECT c.relname, obj_description(c.oid, 'pg_class')
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relkind = 'r' AND c.relname <> '__EFMigrationsHistory'
            ORDER BY c.relname
            """;
        await using (var cmd = new NpgsqlCommand(tablesSql, conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                tables[name] = new TableInfo(name, reader.IsDBNull(1) ? null : reader.GetString(1), new(), new(), new());
            }
        }

        var primaryKeys = new HashSet<(string, string)>();
        const string pkSql = """
            SELECT tc.table_name, kcu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
              ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
            WHERE tc.table_schema = 'public' AND tc.constraint_type = 'PRIMARY KEY'
            """;
        await using (var cmd = new NpgsqlCommand(pkSql, conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                primaryKeys.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        const string columnsSql = """
            SELECT c.relname, a.attname, format_type(a.atttypid, a.atttypmod), NOT a.attnotnull,
                   pg_get_expr(d.adbin, d.adrelid), col_description(c.oid, a.attnum)
            FROM pg_attribute a
            JOIN pg_class c ON c.oid = a.attrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
            WHERE n.nspname = 'public' AND c.relkind = 'r' AND a.attnum > 0 AND NOT a.attisdropped
            ORDER BY c.relname, a.attnum
            """;
        await using (var cmd = new NpgsqlCommand(columnsSql, conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                if (!tables.TryGetValue(reader.GetString(0), out var table))
                {
                    continue;
                }

                var column = reader.GetString(1);
                table.Columns.Add(new ColumnInfo(column, reader.GetString(2), reader.GetBoolean(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5),
                    primaryKeys.Contains((table.Name, column))));
            }
        }

        const string fkSql = """
            SELECT con.conname, src.relname, tgt.relname,
                   ARRAY(SELECT attname FROM pg_attribute WHERE attrelid = con.conrelid AND attnum = ANY(con.conkey) ORDER BY attnum),
                   ARRAY(SELECT attname FROM pg_attribute WHERE attrelid = con.confrelid AND attnum = ANY(con.confkey) ORDER BY attnum),
                   CASE con.confdeltype WHEN 'c' THEN 'CASCADE' WHEN 'r' THEN 'RESTRICT' WHEN 'n' THEN 'SET NULL' WHEN 'd' THEN 'SET DEFAULT' ELSE 'NO ACTION' END
            FROM pg_constraint con
            JOIN pg_class src ON src.oid = con.conrelid
            JOIN pg_class tgt ON tgt.oid = con.confrelid
            JOIN pg_namespace n ON n.oid = src.relnamespace
            WHERE con.contype = 'f' AND n.nspname = 'public'
            ORDER BY con.conname
            """;
        await using (var cmd = new NpgsqlCommand(fkSql, conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                if (tables.TryGetValue(reader.GetString(1), out var table))
                {
                    table.ForeignKeys.Add(new ForeignKeyInfo(reader.GetString(0), reader.GetFieldValue<string[]>(3),
                        reader.GetString(2), reader.GetFieldValue<string[]>(4), reader.GetString(5)));
                }
            }
        }

        const string indexSql = """
            SELECT t.relname, i.relname, ix.indisunique,
                   ARRAY(SELECT a.attname FROM unnest(ix.indkey) WITH ORDINALITY k(attnum, ord)
                         JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum ORDER BY k.ord)
            FROM pg_index ix
            JOIN pg_class t ON t.oid = ix.indrelid
            JOIN pg_class i ON i.oid = ix.indexrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = 'public' AND NOT ix.indisprimary
            ORDER BY t.relname, i.relname
            """;
        await using (var cmd = new NpgsqlCommand(indexSql, conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                if (tables.TryGetValue(reader.GetString(0), out var table))
                {
                    table.Indexes.Add(new IndexInfo(reader.GetString(1), reader.GetFieldValue<string[]>(3), reader.GetBoolean(2)));
                }
            }
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        return new SchemaInfo($"Base de datos PostgreSQL `{builder.Database}` (esquema public)", tables.Values.ToList());
    }
}
