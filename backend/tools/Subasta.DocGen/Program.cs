using System.Text;
using Microsoft.EntityFrameworkCore;
using Subasta.Api.Data;
using Subasta.DocGen;

// Uso: dotnet run --project tools/Subasta.DocGen -- --output ../docs --root .. [--connection "Host=...;Database=..."]
var options = ParseArgs(args);
var root = Path.GetFullPath(options.GetValueOrDefault("root", Path.Combine("..")));
var output = Path.GetFullPath(options.GetValueOrDefault("output", Path.Combine(root, "docs")));
var connection = options.GetValueOrDefault("connection") ?? Environment.GetEnvironmentVariable("DOCGEN_CONNECTION");
Directory.CreateDirectory(output);

SchemaInfo schema;
if (!string.IsNullOrWhiteSpace(connection))
{
    Console.WriteLine("Aplicando migraciones en PostgreSQL…");
    await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options))
    {
        await db.Database.MigrateAsync();
    }

    Console.WriteLine("Leyendo el esquema desde PostgreSQL…");
    schema = await SchemaReader.FromPostgresAsync(connection);
}
else
{
    Console.WriteLine("Sin conexión: se usa el modelo EF Core como fuente del esquema.");
    schema = SchemaReader.FromEfModel();
}

var files = new Dictionary<string, string>
{
    ["diccionario-datos.md"] = DatabaseDocs.DataDictionary(schema),
    ["diagrama-entidad-relacion.md"] = DatabaseDocs.EntityRelationship(schema),
    ["diagrama-clases.md"] = ClassDocs.Generate(),
    ["diagrama-componentes.md"] = ComponentDocs.Generate(Path.Combine(root, "frontend", "src")),
    ["diagrama-despliegue.md"] = DeploymentDocs.Generate(root),
};

var index = new StringBuilder(Md.Header("Documentación técnica", "Índice de la documentación generada."));
index.AppendLine("| Documento | Contenido |");
index.AppendLine("|---|---|");
index.AppendLine("| [Diccionario de datos](diccionario-datos.md) | Tablas, columnas, tipos, llaves e índices |");
index.AppendLine("| [Diagrama entidad-relación](diagrama-entidad-relacion.md) | Modelo relacional (Mermaid `erDiagram`) |");
index.AppendLine("| [Diagrama de clases](diagrama-clases.md) | Dominio y capa de aplicación (Mermaid `classDiagram`) |");
index.AppendLine("| [Diagrama de componentes](diagrama-componentes.md) | Frontend, API, hub y base de datos + endpoints |");
index.AppendLine("| [Diagramas de despliegue](diagrama-despliegue.md) | Render (nube), docker-compose (local) y pipeline CI/CD |");
files["README.md"] = index.ToString();

foreach (var (name, content) in files)
{
    var path = Path.Combine(output, name);
    await File.WriteAllTextAsync(path, content.Replace("\r\n", "\n"), new UTF8Encoding(false));
    Console.WriteLine($"  ✔ {Path.GetRelativePath(root, path)}");
}

Console.WriteLine($"Documentación generada: {schema.Tables.Count} tablas.");
return 0;

static Dictionary<string, string> ParseArgs(string[] args)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].StartsWith("--", StringComparison.Ordinal))
        {
            result[args[i][2..]] = args[++i];
        }
    }

    return result;
}
