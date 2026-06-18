using System.Net;
using System.Text.Json;
using ApiInadimplencia.Application.Abstractions.Monitoring;
using ApiInadimplencia.Application.Features.TrafficMonitoring;
using api_inadimplencia.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace api_inadimplencia.Api.Tests.Features.TrafficMonitoring;

public sealed class TrafficMonitoringEndpointsTests
{
    [Fact]
    public async Task LoadTestProfiles_IncludesLimitIdentificationProfile()
    {
        await using var factory = new ApiTestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/traffic-monitoring/load-tests/profiles");
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var profiles = document.RootElement.GetProperty("profiles").EnumerateArray();
        var limitProfile = profiles.Single(profile =>
            profile.GetProperty("key").GetString() == "identificar-limite");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5000, limitProfile.GetProperty("maxVirtualUsers").GetInt32());
        Assert.Equal(540, limitProfile.GetProperty("expectedDurationSeconds").GetInt32());
    }

    [Fact]
    public async Task Dashboard_WithExcludeLoadTestTraffic_ForwardsFilterToQuery()
    {
        var query = new RecordingTrafficAnalyticsQuery();
        await using var factory = new ApiTestWebApplicationFactory()
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITrafficAnalyticsQuery>();
                services.AddSingleton<ITrafficAnalyticsQuery>(query);
            }));
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/traffic-monitoring/dashboard?periodDays=7&excludeLoadTestTraffic=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(query.ExcludeLoadTestTraffic);
    }

    private sealed class RecordingTrafficAnalyticsQuery : ITrafficAnalyticsQuery
    {
        public bool ExcludeLoadTestTraffic { get; private set; }

        public Task<TrafficDashboardDto> GetDashboardAsync(
            int periodDays,
            string? apiName,
            string? environment,
            bool excludeLoadTestTraffic,
            CancellationToken cancellationToken = default)
        {
            ExcludeLoadTestTraffic = excludeLoadTestTraffic;
            var now = DateTime.UtcNow;

            return Task.FromResult(new TrafficDashboardDto(
                now,
                now.AddDays(-periodDays),
                now,
                new TrafficSummaryDto(0, 0, 0, 0, 0, 0, 0, 0),
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                new TrafficFilterOptionsDto([], [])));
        }
    }
}
