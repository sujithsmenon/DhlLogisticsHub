# syntax=docker/dockerfile:1.7

# ───────────────────────────────────────────────────────────────────────────
# Stage 1 — Build & publish
# ───────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first so the restore layer can be cached
COPY DhlLogistics.Shared/DhlLogistics.Shared.csproj DhlLogistics.Shared/
COPY DhlLogistics.Web/DhlLogistics.Web.csproj       DhlLogistics.Web/

RUN dotnet restore DhlLogistics.Web/DhlLogistics.Web.csproj

# Copy the rest of the source (Mobile project intentionally excluded;
# .dockerignore skips it because MAUI workloads aren't available in the SDK image)
COPY DhlLogistics.Shared/ DhlLogistics.Shared/
COPY DhlLogistics.Web/    DhlLogistics.Web/

RUN dotnet publish DhlLogistics.Web/DhlLogistics.Web.csproj \
      -c Release \
      -o /app/publish \
      --no-restore \
      /p:UseAppHost=false

# ───────────────────────────────────────────────────────────────────────────
# Stage 2 — Runtime
# ───────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Render injects PORT (default 10000). Bind Kestrel to it on all interfaces.
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000}
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app/publish ./

# Drop privileges
RUN groupadd -r app && useradd -r -g app app && chown -R app:app /app
USER app

EXPOSE 10000

ENTRYPOINT ["dotnet", "DhlLogistics.Web.dll"]
