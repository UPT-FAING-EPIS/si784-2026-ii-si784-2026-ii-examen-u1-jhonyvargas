using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Subasta.Api.Controllers;

namespace Subasta.DocGen;

internal static class Md
{
    public static string Escape(string? text) =>
        string.IsNullOrEmpty(text) ? "" : text.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

    public static string MermaidText(string? text) =>
        string.IsNullOrEmpty(text) ? "" : text.Replace("\"", "'").Replace("\n", " ");

    public static string Header(string title, string description) =>
        $"# {title}\n\n> {description}\n>\n> Documento generado automáticamente por `generase-documentation.yml` " +
        $"(Subasta.DocGen) el {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC. No editar manualmente.\n\n";
}

/// <summary>Diccionario de datos y diagrama entidad-relación.</summary>
internal static class DatabaseDocs
{
    public static string DataDictionary(SchemaInfo schema)
    {
        var sb = new StringBuilder(Md.Header("Diccionario de datos",
            $"Fuente: {schema.Source}. Tablas: {schema.Tables.Count}."));

        sb.AppendLine("## Resumen de tablas\n");
        sb.AppendLine("| Tabla | Descripción | Columnas |");
        sb.AppendLine("|---|---|---|");
        foreach (var t in schema.Tables)
        {
            sb.AppendLine($"| [`{t.Name}`](#{t.Name.Replace("_", "_")}) | {Md.Escape(t.Comment)} | {t.Columns.Count} |");
        }

        foreach (var t in schema.Tables)
        {
            sb.AppendLine($"\n## {t.Name}\n");
            if (!string.IsNullOrWhiteSpace(t.Comment))
            {
                sb.AppendLine($"{t.Comment}\n");
            }

            sb.AppendLine("| # | Columna | Tipo de dato | Nulo | PK | FK | Valor por defecto | Descripción |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            var i = 1;
            foreach (var c in t.Columns)
            {
                var fk = t.ForeignKeys.FirstOrDefault(f => f.Columns.Contains(c.Name));
                var fkText = fk is null ? "" : $"→ `{fk.PrincipalTable}.{string.Join(",", fk.PrincipalColumns)}`";
                sb.AppendLine($"| {i++} | `{c.Name}` | {Md.Escape(c.Type)} | {(c.Nullable ? "Sí" : "No")} | " +
                              $"{(c.IsPrimaryKey ? "✔" : "")} | {fkText} | {Md.Escape(c.Default)} | {Md.Escape(c.Comment)} |");
            }

            if (t.ForeignKeys.Count > 0)
            {
                sb.AppendLine("\n**Llaves foráneas**\n");
                sb.AppendLine("| Restricción | Columnas | Referencia | ON DELETE |");
                sb.AppendLine("|---|---|---|---|");
                foreach (var f in t.ForeignKeys)
                {
                    sb.AppendLine($"| `{f.Name}` | {string.Join(", ", f.Columns)} | `{f.PrincipalTable}`({string.Join(", ", f.PrincipalColumns)}) | {f.OnDelete} |");
                }
            }

            if (t.Indexes.Count > 0)
            {
                sb.AppendLine("\n**Índices**\n");
                sb.AppendLine("| Índice | Columnas | Único |");
                sb.AppendLine("|---|---|---|");
                foreach (var ix in t.Indexes)
                {
                    sb.AppendLine($"| `{ix.Name}` | {string.Join(", ", ix.Columns)} | {(ix.Unique ? "Sí" : "No")} |");
                }
            }
        }

        return sb.ToString();
    }

    public static string EntityRelationship(SchemaInfo schema)
    {
        var sb = new StringBuilder(Md.Header("Diagrama entidad-relación", $"Fuente: {schema.Source}."));
        sb.AppendLine("```mermaid");
        sb.AppendLine("erDiagram");

        foreach (var t in schema.Tables)
        {
            foreach (var fk in t.ForeignKeys)
            {
                var columns = t.Columns.Where(c => fk.Columns.Contains(c.Name)).ToList();
                var optional = columns.Any(c => c.Nullable);
                var unique = t.Indexes.Any(ix => ix.Unique && ix.Columns.SequenceEqual(fk.Columns));
                var left = optional ? "|o" : "||";
                var right = unique ? "o|" : "o{";
                sb.AppendLine($"    {fk.PrincipalTable} {left}--{right} {t.Name} : \"{string.Join(",", fk.Columns)}\"");
            }
        }

        foreach (var t in schema.Tables)
        {
            sb.AppendLine($"    {t.Name} {{");
            foreach (var c in t.Columns)
            {
                var keys = new List<string>();
                if (c.IsPrimaryKey) keys.Add("PK");
                if (t.ForeignKeys.Any(f => f.Columns.Contains(c.Name))) keys.Add("FK");
                if (!c.IsPrimaryKey && t.Indexes.Any(ix => ix.Unique && ix.Columns.Count == 1 && ix.Columns[0] == c.Name)) keys.Add("UK");
                var keyText = keys.Count > 0 ? " " + string.Join(", ", keys) : "";
                var comment = string.IsNullOrWhiteSpace(c.Comment) ? "" : $" \"{Md.MermaidText(c.Comment)}\"";
                sb.AppendLine($"        {MermaidType(c.Type)} {c.Name}{keyText}{comment}");
            }

            sb.AppendLine("    }");
        }

        sb.AppendLine("```");
        return sb.ToString();
    }

    private static string MermaidType(string type)
    {
        var cleaned = Regex.Replace(type, "[^A-Za-z0-9]+", "_", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim('_');
        return string.IsNullOrEmpty(cleaned) ? "unknown" : cleaned;
    }
}

/// <summary>Diagramas de clases generados por reflexión sobre el ensamblado de la API.</summary>
internal static class ClassDocs
{
    private static readonly Assembly Api = typeof(AuctionsController).Assembly;

    private static IEnumerable<Type> TypesIn(string ns) =>
        Api.GetTypes().Where(t => t.Namespace == ns && t.IsPublic && !t.IsNested && !IsCompilerGenerated(t)).OrderBy(t => t.Name);

    private static bool IsCompilerGenerated(Type t) => t.Name.Contains('<') || t.Name.StartsWith("__", StringComparison.Ordinal);

    public static string Generate()
    {
        var sb = new StringBuilder(Md.Header("Diagrama de clases",
            "Generado por reflexión a partir del ensamblado Subasta.Api (dominio, servicios, controladores y hub)."));

        sb.AppendLine("## 1. Modelo de dominio\n");
        sb.AppendLine("```mermaid");
        sb.AppendLine("classDiagram");
        sb.AppendLine("    direction LR");
        var domain = TypesIn("Subasta.Api.Domain").ToList();
        foreach (var type in domain)
        {
            AppendClass(sb, type, includeMethods: true);
        }

        var entityNames = domain.Where(t => t.IsClass && !typeof(Exception).IsAssignableFrom(t)).Select(t => t.Name).ToHashSet();
        foreach (var type in domain.Where(t => entityNames.Contains(t.Name)))
        {
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var element = ElementType(p.PropertyType);
                if (element is not null && entityNames.Contains(element.Name))
                {
                    sb.AppendLine($"    {type.Name} \"1\" --> \"*\" {element.Name} : {p.Name}");
                }
                else if (entityNames.Contains(p.PropertyType.Name))
                {
                    sb.AppendLine($"    {type.Name} --> \"0..1\" {p.PropertyType.Name} : {p.Name}");
                }
                else if ((Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType) is { IsEnum: true } enumType)
                {
                    sb.AppendLine($"    {type.Name} ..> {enumType.Name}");
                }
            }
        }

        sb.AppendLine("```\n");

        sb.AppendLine("## 2. Capa de aplicación (controladores, servicios, hub y persistencia)\n");
        sb.AppendLine("```mermaid");
        sb.AppendLine("classDiagram");
        sb.AppendLine("    direction TB");
        var services = TypesIn("Subasta.Api.Services").Where(t => t.IsInterface || t.Name.EndsWith("Service", StringComparison.Ordinal)).ToList();
        var controllers = TypesIn("Subasta.Api.Controllers").ToList();
        var hubs = TypesIn("Subasta.Api.Hubs").ToList();
        var data = TypesIn("Subasta.Api.Data").Where(t => typeof(DbContext).IsAssignableFrom(t)).ToList();
        var all = services.Concat(controllers).Concat(hubs).Concat(data).ToList();

        foreach (var type in all)
        {
            AppendClass(sb, type, includeMethods: true, includeProperties: type.IsInterface || data.Contains(type));
        }

        var known = all.Select(t => t.Name).ToHashSet();
        foreach (var type in all.Where(t => t.IsClass))
        {
            foreach (var itf in type.GetInterfaces().Where(i => known.Contains(i.Name)))
            {
                sb.AppendLine($"    {itf.Name} <|.. {type.Name}");
            }

            if (type.BaseType is { } baseType && (baseType == typeof(ControllerBase) || baseType == typeof(Hub)))
            {
                sb.AppendLine($"    {baseType.Name} <|-- {type.Name}");
            }

            var ctor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
            foreach (var dep in ctor?.GetParameters().Select(p => p.ParameterType.Name).Distinct() ?? [])
            {
                if (known.Contains(dep))
                {
                    sb.AppendLine($"    {type.Name} ..> {dep} : usa");
                }
            }
        }

        sb.AppendLine("```");
        return sb.ToString();
    }

    private static void AppendClass(StringBuilder sb, Type type, bool includeMethods, bool includeProperties = true)
    {
        sb.AppendLine($"    class {type.Name} {{");
        if (type.IsInterface) sb.AppendLine("        <<interface>>");
        else if (type.IsEnum) sb.AppendLine("        <<enumeration>>");
        else if (type.IsAbstract && type.IsSealed) sb.AppendLine("        <<static>>");

        if (type.IsEnum)
        {
            foreach (var name in Enum.GetNames(type)) sb.AppendLine($"        {name}");
            sb.AppendLine("    }");
            return;
        }

        if (includeProperties)
        {
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                sb.AppendLine($"        +{TypeName(p.PropertyType)} {p.Name}");
            }
        }

        if (includeMethods)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.DeclaringType == type && m.Name is not ("Equals" or "GetHashCode" or "ToString" or "Deconstruct" or "<Clone>$"));
            foreach (var m in methods)
            {
                var parameters = string.Join(", ", m.GetParameters().Select(p => $"{TypeName(p.ParameterType)} {p.Name}"));
                sb.AppendLine($"        +{m.Name}({parameters}) {TypeName(m.ReturnType)}{(m.IsStatic ? "$" : "")}");
            }
        }

        sb.AppendLine("    }");
    }

    private static Type? ElementType(Type type) =>
        type.IsGenericType && type != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(type)
            ? type.GetGenericArguments()[0]
            : null;

    internal static string TypeName(Type type)
    {
        if (type.IsByRef) return TypeName(type.GetElementType()!);
        if (Nullable.GetUnderlyingType(type) is { } underlying) return TypeName(underlying) + "?";
        if (type == typeof(void)) return "void";
        if (type.IsArray) return TypeName(type.GetElementType()!) + "[]";
        if (!type.IsGenericType) return Alias(type);
        var name = type.Name[..type.Name.IndexOf('`')];
        // Mermaid no admite comas dentro de los genéricos
        return $"{name}~{string.Join("_", type.GetGenericArguments().Select(TypeName))}~";
    }

    private static string Alias(Type type) => type switch
    {
        _ when type == typeof(string) => "string",
        _ when type == typeof(int) => "int",
        _ when type == typeof(long) => "long",
        _ when type == typeof(bool) => "bool",
        _ when type == typeof(decimal) => "decimal",
        _ when type == typeof(double) => "double",
        _ when type == typeof(byte) => "byte",
        _ when type == typeof(object) => "object",
        _ => type.Name
    };
}

