variable "project" {
  description = "Nombre base de los servicios: define las URLs https://<project>.onrender.com (frontend) y https://<project>-api.onrender.com (API)"
  type        = string
  default     = "subasta-jhonyvargas"

  validation {
    condition     = can(regex("^[a-z0-9-]{3,30}$", var.project))
    error_message = "Solo minusculas, numeros y guiones (3-30 caracteres)."
  }
}

variable "repo_url" {
  description = "URL del repositorio GitHub publico (https://github.com/usuario/repo)"
  type        = string
}

variable "branch" {
  description = "Rama que se despliega"
  type        = string
  default     = "main"
}

variable "region" {
  description = "Region de Render (oregon, ohio, virginia, frankfurt, singapore)"
  type        = string
  default     = "oregon"
}

variable "plan" {
  description = "Plan de Render para la API y la base de datos (free = gratuito)"
  type        = string
  default     = "free"
}

variable "postgres_version" {
  description = "Version de PostgreSQL"
  type        = string
  default     = "16"
}

variable "admin_password" {
  description = "Contrasena del administrador inicial (vacia = se genera una aleatoria)"
  type        = string
  default     = ""
  sensitive   = true
}

variable "admin_email" {
  description = "Correo del administrador inicial de la aplicacion"
  type        = string
  default     = "admin@subasta.app"
}
