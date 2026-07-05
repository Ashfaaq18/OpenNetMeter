
using System.Collections.ObjectModel;
using System.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

public sealed class Graph
{
    private const int WindowSize = 35;
    public ISeries[] GraphSeries { get; }
    public Axis[] GraphXAxes { get; }
    public Axis[] GraphYAxes { get; private set; }
    // Match WPF dark theme accents:
    // Download -> #367061, Upload -> #D98868
    SKColor dlColor = new SKColor(0x4A, 0xA9, 0x8C);
    SKColor ulColor = new SKColor(0xE1, 0x77, 0x17);
    private readonly ObservableCollection<ObservablePoint> dlValues = new();
    private readonly ObservableCollection<ObservablePoint> ulValues = new();
    private int tickCount;
    public Graph()
    {
        GraphSeries =
        [
            new LineSeries<ObservablePoint>
            {
                Values = dlValues,
                Stroke = new SolidColorPaint(dlColor, 2),
                GeometrySize = 0,
                GeometryStroke = null,
                GeometryFill = null,
                Fill = new SolidColorPaint(dlColor.WithAlpha(0x33)),
                LineSmoothness = 0.3,
                Name = "Download"
            },
            new LineSeries<ObservablePoint>
            {
                Values = ulValues,
                Stroke = new SolidColorPaint(ulColor, 2),
                GeometrySize = 0,
                GeometryStroke = null,
                GeometryFill = null,
                Fill = new SolidColorPaint(ulColor.WithAlpha(0x33)),
                LineSmoothness = 0.3,
                Name = "Upload"
            }
        ];

        GraphXAxes =
        [
            new Axis
            {
                ShowSeparatorLines = false,
                IsVisible = false,
                MinLimit = 0,
                MaxLimit = WindowSize
            }
        ];

        GraphYAxes = CreateGraphYAxes();
    }

    public Axis[] CreateGraphYAxes()
    {
        return 
        [
            new Axis
            {
                MinLimit = 0,
                ShowSeparatorLines = true,
                SeparatorsPaint = new SolidColorPaint(new SKColor(0x55, 0x55, 0x55)) { StrokeThickness = 1 },
                LabelsPaint = new SolidColorPaint(new SKColor(0xA9, 0xAB, 0xAB)),
                TextSize = 10,
                Labeler = value => OpenNetMeter.ViewModels.SummaryViewModel.FormatSpeed((long)value) + "/s"
            }
        ];
    }

    public void ClearOnDisconnect()
    {
        dlValues.Clear();
        ulValues.Clear();
        tickCount = 0;
        GraphXAxes[0].MinLimit = 0;
        GraphXAxes[0].MaxLimit = WindowSize;
    }

    public void RefreshSpeedDisplayFormat()
    {
        GraphYAxes = CreateGraphYAxes();
        OnPropertyChanged(nameof(GraphYAxes));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void AppendGraphPoint(long downloadBytes, long uploadBytes)
    {
        dlValues.Add(new ObservablePoint(tickCount, downloadBytes));
        ulValues.Add(new ObservablePoint(tickCount, uploadBytes));

        while (dlValues.Count > WindowSize)
            dlValues.RemoveAt(0);
        while (ulValues.Count > WindowSize)
            ulValues.RemoveAt(0);

        if (tickCount >= WindowSize)
        {
            GraphXAxes[0].MinLimit = tickCount - WindowSize;
            GraphXAxes[0].MaxLimit = tickCount;
        }

        tickCount++;
    }
    
}