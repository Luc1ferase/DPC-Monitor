using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using DpcMonitor.Core.Models;
using DpcMonitor.Core.ViewModels;

namespace DpcMonitor.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.ChartBuckets.CollectionChanged += ChartBuckets_CollectionChanged;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        Loaded += (_, _) => RedrawChart();
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _viewModel.ChartBuckets.CollectionChanged -= ChartBuckets_CollectionChanged;
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
    }

    private void ChartCanvas_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RedrawChart();
    }

    private void ChartBuckets_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RedrawChart();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.ThresholdUs) or nameof(MainWindowViewModel.IsAlerting) or nameof(MainWindowViewModel.StatusMessage))
        {
            RedrawChart();
            UpdateStatusChrome();
        }
    }

    private void UpdateStatusChrome()
    {
        if (_viewModel.IsAlerting)
        {
            CurrentCard.Background = (Brush)FindResource("AlertMutedBrush");
            StatusBadge.Background = (Brush)FindResource("AlertMutedBrush");
        }
        else
        {
            CurrentCard.Background = (Brush)FindResource("CardBrush");
            StatusBadge.Background = (Brush)FindResource("AccentMutedBrush");
        }
    }

    private void RedrawChart()
    {
        UpdateStatusChrome();

        var width = ChartCanvas.ActualWidth;
        var height = ChartCanvas.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        const double paddingX = 16;
        const double paddingY = 18;
        var plotWidth = Math.Max(1, width - paddingX * 2);
        var plotHeight = Math.Max(1, height - paddingY * 2);
        var buckets = _viewModel.ChartBuckets.ToArray();
        var maxCurrent = buckets.Length == 0 ? 0 : buckets.Max(bucket => bucket.CurrentUs);
        var maxUs = Math.Max(100, Math.Max(_viewModel.ThresholdUs, maxCurrent) * 1.15);

        var points = new PointCollection();
        if (buckets.Length == 1)
        {
            points.Add(new Point(paddingX + plotWidth / 2, ToY(buckets[0].CurrentUs, maxUs, plotHeight, paddingY)));
        }
        else if (buckets.Length > 1)
        {
            var step = plotWidth / (buckets.Length - 1);
            for (var index = 0; index < buckets.Length; index++)
            {
                points.Add(new Point(
                    paddingX + step * index,
                    ToY(buckets[index].CurrentUs, maxUs, plotHeight, paddingY)));
            }
        }

        LatencyPolyline.Points = points;
        ThresholdLine.X1 = paddingX;
        ThresholdLine.X2 = paddingX + plotWidth;
        ThresholdLine.Y1 = ToY(_viewModel.ThresholdUs, maxUs, plotHeight, paddingY);
        ThresholdLine.Y2 = ThresholdLine.Y1;
    }

    private static double ToY(double valueUs, double maxUs, double plotHeight, double paddingY)
    {
        var normalized = maxUs <= 0 ? 0 : Math.Clamp(valueUs / maxUs, 0, 1);
        return paddingY + plotHeight * (1 - normalized);
    }
}
