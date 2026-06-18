using ApiInadimplencia.Application.Features.LoadTesting;

namespace ApiInadimplencia.Application.Abstractions.Monitoring;

public interface ILoadTestOrchestrator
{
    IReadOnlyList<LoadTestProfileDto> GetProfiles();

    Task<LoadTestRunDetailDto> StartAsync(
        StartLoadTestRequestDto request,
        string triggeredBy,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LoadTestRunListItemDto>> ListRunsAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<LoadTestRunDetailDto?> GetRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}
