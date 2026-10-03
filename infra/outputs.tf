output "api_service_id" {
  value = render_web_service.api.id
}

output "api_url" {
  value = render_web_service.api.url
}

output "web_service_id" {
  value = render_static_site.web.id
}

output "frontend_url" {
  value = render_static_site.web.url
}

output "postgres_id" {
  value = render_postgres.db.id
}

output "app_admin_email" {
  value = var.admin_email
}

output "app_admin_password" {
  value     = local.admin_password
  sensitive = true
}
