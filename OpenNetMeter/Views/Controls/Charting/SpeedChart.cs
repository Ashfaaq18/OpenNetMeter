using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using OpenNetMeter.ViewModels;

namespace OpenNetMeter.Views.Controls.Charting;

/// <summary>Custom-drawn network speed graph: two smoothed, gradient-filled
/// lines on a nice-tick Y axis, with a continuous scroll
public sealed class SpeedChart : Control
{
    private const long MinFloorBytesPerSecond = 1024;
    private const int DesiredIntervals = 2;
    private const double MaxEaseDurationMs = 350;
    private const double TickIntervalMs = 1000.0;
    private const double CurveSmoothness = 0.8;
    private const double GutterWidth = 46;
    private const double TopPadding = 6;
    private const double BottomPadding = 16;
    private const double RightPadding = 6;

    public static readonly StyledProperty<SpeedHistory?> HistoryProperty =
        AvaloniaProperty.Register<SpeedChart, SpeedHistory?>(nameof(History));

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<SpeedChart, IBrush?>(nameof(Background));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<SpeedChart, CornerRadius>(nameof(CornerRadius));

    public static readonly StyledProperty<IBrush?> DownloadBrushProperty =
        AvaloniaProperty.Register<SpeedChart, IBrush?>(nameof(DownloadBrush));

    public static readonly StyledProperty<IBrush?> UploadBrushProperty =
        AvaloniaProperty.Register<SpeedChart, IBrush?>(nameof(UploadBrush));

    public static readonly StyledProperty<double> FillOpacityProperty =
        AvaloniaProperty.Register<SpeedChart, double>(nameof(FillOpacity), 0.38);

    public static readonly StyledProperty<IBrush?> GridLineBrushProperty =
        AvaloniaProperty.Register<SpeedChart, IBrush?>(nameof(GridLineBrush));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<SpeedChart, IBrush?>(nameof(LabelBrush));

    public static readonly StyledProperty<string> AxisNameProperty =
        AvaloniaProperty.Register<SpeedChart, string>(nameof(AxisName), "Network Speed");

    public static readonly StyledProperty<double> LabelFontSizeProperty =
        AvaloniaProperty.Register<SpeedChart, double>(nameof(LabelFontSize), 10);

    public static readonly StyledProperty<double> AxisNameFontSizeProperty =
        AvaloniaProperty.Register<SpeedChart, double>(nameof(AxisNameFontSize), 12);

    public static readonly StyledProperty<bool> IsAnimatedProperty =
        AvaloniaProperty.Register<SpeedChart, bool>(nameof(IsAnimated), true);

    static SpeedChart()
    {
        AffectsRender<SpeedChart>(
            HistoryProperty, BackgroundProperty, CornerRadiusProperty,
            DownloadBrushProperty, UploadBrushProperty, FillOpacityProperty,
            GridLineBrushProperty, LabelBrushProperty, AxisNameProperty,
            LabelFontSizeProperty, AxisNameFontSizeProperty);
    }

    private Window? hostWindow;
    private bool frameRequested;
    private long lastSampleTimestampMs;
    private double renderedMaxBytes;
    private double maxAtTransitionStart;
    private double targetMaxBytes;
    private long maxTransitionStartMs;
    private double[] tickBytesCache = Array.Empty<double>();
    private string[] labelsCache = Array.Empty<string>();
    private IBrush? downloadFillBrush;
    private IBrush? uploadFillBrush;

    public SpeedHistory? History
    {
        get => GetValue(HistoryProperty);
        set => SetValue(HistoryProperty, value);
    }

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public IBrush? DownloadBrush
    {
        get => GetValue(DownloadBrushProperty);
        set => SetValue(DownloadBrushProperty, value);
    }

    public IBrush? UploadBrush
    {
        get => GetValue(UploadBrushProperty);
        set => SetValue(UploadBrushProperty, value);
    }

    public double FillOpacity
    {
        get => GetValue(FillOpacityProperty);
        set => SetValue(FillOpacityProperty, value);
    }

    public IBrush? GridLineBrush
    {
        get => GetValue(GridLineBrushProperty);
        set => SetValue(GridLineBrushProperty, value);
    }

