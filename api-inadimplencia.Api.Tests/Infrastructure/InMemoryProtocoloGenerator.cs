using System.Globalization;
using System.Threading;
using ApiInadimplencia.Application.Abstractions;

namespace api_inadimplencia.Api.Tests.Infrastructure;

public sealed class InMemoryProtocoloGenerator : IProtocoloGenerator
{
    private int _sequence;

    public Task<string> GerarProtocoloAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var next = Interlocked.Increment(ref _sequence);
        var prefix = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return Task.FromResult($"{prefix}{next:00000}");
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _sequence, 0);
    }
}
