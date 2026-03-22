using DpcMonitor.Core.Models;

namespace DpcMonitor.Core.Services;

public sealed class MonitorCoordinator : IMonitorCoordinator
{
    private readonly IDpcEventSource _eventSource;
    private readonly IPrivilegeService _privilegeService;
    private readonly ILatencyClock _clock;
    private readonly DpcLatencyAggregator _aggregator = new(windowSize: 60);
    private readonly AlertEvaluator _alertEvaluator = new();

    private MonitorStatusSnapshot _snapshot = new(
        MonitorState.Idle,
        CurrentUs: 0,
        PeakUs: 0,
        AverageUs: 0,
        ThresholdUs: 0,
        StatusMessage: "Idle",
        Chart: Array.Empty<DpcLatencyBucket>());

    public MonitorCoordinator(IDpcEventSource eventSource, IPrivilegeService privilegeService, ILatencyClock clock)
    {
        _eventSource = eventSource;
        _privilegeService = privilegeService;
        _clock = clock;
    }

    public event EventHandler<MonitorStatusSnapshot>? SnapshotPublished;

    public async Task<MonitorStatusSnapshot> StartAsync(double thresholdUs, CancellationToken cancellationToken = default)
    {
        if (!_privilegeService.IsElevated())
        {
            _snapshot = new MonitorStatusSnapshot(
                MonitorState.Error,
                CurrentUs: 0,
                PeakUs: 0,
                AverageUs: 0,
                ThresholdUs: thresholdUs,
                StatusMessage: "Administrator rights are required to start monitoring.",
                Chart: _snapshot.Chart);

            SnapshotPublished?.Invoke(this, _snapshot);
            return _snapshot;
        }

        _eventSource.SampleReceived += OnSampleReceived;

        try
        {
            await _eventSource.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _eventSource.SampleReceived -= OnSampleReceived;
            _snapshot = new MonitorStatusSnapshot(
                MonitorState.Error,
                CurrentUs: 0,
                PeakUs: 0,
                AverageUs: 0,
                ThresholdUs: thresholdUs,
                StatusMessage: $"Failed to start monitoring: {ex.Message}",
                Chart: _snapshot.Chart);

            SnapshotPublished?.Invoke(this, _snapshot);
            return _snapshot;
        }

        _snapshot = new MonitorStatusSnapshot(
            MonitorState.Running,
            CurrentUs: 0,
            PeakUs: 0,
            AverageUs: 0,
            ThresholdUs: thresholdUs,
            StatusMessage: "Running",
            Chart: _snapshot.Chart);

        SnapshotPublished?.Invoke(this, _snapshot);
        return _snapshot;
    }

    public async Task<MonitorStatusSnapshot> StopAsync(CancellationToken cancellationToken = default)
    {
        _eventSource.SampleReceived -= OnSampleReceived;
        await _eventSource.StopAsync(cancellationToken);

        _snapshot = _snapshot with
        {
            State = MonitorState.Idle,
            StatusMessage = "Idle",
            Chart = _aggregator.History.ToArray()
        };

        SnapshotPublished?.Invoke(this, _snapshot);
        return _snapshot;
    }

    private void OnSampleReceived(object? sender, DpcLatencySample sample)
    {
        _aggregator.Add(sample);

        foreach (var bucket in _aggregator.DrainCompletedBuckets())
        {
            var evaluation = _alertEvaluator.Evaluate(bucket, _snapshot.ThresholdUs, _snapshot.State);
            _snapshot = new MonitorStatusSnapshot(
                evaluation.State,
                CurrentUs: bucket.CurrentUs,
                PeakUs: bucket.PeakUs,
                AverageUs: bucket.AverageUs,
                ThresholdUs: _snapshot.ThresholdUs,
                StatusMessage: evaluation.LogMessage ?? $"Updated at {_clock.UtcNow:O}",
                Chart: _aggregator.History.ToArray());

            SnapshotPublished?.Invoke(this, _snapshot);
        }
    }
}
