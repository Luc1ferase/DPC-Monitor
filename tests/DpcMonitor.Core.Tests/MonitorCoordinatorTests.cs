using DpcMonitor.Core.Models;
using DpcMonitor.Core.Services;

namespace DpcMonitor.Core.Tests;

public sealed class MonitorCoordinatorTests
{
    [Fact]
    public async Task StartAsync_PublishesErrorSnapshot_WhenPrivilegesAreInsufficient()
    {
        var source = new FakeDpcEventSource();
        var privileges = new StubPrivilegeService(isElevated: false);
        var coordinator = new MonitorCoordinator(source, privileges, new ManualLatencyClock());

        var snapshot = await coordinator.StartAsync(thresholdUs: 300);

        Assert.Equal(MonitorState.Error, snapshot.State);
        Assert.Contains("administrator", snapshot.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(source.Started);
    }

    [Fact]
    public async Task StopAsync_StopsSourceAndPreservesCompletedHistory()
    {
        var source = new FakeDpcEventSource();
        var privileges = new StubPrivilegeService(isElevated: true);
        var coordinator = new MonitorCoordinator(source, privileges, new ManualLatencyClock());

        await coordinator.StartAsync(thresholdUs: 300);
        source.Emit(new DpcLatencySample(new DateTimeOffset(2026, 3, 22, 10, 0, 0, TimeSpan.Zero), 120));
        source.Emit(new DpcLatencySample(new DateTimeOffset(2026, 3, 22, 10, 0, 1, TimeSpan.Zero), 80));

        var snapshot = await coordinator.StopAsync();

        Assert.False(source.Started);
        Assert.Equal(1, source.StopCalls);
        Assert.Equal(MonitorState.Idle, snapshot.State);
        Assert.Single(snapshot.Chart);
        Assert.Equal(120, snapshot.Chart[0].CurrentUs);
    }

    [Fact]
    public async Task StartAsync_PublishesErrorSnapshot_WhenEventSourceStartThrows()
    {
        var source = new ThrowingDpcEventSource();
        var privileges = new StubPrivilegeService(isElevated: true);
        var coordinator = new MonitorCoordinator(source, privileges, new ManualLatencyClock());

        var snapshot = await coordinator.StartAsync(thresholdUs: 300);

        Assert.Equal(MonitorState.Error, snapshot.State);
        Assert.Contains("ETW start failed", snapshot.StatusMessage, StringComparison.Ordinal);
    }

    private sealed class FakeDpcEventSource : IDpcEventSource
    {
        public event EventHandler<DpcLatencySample>? SampleReceived;

        public bool Started { get; private set; }

        public int StopCalls { get; private set; }

        public void Emit(DpcLatencySample sample)
        {
            SampleReceived?.Invoke(this, sample);
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            Started = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            Started = false;
            StopCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class StubPrivilegeService : IPrivilegeService
    {
        private readonly bool _isElevated;

        public StubPrivilegeService(bool isElevated)
        {
            _isElevated = isElevated;
        }

        public bool IsElevated() => _isElevated;
    }

    private sealed class ManualLatencyClock : ILatencyClock
    {
        public DateTimeOffset UtcNow => new(2026, 3, 22, 10, 0, 0, TimeSpan.Zero);
    }

    private sealed class ThrowingDpcEventSource : IDpcEventSource
    {
        public event EventHandler<DpcLatencySample>? SampleReceived
        {
            add { }
            remove { }
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("ETW start failed");
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}

