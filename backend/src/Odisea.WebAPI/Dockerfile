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
# Demo pages were copied in via publish (wwwroot/demo). Component bundle is built fresh:
COPY --from=components /src/components/dist/ ./wwwroot/components/
# loader.js lives under wwwroot/components alongside the bundles — keep it from the source tree:
COPY backend/src/Odisea.WebAPI/wwwroot/components/loader.js ./wwwroot/components/loader.js

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "Odisea.WebAPI.dll"]
