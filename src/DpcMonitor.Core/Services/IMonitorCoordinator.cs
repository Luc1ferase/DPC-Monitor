using DpcMonitor.Core.Models;

namespace DpcMonitor.Core.Services;

public interface IMonitorCoordinator
{
    event EventHandler<MonitorStatusSnapshot>? SnapshotPublished;

    Task<MonitorStatusSnapshot> StartAsync(double thresholdUs, CancellationToken cancellationToken = default);

    Task<MonitorStatusSnapshot> StopAsync(CancellationToken cancellationToken = default);
}
