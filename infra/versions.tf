terraform {
  required_version = ">= 1.6.0"

  required_providers {
    render = {
      source  = "render-oss/render"
      version = "~> 1.7"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # Estado remoto gratuito en HCP Terraform (app.terraform.io).
  # La organización y el workspace se toman de TF_CLOUD_ORGANIZATION y TF_WORKSPACE (ver infra.yml).
  cloud {}
}

# Credenciales por variables de entorno: RENDER_API_KEY y RENDER_OWNER_ID
provider "render" {
  wait_for_deploy_completion = false
}
