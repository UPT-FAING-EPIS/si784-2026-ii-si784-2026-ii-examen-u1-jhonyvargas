# SubastaYa · Aplicación de Subasta en Línea

Plataforma web para publicar artículos en subasta, ofertar **en tiempo real** (WebSockets) y gestionar el cierre y la adjudicación automática de los productos.

| Capa | Tecnología |
|---|---|
| Backend (API REST + WebSockets) | ASP.NET Core **.NET 10**, EF Core, SignalR, JWT |
| Base de datos | **PostgreSQL** (nube) · SQLite (modo local rápido / pruebas) |
| Frontend | **React 19 + Vite + TypeScript** |
| Contenedor | `backend/Dockerfile` (multi-stage, Alpine, usuario no root) |
| Nube (gratuita) | **Render** (API Docker + PostgreSQL + Static Site) con **Terraform** |
| CI/CD | GitHub Actions: `infra.yml`, `sonar.yml`, `snyk-semgrep.yml`, `deploy.yml`, `generase-documentation.yml` |

---

## 1. Funcionalidades

- **Publicación de subastas** con imágenes (JPG/PNG/GIF/WEBP, máx. 5 × 2 MB, validación de firma binaria), descripción, categoría, precio inicial, incremento mínimo y fecha/hora de inicio y cierre.
- **Listado y filtrado** de subastas **activas, próximas y finalizadas**: búsqueda por texto, categoría, rango de precio, orden y paginación.
- **Pujas en tiempo real**: el detalle se actualiza al instante por WebSocket (SignalR); control de concurrencia optimista para pujas simultáneas.
- **Notificaciones automáticas** (persistidas y enviadas por WebSocket): nueva oferta (al vendedor), oferta superada, cierre, adjudicación (ganador y vendedor) y cancelación.
- **Cierre y adjudicación automáticos**: un servicio en segundo plano activa las subastas programadas y cierra las vencidas, adjudicando al mejor postor.
- **Panel de usuario**: subastas en las que participa, historial de pujas (va ganando / superada / ganada), artículos ganados y publicados.
- **Panel de administración**: indicadores globales, gestión de usuarios (rol / activar-desactivar) y de subastas (cerrar / cancelar).
- **Validación de datos en frontend y backend** (DataAnnotations + reglas de dominio; validaciones espejo en React).

## 2. Endpoints principales

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/auctions` | Publicar nueva subasta (JWT) |
| GET | `/auctions?status=active\|upcoming\|finished&search=&categoryId=&minPrice=&maxPrice=&sort=&page=` | Listar subastas |
| GET | `/auctions/{id}` | Detalle de subasta |
| POST | `/auctions/{id}/images` | Subir imagen (vendedor) |
| POST | `/bids` | Realizar una oferta (JWT) |
| GET | `/bids?auctionId={id}` | Historial de pujas de una subasta |
| GET | `/user/auctions?type=published\|participating\|won` | Subastas del usuario |
| GET | `/user/bids` | Historial de pujas del usuario |
| GET | `/user/notifications` | Notificaciones del usuario |
| POST | `/auth/register` · `/auth/login` | Registro e inicio de sesión |
| GET/PATCH/POST | `/admin/...` | Administración (rol Admin) |
| WS | `/hubs/auctions` | Hub SignalR: eventos `BidPlaced`, `AuctionClosed`, `AuctionStarted`, `AuctionCancelled`, `Notification` |

La tabla completa (generada automáticamente) está en [docs/diagrama-componentes.md](docs/diagrama-componentes.md). OpenAPI: `GET /openapi/v1.json`.

## 3. Estructura del repositorio

```
backend/
  src/Subasta.Api/          API: Domain, Data (EF Core + migraciones), Services, Controllers, Hubs
  tests/Subasta.UnitTests/         Pruebas unitarias (dominio, validaciones, servicios)
  tests/Subasta.IntegrationTests/  Pruebas de integración HTTP + WebSocket (WebApplicationFactory)
  tools/Subasta.DocGen/     Generador del diccionario de datos y diagramas Mermaid
  Dockerfile                Imagen de contenedor del backend
frontend/                   SPA React + Vite + TypeScript
infra/                      Terraform (Render + HCP Terraform)
docs/                       Documentación generada (diccionario, ER, clases, componentes, despliegue)
.github/workflows/          Automatizaciones CI/CD
docker-compose.yml          Entorno local completo (PostgreSQL + API + frontend)
```

## 4. Ejecución local

### Opción A — rápida, sin Docker (SQLite)

Requisitos: .NET SDK 10 y Node.js 22+.

```powershell
# Terminal 1: API en http://localhost:5270
cd backend/src/Subasta.Api
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Database__Provider = "Sqlite"
$env:Seed__AdminEmail = "admin@subasta.local"
$env:Seed__AdminPassword = "<elija-una-clave-segura>"
dotnet run --no-launch-profile --urls http://localhost:5270

