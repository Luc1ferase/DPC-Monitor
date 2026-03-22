using DpcMonitor.Core.Models;
using DpcMonitor.Core.Services;
using DpcMonitor.Core.ViewModels;

namespace DpcMonitor.Core.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task OnSnapshotPublished_UpdatesCurrentPeakAverageAndChartSeries()
    {
        var coordinator = new FakeMonitorCoordinator();
        var viewModel = new MainWindowViewModel(coordinator, action => action());

        await viewModel.StartAsync();
        coordinator.Publish(new MonitorStatusSnapshot(
            MonitorState.Running,
            CurrentUs: 184,
            PeakUs: 512,
            AverageUs: 129,
            ThresholdUs: 300,
            StatusMessage: "Running",
            Chart: new[] { new DpcLatencyBucket(DateTimeOffset.UtcNow, 184, 512, 129) }));

        Assert.Equal("184 us", viewModel.CurrentText);
        Assert.Equal("512 us", viewModel.PeakText);
        Assert.Equal("129 us", viewModel.AverageText);
        Assert.Single(viewModel.ChartBuckets);
    }

    private sealed class FakeMonitorCoordinator : IMonitorCoordinator
    {
        public event EventHandler<MonitorStatusSnapshot>? SnapshotPublished;

        public Task<MonitorStatusSnapshot> StartAsync(double thresholdUs, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new MonitorStatusSnapshot(
                MonitorState.Running,
                CurrentUs: 0,
                PeakUs: 0,
                AverageUs: 0,
                ThresholdUs: thresholdUs,
                StatusMessage: "Running",
                Chart: Array.Empty<DpcLatencyBucket>()));
        }

        public Task<MonitorStatusSnapshot> StopAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new MonitorStatusSnapshot(
                MonitorState.Idle,
                CurrentUs: 0,
                PeakUs: 0,
                AverageUs: 0,
                ThresholdUs: 300,
                StatusMessage: "Idle",
                Chart: Array.Empty<DpcLatencyBucket>()));
        }

        public void Publish(MonitorStatusSnapshot snapshot)
        {
            SnapshotPublished?.Invoke(this, snapshot);
        }
    }
}
