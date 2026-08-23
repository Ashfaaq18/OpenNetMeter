using System;
using System.Collections.Generic;

namespace OpenNetMeter.Views.Controls.Charting;

public readonly record struct AxisTicks(double Top, IReadOnlyList<double> Values);

public static class AxisScale
{
    // Below this, Math.Pow(10, floor(log10(x))) starts losing precision as it approaches the
    // denormal range, and at x <= double.Epsilon it underflows to exactly 0 - which NextNiceStep
    // can never step away from, spinning the loop below forever. Comfortably normal range instead.
    private const double MinSafeMax = 1e-6;
    private const int MaxStepIterations = 64;

    /// <summary>Computes 0..Top in even 1/2/5 x 10^n steps, with Top always >= max.</summary>
    public static AxisTicks Compute(double max, int desiredIntervals)
    {
        if (desiredIntervals < 1) desiredIntervals = 1;
        double safeMax = max > MinSafeMax ? max : MinSafeMax;

        double rawStep = safeMax / desiredIntervals;
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        double normalized = rawStep / magnitude;
        double step = normalized <= 1 ? magnitude
            : normalized <= 2 ? 2 * magnitude
            : normalized <= 5 ? 5 * magnitude
            : 10 * magnitude;

        double top = step * desiredIntervals;
        for (int i = 0; top < safeMax && i < MaxStepIterations; i++)
        {
            step = NextNiceStep(step);
            top = step * desiredIntervals;
        }

        var values = new double[desiredIntervals + 1];
        for (int i = 0; i <= desiredIntervals; i++)
            values[i] = step * i;

        return new AxisTicks(top, values);
    }

    private static double NextNiceStep(double step)
    {
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(step)));
        double normalized = Math.Round(step / magnitude, 6);
        if (normalized < 2) return 2 * magnitude;
        if (normalized < 5) return 5 * magnitude;
        return 10 * magnitude;
    }
}