# Terminal 2: frontend en http://localhost:5173
cd frontend
npm install
npm run dev
```

### Opción B — completa con Docker (PostgreSQL)

```bash
cp .env.example .env      # complete las claves
docker compose up --build # frontend: http://localhost:8081 · API: http://localhost:8080
```

### Pruebas

```bash
dotnet test backend/Subasta.sln   # 42 pruebas unitarias + 14 de integración (incluye WebSocket en tiempo real)
```

## 5. Nube gratuita y CI/CD

Todo usa **planes gratuitos** (no requiere tarjeta de crédito):

| Servicio | Uso | Plan |
|---|---|---|
| [Render](https://render.com) | API en contenedor Docker, PostgreSQL y frontend estático | Free |
| [HCP Terraform](https://app.terraform.io) | Estado remoto de Terraform | Free |
| [SonarCloud](https://sonarcloud.io) | Calidad y seguridad del código | Free (repo público) |
| [Snyk](https://snyk.io) | Dependencias, código e imagen del contenedor | Free |
| [Semgrep](https://semgrep.dev) | SAST | OSS (sin cuenta) |
| GitHub Actions | Automatizaciones | Free (repo público) |

### 5.1 Configuración (una sola vez, ~10 minutos)

Cree las cuentas gratuitas (todas permiten ingresar con GitHub) y agregue los tokens en
**GitHub → Settings → Secrets and variables → Actions → New repository secret**:

| Secreto | Dónde obtenerlo |
|---|---|
| `RENDER_API_KEY` | [Render](https://dashboard.render.com) → Account Settings → API Keys → Create API Key |
| `TF_API_TOKEN` | [HCP Terraform](https://app.terraform.io) → crear una organización (cualquier nombre) → User Settings → Tokens |
| `SONAR_TOKEN` | [SonarCloud](https://sonarcloud.io) → crear/importar una organización → My Account → Security → Generate Token |
| `SNYK_TOKEN` | [Snyk](https://app.snyk.io) → Account Settings → Auth Token (y en Settings → Snyk Code → habilitar) |
| `APP_ADMIN_PASSWORD` *(opcional)* | Contraseña que desea para el administrador `admin@subasta.app` |

La organización de HCP Terraform, el workspace de Render y la organización/proyecto de SonarCloud
**se detectan y crean automáticamente** a partir de los tokens (opcionalmente pueden fijarse con las
variables `TF_CLOUD_ORGANIZATION`, `SONAR_ORGANIZATION` y `SONAR_PROJECT_KEY`).

Luego, en la pestaña **Actions**:

1. **infra → Run workflow → apply**: crea PostgreSQL, la API (contenedor) y el frontend en Render y muestra sus URLs.
2. **deploy → Run workflow** (también se ejecuta con cada `git push` a `main`): pruebas → despliegue de la API → despliegue del frontend → verificación de `/health`.
3. **sonar**, **snyk-semgrep** y **generase-documentation** se ejecutan solos con cada push (o manualmente).

### 5.2 Automatizaciones

| Workflow | Qué hace | Evidencia |
|---|---|---|
| `infra.yml` | `terraform fmt/validate/plan/apply/destroy` de `infra/` en Render; estado en HCP Terraform | Resumen con URLs de la API y el frontend |
| `sonar.yml` | Compila, ejecuta pruebas con cobertura y analiza backend + frontend en SonarCloud; espera el Quality Gate y **falla si hay bugs, vulnerabilidades o security hotspots** | Resumen + artefacto `reporte-sonarcloud` |
| `snyk-semgrep.yml` | Semgrep (SAST) + Snyk Open Source (NuGet/npm) + Snyk Code + **Snyk Container** sobre la imagen del backend; falla ante cualquier vulnerabilidad | Artefactos `reporte-semgrep` y `reporte-snyk` (SARIF, JSON y **HTML**) + pestaña *Security* |
| `deploy.yml` | Pruebas, build del frontend y de la imagen, despliegue en Render vía API y verificación de salud | Resumen con estado de cada servicio |
| `generase-documentation.yml` | Levanta PostgreSQL, aplica las migraciones y genera el **diccionario de datos**, **diagrama ER**, **diagrama de clases**, **diagrama de componentes** y **diagramas de despliegue** en Mermaid; hace commit en `docs/` | Carpeta [`docs/`](docs/README.md) + artefacto `documentacion` |

> **Notas del plan gratuito de Render:** la API se suspende tras 15 min sin tráfico y tarda ~1 min en despertar; la base de datos gratuita expira a los 30 días (puede recrearse ejecutando `infra.yml` o cambiar `plan`).

## 6. Seguridad aplicada

- Contraseñas con PBKDF2 (`PasswordHasher`), JWT firmado (HS256) con clave generada por Render, roles `User`/`Admin`.
- CORS restringido al dominio del frontend; cabeceras `nosniff`, `X-Frame-Options`, `Referrer-Policy`; sin cabecera `Server`.
- Validación de imágenes por firma binaria, límites de tamaño y nombres saneados; expresiones regulares con *timeout*.
- Errores en formato ProblemDetails sin exponer detalles internos.
- Imagen de contenedor Alpine con paquetes actualizados y usuario no root; `docker-compose` con `no-new-privileges` y sistema de archivos de solo lectura.
- GitHub Actions fijadas por SHA de commit y secretos solo en variables de entorno.

## 7. Entrega

| Ítem | URL |
|---|---|
| Repositorio | https://github.com/UPT-FAING-EPIS/si784-2026-ii-si784-2026-ii-examen-u1-jhonyvargas |
| Aplicación publicada | https://subasta-jhonyvargas.onrender.com |
| API publicada | https://subasta-jhonyvargas-api.onrender.com (salud: `/health`) |
| SonarCloud | https://sonarcloud.io/project/overview?id=subasta-jhonyvargas |

## 8. Documentación técnica

- [Diccionario de datos](docs/diccionario-datos.md)
- [Diagrama entidad-relación](docs/diagrama-entidad-relacion.md)
- [Diagrama de clases](docs/diagrama-clases.md)
- [Diagrama de componentes](docs/diagrama-componentes.md)
- [Diagramas de despliegue](docs/diagrama-despliegue.md)
