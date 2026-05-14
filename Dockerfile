# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy local NuGet packages to avoid network issues
COPY nuget-packages /root/.nuget/packages

# Configure NuGet to use ONLY local packages (no network)
RUN mkdir -p /root/.nuget/NuGet && \
    echo '<?xml version="1.0" encoding="utf-8"?>' > /root/.nuget/NuGet/NuGet.Config && \
    echo '<configuration>' >> /root/.nuget/NuGet/NuGet.Config && \
    echo '  <packageSources>' >> /root/.nuget/NuGet/NuGet.Config && \
    echo '    <add key="local" value="/root/.nuget/packages" />' >> /root/.nuget/NuGet/NuGet.Config && \
    echo '  </packageSources>' >> /root/.nuget/NuGet/NuGet.Config && \
    echo '  <packageSourceCredentials>' >> /root/.nuget/NuGet/NuGet.Config && \
    echo '  </packageSourceCredentials>' >> /root/.nuget/NuGet/NuGet.Config && \
    echo '  <config>' >> /root/.nuget/NuGet/NuGet.Config && \
    echo '    <add key="globalPackagesFolder" value="/root/.nuget/packages" />' >> /root/.nuget/NuGet/NuGet.Config && \
    echo '  </config>' >> /root/.nuget/NuGet/NuGet.Config && \
    echo '  <disabledPackageSources>' >> /root/.nuget/NuGet/NuGet.Config && \
    echo '    <add key="nuget.org" value="true" />' >> /root/.nuget/NuGet/NuGet.Config && \
    echo '  </disabledPackageSources>' >> /root/.nuget/NuGet/NuGet.Config && \
    echo '</configuration>' >> /root/.nuget/NuGet/NuGet.Config

COPY api-inadimplencia.sln ./
COPY Directory.Build.props ./
COPY ApiInadimplencia.Domain/ApiInadimplencia.Domain.csproj ApiInadimplencia.Domain/
COPY ApiInadimplencia.Application/ApiInadimplencia.Application.csproj ApiInadimplencia.Application/
COPY ApiInadimplencia.Infrastructure/ApiInadimplencia.Infrastructure.csproj ApiInadimplencia.Infrastructure/
COPY api-inadimplencia.Api/api-inadimplencia.Api.csproj api-inadimplencia.Api/

# Restore packages using local cache first
RUN dotnet restore api-inadimplencia.Api/api-inadimplencia.Api.csproj \
    --packages /root/.nuget/packages \
    --verbosity normal

COPY . .
RUN dotnet publish api-inadimplencia.Api/api-inadimplencia.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --gid 10001 appgroup \
    && useradd --uid 10001 --gid appgroup --create-home --shell /usr/sbin/nologin appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
# App target net8.0 but transitive deps (OpenTelemetry.Exporter.Prometheus.AspNetCore 1.10.0-beta.1)
# require Microsoft.Extensions.* 9.0.0 assemblies, so run on aspnet:9.0 with major roll-forward.
ENV DOTNET_ROLL_FORWARD=Major
EXPOSE 8080

USER appuser

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "api-inadimplencia.Api.dll"]
