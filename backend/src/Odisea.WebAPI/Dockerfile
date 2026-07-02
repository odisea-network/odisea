# syntax=docker/dockerfile:1.6
# Multi-stage build for the Odisea Web API.
# Build context MUST be the repository root — Stage A pulls frontend/components.

# ----- Stage A: build Lit web components -----
FROM node:22-alpine AS components
WORKDIR /src/components
COPY frontend/components/package.json frontend/components/package-lock.json* ./
RUN npm install --no-audit --no-fund
COPY frontend/components/ ./
RUN npm run build

# ----- Stage A2: build the Angular portal (SPA) -----
FROM node:22-alpine AS portal
WORKDIR /src/portal
COPY frontend/portal/package.json frontend/portal/package-lock.json* ./
RUN npm install --no-audit --no-fund
COPY frontend/portal/ ./
RUN npx ng build --configuration=production

# ----- Stage B: restore + publish .NET app -----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet
WORKDIR /src
COPY backend/global.json ./global.json
COPY backend/Odisea.sln ./Odisea.sln
COPY backend/src/Odisea.Domain/Odisea.Domain.csproj                   ./src/Odisea.Domain/
COPY backend/src/Odisea.Application/Odisea.Application.csproj         ./src/Odisea.Application/
COPY backend/src/Odisea.Infrastructure/Odisea.Infrastructure.csproj   ./src/Odisea.Infrastructure/
COPY backend/src/Odisea.WebAPI/Odisea.WebAPI.csproj                   ./src/Odisea.WebAPI/
COPY backend/tests/Odisea.UnitTests/Odisea.UnitTests.csproj           ./tests/Odisea.UnitTests/
RUN dotnet restore Odisea.sln
COPY backend/ ./
RUN dotnet publish src/Odisea.WebAPI/Odisea.WebAPI.csproj -c Release -o /publish /p:UseAppHost=false

# ----- Stage C: runtime image -----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=dotnet /publish ./
# Angular portal (SPA) served from wwwroot root; the API's MapFallbackToFile
# routes client-side deep links back to index.html so one origin serves everything.
COPY --from=portal /src/portal/dist/portal/browser/ ./wwwroot/
# Versioned bundle — agencies embed via loader.js?data-version=1 or hardcode the v1 path:
COPY --from=components /src/components/dist/v1/ ./wwwroot/components/v1/
# loader.js and the legacy-path shim come from the source tree (published as wwwroot content).
# dotnet publish already copied them; these lines guarantee the final image is up to date:
COPY backend/src/Odisea.WebAPI/wwwroot/components/loader.js ./wwwroot/components/loader.js
COPY backend/src/Odisea.WebAPI/wwwroot/components/odisea-components.umd.js ./wwwroot/components/odisea-components.umd.js

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "Odisea.WebAPI.dll"]
