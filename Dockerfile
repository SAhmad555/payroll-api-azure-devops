# syntax=docker/dockerfile:1
# Multi-stage build for the Umbrella Payroll API.
#   docker build -t payroll-api:local .
#   docker run -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=Development payroll-api:local

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore first (cached layer as long as project files don't change).
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Payroll.Domain/Payroll.Domain.csproj                 src/Payroll.Domain/
COPY src/Payroll.Application/Payroll.Application.csproj       src/Payroll.Application/
COPY src/Payroll.Infrastructure/Payroll.Infrastructure.csproj src/Payroll.Infrastructure/
COPY src/Payroll.Api/Payroll.Api.csproj                       src/Payroll.Api/
RUN dotnet restore src/Payroll.Api/Payroll.Api.csproj

COPY src/ src/
RUN dotnet publish src/Payroll.Api/Payroll.Api.csproj \
      --configuration $BUILD_CONFIGURATION \
      --no-restore \
      --output /app/publish \
      /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Writable folder for the local SQLite fallback; in Azure the app uses Azure SQL instead.
RUN mkdir -p /app/data && chown "$APP_UID" /app/data

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__Payroll="Data Source=/app/data/payroll.db" \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

COPY --from=build /app/publish .

# Run as the built-in non-root user provided by the .NET images.
USER $APP_UID

ENTRYPOINT ["dotnet", "Payroll.Api.dll"]
