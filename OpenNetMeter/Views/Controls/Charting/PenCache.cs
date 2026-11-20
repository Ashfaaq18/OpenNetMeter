using System;
using Avalonia.Media;

namespace OpenNetMeter.Views.Controls.Charting;

/// <summary>
/// One reusable <see cref="Pen"/> per slot, rebuilt only when the brush instance, thickness, cap or join it was
/// built with changes, so a chart that redraws every frame doesn't allocate its pens every frame.
/// </summary>
internal sealed class PenCache
{
    private readonly Entry[] entries;

    public PenCache(int slots)
    {
        entries = new Entry[slots];
    }

    /// <summary>How many pens have been built so far. For tests.</summary>
    public int BuildCount { get; private set; }

    public Pen Get(int slot, IBrush brush, double thickness, PenLineCap lineCap = PenLineCap.Flat, PenLineJoin lineJoin = PenLineJoin.Miter)
    {
        ref var entry = ref entries[slot];
        if (entry.Pen is { } pen &&
            ReferenceEquals(entry.Brush, brush) &&
            entry.Thickness == thickness &&
            entry.LineCap == lineCap &&
            entry.LineJoin == lineJoin)
        {
            return pen;
        }

        entry = new Entry(brush, thickness, lineCap, lineJoin, new Pen(brush, thickness, null, lineCap, lineJoin));
        BuildCount++;
        return entry.Pen!;
    }

    private readonly record struct Entry(IBrush? Brush, double Thickness, PenLineCap LineCap, PenLineJoin LineJoin, Pen? Pen);
}
