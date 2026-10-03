resource "random_string" "suffix" {
  length  = 5
  upper   = false
  special = false
}

resource "random_password" "app_admin" {
  length           = 20
  special          = true
  override_special = "-_.!"
}

locals {
  name           = "${var.project}-${random_string.suffix.result}"
  admin_password = var.admin_password != "" ? var.admin_password : random_password.app_admin.result
}

# ---------- Base de datos: Render PostgreSQL (plan free) ----------
resource "render_postgres" "db" {
  name          = "${local.name}-db"
  plan          = var.plan
  region        = var.region
  version       = var.postgres_version
  database_name = "subasta"
  database_user = "subasta"
}

# ---------- Backend: Render Web Service con imagen Docker (plan free) ----------
resource "render_web_service" "api" {
  name              = "${local.name}-api"
  plan              = var.plan
  region            = var.region
  health_check_path = "/health"

  runtime_source = {
    docker = {
      repo_url        = var.repo_url
      branch          = var.branch
      context         = "./backend"
      dockerfile_path = "./backend/Dockerfile"
      # El despliegue lo dispara deploy.yml después de pasar las pruebas
      auto_deploy = false
    }
  }

  env_vars = {
    "ASPNETCORE_ENVIRONMENT"     = { value = "Production" }
    "PORT"                       = { value = "8080" }
    "ConnectionStrings__Default" = { value = render_postgres.db.connection_info.internal_connection_string }
    "Jwt__Key"                   = { generate_value = true }
    "Seed__AdminEmail"           = { value = var.admin_email }
    "Seed__AdminPassword"        = { value = local.admin_password }
  }
}

# ---------- Frontend: Render Static Site (siempre gratuito) ----------
resource "render_static_site" "web" {
  name           = "${local.name}-web"
  repo_url       = var.repo_url
  branch         = var.branch
  root_directory = "frontend"
  build_command  = "npm ci --ignore-scripts && npm run build"
  publish_path   = "dist"
  auto_deploy    = false

  env_vars = {
    "VITE_API_URL" = { value = render_web_service.api.url }
    "NODE_VERSION" = { value = "24" }
  }

  # Enrutamiento SPA: todas las rutas sirven index.html
  routes = [
    {
      type        = "rewrite"
      source      = "/*"
      destination = "/index.html"
    }
  ]

  headers = [
    { path = "/*", name = "X-Content-Type-Options", value = "nosniff" },
    { path = "/*", name = "X-Frame-Options", value = "DENY" },
    { path = "/*", name = "Referrer-Policy", value = "no-referrer" }
  ]
}

# CORS: la URL del frontend se conoce después de crearlo, por eso se inyecta con un grupo de variables
resource "render_env_group" "cors" {
  name = "${local.name}-cors"
  env_vars = {
    "Cors__AllowedOrigins__0" = { value = render_static_site.web.url }
  }
}

resource "render_env_group_link" "cors_api" {
  env_group_id = render_env_group.cors.id
  service_ids  = [render_web_service.api.id]
}
