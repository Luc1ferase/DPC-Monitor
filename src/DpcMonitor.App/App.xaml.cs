using System.Diagnostics;
using System.Windows;
using DpcMonitor.Core.Services;
using DpcMonitor.Core.ViewModels;
using DpcMonitor.Etw;

namespace DpcMonitor.App;

public partial class App : Application
{
    private EtwDpcEventSource? _eventSource;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var privilegeService = new WindowsPrivilegeService();
        _eventSource = new EtwDpcEventSource();
        var coordinator = new MonitorCoordinator(_eventSource, privilegeService, new SystemLatencyClock());
        var viewModel = new MainWindowViewModel(
            coordinator,
            action => Dispatcher.Invoke(action),
            RestartElevatedAsync);

        var window = new MainWindow(viewModel);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _eventSource?.Dispose();
        base.OnExit(e);
    }

    private Task RestartElevatedAsync()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return Task.CompletedTask;
        }

        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = AppContext.BaseDirectory
        };

        Process.Start(startInfo);
        Shutdown();
        return Task.CompletedTask;
    }

    private sealed class SystemLatencyClock : ILatencyClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
