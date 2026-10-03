using System.Text;
using System.Text.RegularExpressions;

namespace Subasta.DocGen;

/// <summary>Diagramas de despliegue construidos a partir de Terraform (infra/), docker-compose y los workflows.</summary>
internal static class DeploymentDocs
{
    private static readonly Regex ResourcePattern = new(
        "^resource\\s+\"(?<type>[a-z0-9_]+)\"\\s+\"(?<name>[a-z0-9_]+)\"", RegexOptions.Multiline, TimeSpan.FromSeconds(1));

    private static readonly Regex ComposeServicePattern = new(
        "^  (?<name>[a-z0-9_-]+):\\s*$", RegexOptions.Multiline, TimeSpan.FromSeconds(1));

    private static readonly Dictionary<string, string> Labels = new()
    {
        ["render_postgres"] = "Render PostgreSQL (plan free)",
        ["render_web_service"] = "Render Web Service - contenedor Docker de la API .NET",
        ["render_static_site"] = "Render Static Site - frontend React (CDN)",
        ["render_env_group"] = "Grupo de variables de entorno (CORS)",
        ["render_env_group_link"] = "Vínculo del grupo de variables con la API",
        ["random_string"] = "Sufijo aleatorio para nombres únicos",
        ["random_password"] = "Contraseña generada del administrador inicial",
    };

