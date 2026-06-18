using System.Threading.Channels;
using ApiInadimplencia.Application.Abstractions.Monitoring;
using ApiInadimplencia.Application.Configuration;
using ApiInadimplencia.Application.Features.TrafficMonitoring;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApiInadimplencia.Infrastructure.Monitoring;

/// <summary>
/// Buffers traffic records and persists them in batches outside the request pipeline.
/// </summary>
public sealed class TrafficRecordChannel : BackgroundService, ITrafficRequestSink
{
    private readonly Channel<TrafficRequestRecord> _channel;
    private readonly ITrafficRequestStore _store;
    private readonly TrafficMonitoringOptions _options;
    private readonly ILogger<TrafficRecordChannel> _logger;
    private long _droppedRecords;

    /// <summary>
    /// Initializes a new traffic record channel.
    /// </summary>
    /// <param name="store">Traffic persistence adapter.</param>
    /// <param name="options">Traffic monitoring options.</param>
    /// <param name="logger">Logger.</param>
    public TrafficRecordChannel(
        ITrafficRequestStore store,
        IOptions<TrafficMonitoringOptions> options,
        ILogger<TrafficRecordChannel> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _channel = Channel.CreateBounded<TrafficRequestRecord>(new BoundedChannelOptions(_options.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <inheritdoc />
    public bool TryWrite(TrafficRequestRecord record)
    {
        if (_channel.Writer.TryWrite(record))
        {
            return true;
        }

        var dropped = Interlocked.Increment(ref _droppedRecords);
        if (dropped == 1 || dropped % 100 == 0)
        {
            _logger.LogWarning(
                "Traffic monitoring queue is full. Dropped records: {DroppedRecords}.",
                dropped);
        }

        return false;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<TrafficRequestRecord>(_options.BatchSize);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await FillBatchAsync(batch, stoppingToken).ConfigureAwait(false);
                await FlushAsync(batch, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal hosted-service shutdown.
        }
        finally
        {
            while (_channel.Reader.TryRead(out var record))
            {
                batch.Add(record);
                if (batch.Count >= _options.BatchSize)
                {
                    await FlushAsync(batch, CancellationToken.None).ConfigureAwait(false);
                }
            }

            await FlushAsync(batch, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task FillBatchAsync(
        ICollection<TrafficRequestRecord> batch,
        CancellationToken cancellationToken)
    {
        await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
        var flushAt = DateTime.UtcNow.AddSeconds(_options.FlushIntervalSeconds);

        while (batch.Count < _options.BatchSize)
        {
            while (batch.Count < _options.BatchSize && _channel.Reader.TryRead(out var record))
            {
                batch.Add(record);
            }

            if (batch.Count >= _options.BatchSize)
            {
                return;
            }

            var remaining = flushAt - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var recordAvailable = _channel.Reader.WaitToReadAsync(waitCancellation.Token).AsTask();
            var flushElapsed = Task.Delay(remaining, cancellationToken);
            var completed = await Task.WhenAny(recordAvailable, flushElapsed).ConfigureAwait(false);
            if (completed == flushElapsed)
            {
                waitCancellation.Cancel();
                return;
            }
        }
    }

    private async Task FlushAsync(
        ICollection<TrafficRequestRecord> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        var records = batch.ToArray();
        batch.Clear();

        try
        {
            await _store.WriteBatchAsync(records, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist {RecordCount} API traffic records.",
                records.Length);
        }
    }
}