    public IBrush? LabelBrush
    {
        get => GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    public string AxisName
    {
        get => GetValue(AxisNameProperty);
        set => SetValue(AxisNameProperty, value);
    }

    public double LabelFontSize
    {
        get => GetValue(LabelFontSizeProperty);
        set => SetValue(LabelFontSizeProperty, value);
    }

    public double AxisNameFontSize
    {
        get => GetValue(AxisNameFontSizeProperty);
        set => SetValue(AxisNameFontSizeProperty, value);
    }

    public bool IsAnimated
    {
        get => GetValue(IsAnimatedProperty);
        set => SetValue(IsAnimatedProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == HistoryProperty)
        {
            if (change.OldValue is SpeedHistory oldHistory)
            {
                oldHistory.SampleAppended -= OnHistoryChanged;
                oldHistory.Cleared -= OnHistoryCleared;
                oldHistory.DisplayFormatChanged -= OnHistoryChanged;
            }

            if (change.NewValue is SpeedHistory newHistory)
            {
                newHistory.SampleAppended += OnHistoryChanged;
                newHistory.Cleared += OnHistoryCleared;
                newHistory.DisplayFormatChanged += OnHistoryChanged;
            }

            ResetAnimationState();
        }
        else if (change.Property == DownloadBrushProperty || change.Property == FillOpacityProperty)
        {
            downloadFillBrush = null;
        }
        else if (change.Property == UploadBrushProperty || change.Property == FillOpacityProperty)
        {
            uploadFillBrush = null;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        hostWindow = TopLevel.GetTopLevel(this) as Window;
        if (hostWindow is not null)
            hostWindow.PropertyChanged += OnHostWindowPropertyChanged;

        ResetAnimationState();
        RequestFrame();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (hostWindow is not null)
        {
            hostWindow.PropertyChanged -= OnHostWindowPropertyChanged;
            hostWindow = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnHostWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty)
            RequestFrame();
    }

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        lastSampleTimestampMs = Environment.TickCount64;
        RecomputeTargets();
        RequestFrame();
    }

    private void OnHistoryCleared(object? sender, EventArgs e)
    {
        ResetAnimationState();
        InvalidateVisual();
    }

    private void ResetAnimationState()
    {
        lastSampleTimestampMs = Environment.TickCount64;
        RecomputeTargets();
        renderedMaxBytes = targetMaxBytes;
        maxAtTransitionStart = targetMaxBytes;
        maxTransitionStartMs = Environment.TickCount64;
    }

    private void RecomputeTargets()
    {
        var history = History;
        long maxBytes = history is { Count: > 0 }
            ? Math.Max(history.MaxInWindow(SpeedHistory.VisibleSamples), MinFloorBytesPerSecond)
            : MinFloorBytesPerSecond;

        var (topBytes, tickBytes, labels) = ComputeAxis(maxBytes);

        if (tickBytesCache.Length == 0 || Math.Abs(topBytes - targetMaxBytes) > 0.5)
        {
            maxAtTransitionStart = renderedMaxBytes;
            targetMaxBytes = topBytes;
            maxTransitionStartMs = Environment.TickCount64;
        }

        tickBytesCache = tickBytes;
        labelsCache = labels;
    }

    private static (double topBytes, double[] tickBytes, string[] labels) ComputeAxis(long maxBytesPerSecond)
    {
        var settings = Properties.SettingsManager.Current;
        bool useBytes = settings.NetworkSpeedFormat != 0;
        var magnitude = NetworkSpeed.NormalizeMagnitude(settings.NetworkSpeedMagnitude);

        double signedValue = useBytes ? maxBytesPerSecond : maxBytesPerSecond * 8.0;
        var (_, mag) = NetworkSpeed.GetAdjustedSize((long)signedValue, magnitude);

        double divisor = 1L << (mag * 10);
        double maxDisplay = signedValue / divisor;

        var ticks = AxisScale.Compute(maxDisplay, DesiredIntervals);

        var tickBytes = new double[ticks.Values.Count];
        var labels = new string[ticks.Values.Count];
        for (int i = 0; i < ticks.Values.Count; i++)
        {
            double rawValue = ticks.Values[i] * divisor;
            long bytesPerSecond = (long)Math.Round(useBytes ? rawValue : rawValue / 8.0);
            tickBytes[i] = bytesPerSecond;
            labels[i] = SummaryViewModel.FormatSpeed(bytesPerSecond);
        }

        double topBytes = ticks.Top * divisor / (useBytes ? 1.0 : 8.0);
        return (topBytes, tickBytes, labels);
    }

    private void RequestFrame()
    {
        if (frameRequested)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        frameRequested = true;
        topLevel.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan _)
    {
        frameRequested = false;
        if (!CanAnimate())
            return;

        InvalidateVisual();
        RequestFrame();
    }

    private bool CanAnimate() =>
        IsAnimated &&
        IsEffectivelyVisible &&
        hostWindow is not { WindowState: WindowState.Minimized };

    private static double Smoothstep(double p) => p * p * (3 - 2 * p);

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 1 || bounds.Height <= 1)
            return;

        var background = Background;
        if (background is not null)
            context.DrawRectangle(background, null, new RoundedRect(bounds, CornerRadius));

        var gridBrush = GridLineBrush;
        var labelBrush = LabelBrush;
        var typeface = Typeface.Default;
        double labelFontSize = LabelFontSize;

        FormattedText? nameText = null;
        double nameBandHeight = 0;
        if (labelBrush is not null && !string.IsNullOrEmpty(AxisName))
        {
            nameText = new FormattedText(AxisName, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, AxisNameFontSize, labelBrush);
            nameBandHeight = nameText.Height + 2;
        }

        var plotRect = new Rect(
            bounds.X + GutterWidth,
            bounds.Y + nameBandHeight + TopPadding,
            Math.Max(0, bounds.Width - GutterWidth - RightPadding),
            Math.Max(0, bounds.Height - nameBandHeight - TopPadding - BottomPadding));