    public static string Generate(string root)
    {
        var infraDir = Path.Combine(root, "infra");
        var workflowsDir = Path.Combine(root, ".github", "workflows");
        var composeFile = Path.Combine(root, "docker-compose.yml");

        var resources = Directory.Exists(infraDir)
            ? Directory.GetFiles(infraDir, "*.tf")
                .SelectMany(f => ResourcePattern.Matches(File.ReadAllText(f))
                    .Select(m => (Type: m.Groups["type"].Value, Name: m.Groups["name"].Value)))
                .ToList()
            : [];
        var workflows = Directory.Exists(workflowsDir)
            ? Directory.GetFiles(workflowsDir, "*.yml").Select(Path.GetFileName).OfType<string>().OrderBy(x => x).ToList()
            : [];
        var composeServices = File.Exists(composeFile) ? ComposeServices(File.ReadAllText(composeFile)) : [];
        bool Has(string type) => resources.Exists(r => r.Type == type);

        var sb = new StringBuilder(Md.Header("Diagramas de despliegue",
            $"Construido a partir de {resources.Count} recursos Terraform en `infra/`, `docker-compose.yml` " +
            $"y {workflows.Count} workflows de GitHub Actions."));

        sb.AppendLine("## 1. Producción en la nube (Render, plan gratuito)\n");
        sb.AppendLine("```mermaid");
        sb.AppendLine("flowchart TB");
        sb.AppendLine("    browser([\"Navegador del usuario\"])");
        sb.AppendLine("    subgraph RENDER[\"Render - plan gratuito\"]");
        if (Has("render_static_site")) sb.AppendLine("        web[\"Static Site<br/>Frontend React + Vite (CDN)\"]");
        if (Has("render_web_service")) sb.AppendLine("        api[\"Web Service Docker<br/>API ASP.NET Core .NET 10<br/>REST + SignalR - puerto 8080 - /health\"]");
        if (Has("render_postgres")) sb.AppendLine("        db[(\"PostgreSQL 16<br/>BD subasta\")]");
        if (Has("render_env_group")) sb.AppendLine("        env{{\"Env Group<br/>CORS\"}}");
        sb.AppendLine("    end");
        sb.AppendLine("    subgraph GH[\"GitHub\"]");
        sb.AppendLine("        repo[\"Repositorio: código + Dockerfile\"]");
        sb.AppendLine("        actions[\"GitHub Actions\"]");
        sb.AppendLine("    end");
        sb.AppendLine("    hcp[(\"HCP Terraform<br/>estado remoto\")]");
        sb.AppendLine("    browser -- \"HTTPS\" --> web");
        sb.AppendLine("    browser -- \"HTTPS REST/JSON + WSS SignalR\" --> api");
        sb.AppendLine("    api -- \"red privada de Render\" --> db");
        if (Has("render_env_group")) sb.AppendLine("    env -. \"variables\" .-> api");
        sb.AppendLine("    repo -- \"docker build backend/Dockerfile\" --> api");
        sb.AppendLine("    repo -- \"npm run build\" --> web");
        sb.AppendLine("    actions -- \"terraform apply - infra.yml\" --> RENDER");
        sb.AppendLine("    actions -. \"estado\" .-> hcp");
        sb.AppendLine("    actions -- \"API de deploys - deploy.yml\" --> api");
        sb.AppendLine("```\n");

        if (composeServices.Count > 0)
        {
            sb.AppendLine("## 2. Entorno local (docker-compose)\n");
            sb.AppendLine("```mermaid");
            sb.AppendLine("flowchart LR");
            sb.AppendLine("    dev([\"Desarrollador\"])");
            sb.AppendLine("    subgraph HOST[\"Docker en la máquina local\"]");
            foreach (var service in composeServices)
            {
                var shape = service switch
                {
                    "db" => "[(\"db: postgres:17-alpine<br/>puerto 5432\")]",
                    "api" => "[\"api: imagen backend/Dockerfile<br/>puerto 8080\"]",
                    "web" => "[\"web: nginx + React<br/>puerto 8081\"]",
                    _ => $"[\"{service}\"]"
                };
                sb.AppendLine($"        {service}{shape}");
            }

            sb.AppendLine("    end");
            sb.AppendLine("    dev --> web");
            sb.AppendLine("    dev --> api");
            sb.AppendLine("    api --> db");
            sb.AppendLine("```\n");
        }

        sb.AppendLine("## 3. Pipeline CI/CD (GitHub Actions)\n");
        sb.AppendLine("```mermaid");
        sb.AppendLine("flowchart LR");
        sb.AppendLine("    dev([\"Desarrollador\"]) -- \"git push\" --> gh[\"Repositorio GitHub\"]");
        foreach (var wf in workflows)
        {
            sb.AppendLine($"    gh --> {NodeId(wf)}[\"{wf}\"]");
        }

        if (workflows.Contains("infra.yml")) sb.AppendLine("    wf_infra -- \"terraform apply\" --> render[\"Render\"]");
        if (workflows.Contains("sonar.yml")) sb.AppendLine("    wf_sonar -- \"análisis + quality gate\" --> sonarcloud[\"SonarCloud\"]");
        if (workflows.Contains("snyk-semgrep.yml")) sb.AppendLine("    wf_snyk_semgrep -- \"SCA / SAST / imagen\" --> reports[\"Reportes SARIF / HTML\"]");
        if (workflows.Contains("deploy.yml")) sb.AppendLine("    wf_deploy -- \"pruebas + deploy API y frontend\" --> render");
        if (workflows.Contains("generase-documentation.yml")) sb.AppendLine("    wf_generase_documentation -- \"commit\" --> docs[\"docs/*.md\"]");
        sb.AppendLine("```\n");

        sb.AppendLine("## 4. Recursos Terraform\n");
        sb.AppendLine("| Tipo | Nombre lógico | Descripción |");
        sb.AppendLine("|---|---|---|");
        foreach (var (type, name) in resources)
        {
            sb.AppendLine($"| `{type}` | `{name}` | {(Labels.TryGetValue(type, out var label) ? label : "")} |");
        }

        return sb.ToString();
    }

    private static string NodeId(string workflowFile) =>
        "wf_" + Path.GetFileNameWithoutExtension(workflowFile).Replace("-", "_");

    /// <summary>Nombres de los servicios definidos bajo la clave "services:" del docker-compose.</summary>
    private static List<string> ComposeServices(string compose)
    {
        var start = compose.IndexOf("\nservices:", StringComparison.Ordinal);
        if (start < 0) return [];
        var end = compose.IndexOf("\nvolumes:", start + 1, StringComparison.Ordinal);
        var section = end < 0 ? compose[start..] : compose[start..end];
        return ComposeServicePattern.Matches(section.Replace("\r", "")).Select(m => m.Groups["name"].Value).ToList();
    }
}
