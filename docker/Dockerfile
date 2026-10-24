# syntax=docker/dockerfile:1
# Build from the repository root:  docker build -f docker/Dockerfile -t complot .

# 1. Frontend (Vue + Vite+) -> static files. Vite+ brings the pnpm version pinned in package.json (devEngines).
FROM node:24-alpine AS web
RUN npm install --global vite-plus@1.4.0
WORKDIR /src/frontend
COPY frontend/package.json frontend/pnpm-lock.yaml frontend/pnpm-workspace.yaml ./
RUN vp install --frozen-lockfile
COPY frontend/ ./
RUN vp run build

# 2. Backend (.NET 10) -> published app with the frontend in wwwroot
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src/backend
COPY backend/global.json backend/Directory.Build.props backend/Directory.Packages.props ./
COPY backend/src/Complot.Domain/Complot.Domain.csproj src/Complot.Domain/
COPY backend/src/Complot.Api/Complot.Api.csproj src/Complot.Api/
RUN dotnet restore src/Complot.Api/Complot.Api.csproj
COPY backend/ ./
COPY --from=web /src/frontend/dist src/Complot.Api/wwwroot
# The OpenAPI document and TypeScript client are generated during development, not in the image.
RUN dotnet publish src/Complot.Api/Complot.Api.csproj -c Release -o /app --no-restore -p:OpenApiGenerateDocuments=false

# 3. Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
COPY --from=api /app ./
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Storage__DataDir=/data
RUN mkdir -p /data && chown "$APP_UID" /data
VOLUME /data
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s CMD wget -qO- http://127.0.0.1:8080/health || exit 1
ENTRYPOINT ["dotnet", "Complot.Api.dll"]
