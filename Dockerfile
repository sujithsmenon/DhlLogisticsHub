# syntax=docker/dockerfile:1.7
#
# Production Dockerfile for DhlLogistics.Web on AWS ECS Fargate.
#
# Design choices:
#   - Multi-stage: SDK image only used for build; runtime image is small.
#   - Runtime base = chiseled `aspnet:8.0` — ~100 MB vs ~210 MB for the full
#     Debian image, no shell, no package manager, smaller CVE surface.
#   - Non-root user (UID 1654, "app") baked into the chiseled image.
#   - Listens on 8080 (matches ECS task definition + ALB target group).
#   - No Docker HEALTHCHECK: the chiseled image has no shell/curl. Health is
#     enforced by the ECS ALB target group (HTTP GET /api/ping). See bottom.
#   - dotnet restore is a separate layer so adding source code doesn't bust
#     the NuGet cache.
#
# Build:   docker build -t dhl-web:local .
# Run:     docker run -p 8080:8080 -e ConnectionStrings__DefaultConnection="..." dhl-web:local

# ───────────────────────────────────────────────────────────────────────────
# Stage 1 — Restore + build
# ───────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy only the csproj files first → restore in its own cache layer.
COPY DhlLogistics.Shared/DhlLogistics.Shared.csproj DhlLogistics.Shared/
COPY DhlLogistics.Web/DhlLogistics.Web.csproj       DhlLogistics.Web/

RUN dotnet restore DhlLogistics.Web/DhlLogistics.Web.csproj \
      --runtime linux-x64

# Copy the full source.
COPY DhlLogistics.Shared/ DhlLogistics.Shared/
COPY DhlLogistics.Web/    DhlLogistics.Web/

# ───────────────────────────────────────────────────────────────────────────
# Stage 2 — Publish (trimmed of source, only the published output remains)
# ───────────────────────────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish DhlLogistics.Web/DhlLogistics.Web.csproj \
      -c $BUILD_CONFIGURATION \
      -o /app/publish \
      --no-restore \
      --runtime linux-x64 \
      --self-contained false \
      /p:UseAppHost=false \
      /p:PublishReadyToRun=true

# ───────────────────────────────────────────────────────────────────────────
# Stage 3 — Runtime (chiseled image, ~100 MB)
# ───────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled AS runtime
WORKDIR /app

# ECS sets these via the task definition, but defaults are useful for local runs.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_PRINT_TELEMETRY_MESSAGE=false

COPY --from=publish --chown=app:app /app/publish ./

# The chiseled image already runs as non-root "app" (UID 1654).
USER app
EXPOSE 8080

# No Docker HEALTHCHECK — the chiseled image has no shell/curl. ECS performs
# the real HTTP health check via the ALB target group hitting /api/ping.
# If you switch off chiseled, add: HEALTHCHECK CMD curl -fsS http://localhost:8080/api/ping || exit 1

ENTRYPOINT ["dotnet", "DhlLogistics.Web.dll"]
