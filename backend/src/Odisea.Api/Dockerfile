# syntax=docker/dockerfile:1.6
# Multi-stage build for the Odisea API. Build context MUST be the repository root.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY backend/Odisea.slnx backend/Directory.Build.props backend/Directory.Packages.props ./backend/
COPY backend/src/Odisea.SharedKernel/Odisea.SharedKernel.csproj                           ./backend/src/Odisea.SharedKernel/
COPY backend/src/Odisea.Api/Odisea.Api.csproj                                             ./backend/src/Odisea.Api/
COPY backend/src/Modules/Odisea.Modules.Agencies/Odisea.Modules.Agencies.csproj           ./backend/src/Modules/Odisea.Modules.Agencies/
COPY backend/src/Modules/Odisea.Modules.Catalog/Odisea.Modules.Catalog.csproj             ./backend/src/Modules/Odisea.Modules.Catalog/
COPY backend/src/Modules/Odisea.Modules.Pricing/Odisea.Modules.Pricing.csproj             ./backend/src/Modules/Odisea.Modules.Pricing/
COPY backend/src/Modules/Odisea.Modules.Booking/Odisea.Modules.Booking.csproj             ./backend/src/Modules/Odisea.Modules.Booking/
COPY backend/src/Modules/Odisea.Modules.Integrations/Odisea.Modules.Integrations.csproj   ./backend/src/Modules/Odisea.Modules.Integrations/
COPY backend/src/Modules/Odisea.Modules.Documents/Odisea.Modules.Documents.csproj         ./backend/src/Modules/Odisea.Modules.Documents/
COPY backend/tests/Odisea.UnitTests/Odisea.UnitTests.csproj                               ./backend/tests/Odisea.UnitTests/
RUN dotnet restore backend/Odisea.slnx
COPY backend/ ./backend/
RUN dotnet publish backend/src/Odisea.Api/Odisea.Api.csproj -c Release -o /publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# QuestPDF (SkiaSharp) needs fontconfig at runtime for PDF generation.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /publish ./
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "Odisea.Api.dll"]