/// <summary>Diagrama de componentes: frontend, API y base de datos.</summary>
internal static class ComponentDocs
{
    private static readonly Assembly Api = typeof(AuctionsController).Assembly;

    public static string Generate(string frontendSrc)
    {
        var controllers = Api.GetTypes().Where(t => t.IsPublic && typeof(ControllerBase).IsAssignableFrom(t)).OrderBy(t => t.Name).ToList();
        var services = Api.GetTypes().Where(t => t.IsPublic && t.IsInterface && t.Namespace == "Subasta.Api.Services").OrderBy(t => t.Name).ToList();
        var background = Api.GetTypes().Where(t => t.IsPublic && typeof(Microsoft.Extensions.Hosting.BackgroundService).IsAssignableFrom(t)).ToList();
        var pages = FrontendFiles(frontendSrc, "pages");
        var components = FrontendFiles(frontendSrc, "components");
        var contexts = FrontendFiles(frontendSrc, "context");

        var sb = new StringBuilder(Md.Header("Diagrama de componentes",
            "Componentes del frontend (React), de la API (.NET) y de la persistencia, con sus dependencias."));
        sb.AppendLine("```mermaid");
        sb.AppendLine("flowchart LR");
        sb.AppendLine("    user((Usuario / Administrador))");
        sb.AppendLine("    subgraph FE[\"Frontend SPA - React + Vite\"]");
        sb.AppendLine("        direction TB");
        foreach (var p in pages) sb.AppendLine($"        page_{p}[\"Página: {p}\"]");
        foreach (var c in components) sb.AppendLine($"        comp_{c}[\"Componente: {c}\"]");
        foreach (var c in contexts) sb.AppendLine($"        ctx_{c}[\"Contexto: {c}\"]");
        sb.AppendLine("        apiClient[[\"api/client.ts - Cliente REST\"]]");
        sb.AppendLine("        rtClient[[\"api/realtime.ts - Cliente SignalR\"]]");
        sb.AppendLine("    end");

        sb.AppendLine("    subgraph BE[\"Backend API - ASP.NET Core (contenedor)\"]");
        sb.AppendLine("        direction TB");
        foreach (var c in controllers) sb.AppendLine($"        {c.Name}[\"{c.Name}<br/>/{RoutePrefix(c)}\"]");
        sb.AppendLine("        AuctionHub{{\"AuctionHub - WebSocket /hubs/auctions\"}}");
        foreach (var s in services) sb.AppendLine($"        {s.Name}([\"{s.Name}\"])");
        foreach (var b in background) sb.AppendLine($"        {b.Name}[/\"{b.Name} - proceso en segundo plano\"/]");
        sb.AppendLine("        AppDbContext[(\"AppDbContext - EF Core\")]");
        sb.AppendLine("    end");
        sb.AppendLine("    db[(\"PostgreSQL\")]");

        sb.AppendLine("    user --> FE");
        foreach (var p in pages) sb.AppendLine($"    page_{p} --> apiClient");
        sb.AppendLine("    ctx_NotificationContext --> rtClient");
        if (pages.Contains("AuctionDetail")) sb.AppendLine("    page_AuctionDetail --> rtClient");
        sb.AppendLine("    apiClient -- \"HTTPS REST/JSON + JWT\" --> BE");
        sb.AppendLine("    rtClient -- \"WSS SignalR\" --> AuctionHub");

        var serviceNames = services.Select(s => s.Name).ToHashSet();
        foreach (var c in controllers)
        {
            foreach (var dep in Dependencies(c).Where(serviceNames.Contains)) sb.AppendLine($"    {c.Name} --> {dep}");
        }

        foreach (var impl in Api.GetTypes().Where(t => t.IsClass && t.IsPublic && t.Namespace == "Subasta.Api.Services"))
        {
            var itf = impl.GetInterfaces().FirstOrDefault(i => serviceNames.Contains(i.Name));
            if (itf is null) continue;
            foreach (var dep in Dependencies(impl))
            {
                if (serviceNames.Contains(dep)) sb.AppendLine($"    {itf.Name} --> {dep}");
                else if (dep == "AppDbContext") sb.AppendLine($"    {itf.Name} --> AppDbContext");
                else if (dep.StartsWith("IHubContext", StringComparison.Ordinal)) sb.AppendLine($"    {itf.Name} -. \"push\" .-> AuctionHub");
            }
        }

        foreach (var b in background) sb.AppendLine($"    {b.Name} --> IAuctionLifecycleService");
        sb.AppendLine("    AppDbContext -- \"Npgsql / TLS\" --> db");
        sb.AppendLine("```\n");

        sb.AppendLine("## Endpoints expuestos\n");
        sb.AppendLine("| Método | Ruta | Controlador | Acción | Autorización |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var c in controllers)
        {
            var prefix = RoutePrefix(c);
            var classAuth = c.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().FirstOrDefault();
            foreach (var m in c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                foreach (var http in m.GetCustomAttributes<HttpMethodAttribute>())
                {
                    var template = http.Template ?? "";
                    var route = template.StartsWith('/') ? template : "/" + string.Join("/", new[] { prefix, template }.Where(x => x.Length > 0));
                    var auth = m.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>() ?? classAuth;
                    var authText = auth is null ? "Pública" : string.IsNullOrEmpty(auth.Roles) ? "JWT" : $"JWT ({auth.Roles})";
                    sb.AppendLine($"| {string.Join(",", http.HttpMethods)} | `{route}` | {c.Name} | {m.Name} | {authText} |");
                }
            }
        }

        sb.AppendLine("| WS | `/hubs/auctions` | AuctionHub | JoinAuction / LeaveAuction | Opcional (JWT) |");
        sb.AppendLine("\n**Eventos en tiempo real (servidor → cliente):** `BidPlaced`, `AuctionClosed`, `AuctionStarted`, `AuctionCancelled`, `Notification`.");
        return sb.ToString();
    }

    private static IEnumerable<string> Dependencies(Type type) =>
        type.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType.Name).Distinct();

    private static string RoutePrefix(Type controller) => controller.GetCustomAttribute<RouteAttribute>()?.Template ?? "";

    private static List<string> FrontendFiles(string frontendSrc, string folder)
    {
        var dir = Path.Combine(frontendSrc, folder);
        return Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.tsx").Select(Path.GetFileNameWithoutExtension).OfType<string>().OrderBy(x => x).ToList()
            : [];
    }
}