        if (plotRect.Width <= 0 || plotRect.Height <= 0)
            return;

        UpdateMaxEasing();

        if (nameText is not null)
        {
            double nameX = bounds.X + (bounds.Width - nameText.Width) / 2;
            double nameY = bounds.Y + (nameBandHeight - nameText.Height) / 2;
            context.DrawText(nameText, new Point(nameX, nameY));
        }

        for (int i = 0; i < tickBytesCache.Length; i++)
        {
            double y = ValueToY(tickBytesCache[i], plotRect);

            if (gridBrush is not null)
                context.DrawLine(new Pen(gridBrush, 1), new Point(plotRect.X, y), new Point(plotRect.Right, y));

            if (labelBrush is not null && i < labelsCache.Length)
            {
                var text = new FormattedText(labelsCache[i], CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, labelFontSize, labelBrush);
                double textY = Math.Clamp(y - text.Height / 2, bounds.Y, Math.Max(bounds.Y, bounds.Bottom - text.Height));
                context.DrawText(text, new Point(GutterWidth - 6 - text.Width, textY));
            }
        }

        var history = History;
        if (history is null || history.Count == 0)
            return;

        double slot = plotRect.Width / (SpeedHistory.VisibleSamples - 1);
        double shiftPixels = ComputeShiftPixels(slot);

        using (context.PushClip(plotRect))
        {
            DrawSeries(context, history, plotRect, slot, shiftPixels, isDownload: true);
            DrawSeries(context, history, plotRect, slot, shiftPixels, isDownload: false);
        }
    }

    private double ComputeShiftPixels(double slot)
    {
        long elapsedMs = Environment.TickCount64 - lastSampleTimestampMs;
        double progress = Math.Clamp(elapsedMs / TickIntervalMs, 0.0, 1.0);
        return slot * (1 - Smoothstep(progress));
    }

    private void UpdateMaxEasing()
    {
        long elapsedMs = Environment.TickCount64 - maxTransitionStartMs;
        double progress = Math.Clamp(elapsedMs / MaxEaseDurationMs, 0.0, 1.0);
        renderedMaxBytes = maxAtTransitionStart + (targetMaxBytes - maxAtTransitionStart) * Smoothstep(progress);
    }

    private double ValueToY(double bytesPerSecond, Rect plotRect)
    {
        double ratio = renderedMaxBytes > 0 ? bytesPerSecond / renderedMaxBytes : 0;
        return plotRect.Bottom - ratio * plotRect.Height;
    }

    private void DrawSeries(DrawingContext context, SpeedHistory history, Rect plotRect, double slot, double shiftPixels, bool isDownload)
    {
        var strokeBrush = isDownload ? DownloadBrush : UploadBrush;
        if (strokeBrush is null)
            return;

        int windowSize = Math.Min(history.Count, SpeedHistory.VisibleSamples + 1);
        int startIndex = history.Count - windowSize;

        var points = new Point[windowSize];
        for (int i = 0; i < windowSize; i++)
        {
            var sample = history[startIndex + i];
            long value = isDownload ? sample.DownloadBytesPerSecond : sample.UploadBytesPerSecond;
            int distanceFromNewest = (history.Count - 1) - (startIndex + i);
            double x = plotRect.Right - distanceFromNewest * slot + shiftPixels;
            double y = ValueToY(value, plotRect);
            points[i] = new Point(x, y);
        }

        double baselineY = plotRect.Bottom;
        var fillBrush = GetOrBuildFillBrush(isDownload, strokeBrush);
        if (fillBrush is not null)
        {
            var fillGeometry = CurveBuilder.BuildFill(points, CurveSmoothness, baselineY);
            context.DrawGeometry(fillBrush, null, fillGeometry);
        }

        var strokeGeometry = CurveBuilder.BuildStroke(points, CurveSmoothness);
        var pen = new Pen(strokeBrush, 2, null, PenLineCap.Round, PenLineJoin.Round);
        context.DrawGeometry(null, pen, strokeGeometry);
    }

    private IBrush? GetOrBuildFillBrush(bool isDownload, IBrush strokeBrush)
    {
        if (isDownload)
            return downloadFillBrush ??= BuildFillBrush(strokeBrush);
        return uploadFillBrush ??= BuildFillBrush(strokeBrush);
    }

    private IBrush? BuildFillBrush(IBrush strokeBrush)
    {
        if (strokeBrush is not ISolidColorBrush solid)
            return null;

        var color = solid.Color;
        var top = Color.FromArgb((byte)Math.Round(Math.Clamp(FillOpacity, 0, 1) * 255), color.R, color.G, color.B);
        var bottom = Color.FromArgb(0, color.R, color.G, color.B);

        var gradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative)
        };
        gradient.GradientStops.Add(new GradientStop(top, 0));
        gradient.GradientStops.Add(new GradientStop(bottom, 1));
        return gradient;
    }
}
