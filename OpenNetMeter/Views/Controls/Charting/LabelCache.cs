using System;

namespace OpenNetMeter.Views.Controls.Charting;

/// <summary>
/// Keeps one built value (in practice a <c>FormattedText</c>) per label slot, so a chart that redraws every frame
/// shapes each label once instead of once per frame. A slot's value is rebuilt only when its text changes or when
/// the shared style (typeface, size, brush, culture) no longer equals the one it was built with; a style change
/// drops every slot.
/// </summary>
internal sealed class LabelCache<TStyle, TValue>
    where TStyle : IEquatable<TStyle>
    where TValue : class
{
    private readonly Func<string, TStyle, TValue> build;
    private TStyle? style;
    private bool hasStyle;
    private string?[] texts = [];
    private TValue?[] values = [];

    public LabelCache(Func<string, TStyle, TValue> build)
    {
        this.build = build;
    }

    /// <summary>How many values have been built so far. For tests.</summary>
    public int BuildCount { get; private set; }

    public TValue Get(int slot, string text, TStyle currentStyle)
    {
        if (!hasStyle || !currentStyle.Equals(style))
        {
            Array.Clear(texts);
            Array.Clear(values);
            style = currentStyle;
            hasStyle = true;
        }

        if (slot >= values.Length)
        {
            Array.Resize(ref texts, slot + 1);
            Array.Resize(ref values, slot + 1);
        }

        if (values[slot] is { } cached && string.Equals(texts[slot], text, StringComparison.Ordinal))
            return cached;

        var built = build(text, currentStyle);
        texts[slot] = text;
        values[slot] = built;
        BuildCount++;
        return built;
    }

    /// <summary>Forgets everything built so far.</summary>
    public void Clear()
    {
        Array.Clear(texts);
        Array.Clear(values);
        hasStyle = false;
        style = default;
    }
}
