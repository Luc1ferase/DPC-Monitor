using System.Collections.ObjectModel;
using System.Windows.Input;
using DpcMonitor.Core.Commands;
using DpcMonitor.Core.Models;
using DpcMonitor.Core.Services;

namespace DpcMonitor.Core.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IMonitorCoordinator _coordinator;
    private readonly Action<Action> _dispatch;
    private readonly Func<Task>? _restartElevatedAction;
    private readonly SessionLogBuffer _logBuffer = new();

    private string _currentText = "0 us";
    private string _peakText = "0 us";
    private string _averageText = "0 us";
    private string _statusMessage = "Idle";
    private double _thresholdUs = 300;
    private bool _isRunning;
    private bool _isAlerting;

    public MainWindowViewModel(IMonitorCoordinator coordinator, Action<Action>? dispatch = null, Func<Task>? restartElevatedAction = null)
    {
        _coordinator = coordinator;
        _dispatch = dispatch ?? (action => action());
        _restartElevatedAction = restartElevatedAction;
        ChartBuckets = new ObservableCollection<DpcLatencyBucket>();
        LogEntries = new ObservableCollection<string>();
        StartCommand = new RelayCommand(() => _ = StartAsync(), () => !IsRunning);
        StopCommand = new RelayCommand(() => _ = StopAsync(), () => IsRunning);
        RestartElevatedCommand = new RelayCommand(() => _ = RestartElevatedAsync(), () => _restartElevatedAction is not null);
        _coordinator.SnapshotPublished += OnSnapshotPublished;
    }

    public ObservableCollection<DpcLatencyBucket> ChartBuckets { get; }

    public ObservableCollection<string> LogEntries { get; }

    public ICommand StartCommand { get; }

    public ICommand StopCommand { get; }

    public ICommand RestartElevatedCommand { get; }

    public string CurrentText
    {
        get => _currentText;
        private set => SetProperty(ref _currentText, value);
    }

    public string PeakText
    {
        get => _peakText;
        private set => SetProperty(ref _peakText, value);
    }

    public string AverageText
    {
        get => _averageText;
        private set => SetProperty(ref _averageText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public double ThresholdUs
    {
        get => _thresholdUs;
        set => SetProperty(ref _thresholdUs, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set => SetProperty(ref _isRunning, value);
    }

    public bool IsAlerting
    {
        get => _isAlerting;
        private set => SetProperty(ref _isAlerting, value);
    }

    public async Task StartAsync()
    {
        ApplySnapshot(await _coordinator.StartAsync(ThresholdUs));
    }

    public async Task StopAsync()
    {
        ApplySnapshot(await _coordinator.StopAsync());
    }

    private async Task RestartElevatedAsync()
    {
        if (_restartElevatedAction is not null)
        {
            await _restartElevatedAction();
        }
    }

    private void OnSnapshotPublished(object? sender, MonitorStatusSnapshot snapshot)
    {
        _dispatch(() => ApplySnapshot(snapshot));
    }

    private void ApplySnapshot(MonitorStatusSnapshot snapshot)
    {
        CurrentText = FormatUs(snapshot.CurrentUs);
        PeakText = FormatUs(snapshot.PeakUs);
        AverageText = FormatUs(snapshot.AverageUs);
        ThresholdUs = snapshot.ThresholdUs;
        StatusMessage = snapshot.StatusMessage;
        IsAlerting = snapshot.State == MonitorState.Alerting;
        IsRunning = snapshot.State is MonitorState.Running or MonitorState.Alerting;

        ChartBuckets.Clear();
        foreach (var bucket in snapshot.Chart)
        {
            ChartBuckets.Add(bucket);
        }

        if (!string.IsNullOrWhiteSpace(snapshot.StatusMessage))
        {
            _logBuffer.Append(snapshot.StatusMessage);
            SyncLogs();
        }

        RaiseCommandState();
    }

    private void SyncLogs()
    {
        LogEntries.Clear();
        foreach (var entry in _logBuffer.Entries)
        {
            LogEntries.Add(entry);
        }
    }

    private void RaiseCommandState()
    {
        if (StartCommand is RelayCommand start)
        {
            start.RaiseCanExecuteChanged();
        }

        if (StopCommand is RelayCommand stop)
        {
            stop.RaiseCanExecuteChanged();
        }

        if (RestartElevatedCommand is RelayCommand restart)
        {
            restart.RaiseCanExecuteChanged();
        }
    }

    private static string FormatUs(double value) => $"{value:0.##} us";
}
