# Diagramas de despliegue

> Construido a partir de 7 recursos Terraform en `infra/`, `docker-compose.yml` y 5 workflows de GitHub Actions.
>
> Documento generado automáticamente por `generase-documentation.yml` (Subasta.DocGen) el 2026-10-03 02:26 UTC. No editar manualmente.

## 1. Producción en la nube (Render, plan gratuito)

```mermaid
flowchart TB
    browser(["Navegador del usuario"])
    subgraph RENDER["Render - plan gratuito"]
        web["Static Site<br/>Frontend React + Vite (CDN)"]
        api["Web Service Docker<br/>API ASP.NET Core .NET 10<br/>REST + SignalR - puerto 8080 - /health"]
        db[("PostgreSQL 16<br/>BD subasta")]
        env{{"Env Group<br/>CORS"}}
    end
    subgraph GH["GitHub"]
        repo["Repositorio: código + Dockerfile"]
        actions["GitHub Actions"]
    end
    hcp[("HCP Terraform<br/>estado remoto")]
    browser -- "HTTPS" --> web
    browser -- "HTTPS REST/JSON + WSS SignalR" --> api
    api -- "red privada de Render" --> db
    env -. "variables" .-> api
    repo -- "docker build backend/Dockerfile" --> api
    repo -- "npm run build" --> web
    actions -- "terraform apply - infra.yml" --> RENDER
    actions -. "estado" .-> hcp
    actions -- "API de deploys - deploy.yml" --> api
```

## 2. Entorno local (docker-compose)

```mermaid
flowchart LR
    dev(["Desarrollador"])
    subgraph HOST["Docker en la máquina local"]
        db[("db: postgres:17-alpine<br/>puerto 5432")]
        api["api: imagen backend/Dockerfile<br/>puerto 8080"]
        web["web: nginx + React<br/>puerto 8081"]
    end
    dev --> web
    dev --> api
    api --> db
```

## 3. Pipeline CI/CD (GitHub Actions)

```mermaid
flowchart LR
    dev(["Desarrollador"]) -- "git push" --> gh["Repositorio GitHub"]
    gh --> wf_deploy["deploy.yml"]
    gh --> wf_generase_documentation["generase-documentation.yml"]
    gh --> wf_infra["infra.yml"]
    gh --> wf_snyk_semgrep["snyk-semgrep.yml"]
    gh --> wf_sonar["sonar.yml"]
    wf_infra -- "terraform apply" --> render["Render"]
    wf_sonar -- "análisis + quality gate" --> sonarcloud["SonarCloud"]
    wf_snyk_semgrep -- "SCA / SAST / imagen" --> reports["Reportes SARIF / HTML"]
    wf_deploy -- "pruebas + deploy API y frontend" --> render
    wf_generase_documentation -- "commit" --> docs["docs/*.md"]
```

## 4. Recursos Terraform

| Tipo | Nombre lógico | Descripción |
|---|---|---|
| `random_string` | `suffix` | Sufijo aleatorio para nombres únicos |
| `random_password` | `app_admin` | Contraseña generada del administrador inicial |
| `render_postgres` | `db` | Render PostgreSQL (plan free) |
| `render_web_service` | `api` | Render Web Service - contenedor Docker de la API .NET |
| `render_static_site` | `web` | Render Static Site - frontend React (CDN) |
| `render_env_group` | `cors` | Grupo de variables de entorno (CORS) |
| `render_env_group_link` | `cors_api` | Vínculo del grupo de variables con la API |
