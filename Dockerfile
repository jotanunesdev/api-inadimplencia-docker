# syntax=docker/dockerfile:1

FROM grafana/k6:latest AS k6

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Install corporate CA bundle so the build stage can reach nuget.org through
# the Sophos firewall that performs TLS inspection.
COPY certs/ /usr/local/share/ca-certificates-extra/
RUN if ls /usr/local/share/ca-certificates-extra/*.crt >/dev/null 2>&1; then \
        cp /usr/local/share/ca-certificates-extra/*.crt /usr/local/share/ca-certificates/ && \
        update-ca-certificates; \
    fi

# Copy the local NuGet cache (may be empty on CI/servers; populated locally for
# fast offline builds via `dotnet restore --packages nuget-packages`).
COPY nuget-packages /root/.nuget/packages

# Configure NuGet to prefer the local cache and fall back to nuget.org over
# the corporate proxy. This lets local dev builds run offline AND lets servers
# without a pre-warmed cache restore through the firewall.
RUN mkdir -p /root/.nuget/NuGet && \
    printf '%s\n' \
        '<?xml version="1.0" encoding="utf-8"?>' \
        '<configuration>' \
        '  <packageSources>' \
        '    <clear />' \
        '    <add key="local" value="/root/.nuget/packages" />' \
        '    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />' \
        '  </packageSources>' \
        '  <config>' \
        '    <add key="globalPackagesFolder" value="/root/.nuget/packages" />' \
        '  </config>' \
        '</configuration>' \
        > /root/.nuget/NuGet/NuGet.Config

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
    && apt-get install -y --no-install-recommends curl ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --gid 10001 appgroup \
    && useradd --uid 10001 --gid appgroup --create-home --shell /usr/sbin/nologin appuser

# Install corporate CA bundle (Sophos firewall does TLS inspection of outbound HTTPS)
COPY certs/ /usr/local/share/ca-certificates-extra/
RUN if ls /usr/local/share/ca-certificates-extra/*.crt >/dev/null 2>&1; then \
        cp /usr/local/share/ca-certificates-extra/*.crt /usr/local/share/ca-certificates/ && \
        update-ca-certificates; \
    fi

COPY --from=build /app/publish .
COPY --from=k6 /usr/bin/k6 /usr/local/bin/k6
COPY --from=build --chown=appuser:appgroup /src/loadtests/k6 /app/loadtests/k6

ENV ASPNETCORE_HTTP_PORTS=8080
# App target net8.0 but transitive deps (OpenTelemetry.Exporter.Prometheus.AspNetCore 1.10.0-beta.1)
# require Microsoft.Extensions.* 9.0.0 assemblies, so run on aspnet:9.0 with major roll-forward.
ENV DOTNET_ROLL_FORWARD=Major
EXPOSE 8080

USER appuser

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "api-inadimplencia.Api.dll"]
