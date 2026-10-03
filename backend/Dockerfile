# syntax=docker/dockerfile:1

# ---------- Etapa de compilación ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY Subasta.sln ./
COPY src/Subasta.Api/Subasta.Api.csproj src/Subasta.Api/
RUN dotnet restore src/Subasta.Api/Subasta.Api.csproj

COPY src/Subasta.Api/ src/Subasta.Api/
RUN dotnet publish src/Subasta.Api/Subasta.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# ---------- Etapa de ejecución (Alpine mínima, paquetes del SO actualizados, usuario no root) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
RUN apk upgrade --no-cache
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_gcServer=0
COPY --from=build /app ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Subasta.Api.dll"]
