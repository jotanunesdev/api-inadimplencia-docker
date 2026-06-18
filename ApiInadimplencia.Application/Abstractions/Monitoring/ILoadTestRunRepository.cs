using ApiInadimplencia.Application.Features.LoadTesting;

namespace ApiInadimplencia.Application.Abstractions.Monitoring;

public interface ILoadTestRunRepository
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    Task InsertStartedRunAsync(
        LoadTestRunListItemDto run,
        CancellationToken cancellationToken = default);

    Task CompleteRunAsync(
        LoadTestRunDetailDto run,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LoadTestRunListItemDto>> ListRunsAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<LoadTestRunDetailDto?> GetRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}
