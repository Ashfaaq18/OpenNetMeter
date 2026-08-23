using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace OpenNetMeter.Views.Controls.Charting;

/// <summary>Builds smoothed line/area geometry from pixel-space points using a Catmull-Rom-derived cubic spline.</summary>
internal static class CurveBuilder
{
    public static StreamGeometry BuildStroke(IReadOnlyList<Point> points, double smoothness)
    {
        var geometry = new StreamGeometry();
        if (points.Count < 2)
            return geometry;

        using var ctx = geometry.Open();
        ctx.BeginFigure(points[0], isFilled: false);
        AppendCurve(ctx, points, smoothness, clampBelowY: null);
        ctx.EndFigure(false);
        return geometry;
    }

    public static StreamGeometry BuildFill(IReadOnlyList<Point> points, double smoothness, double baselineY)
    {
        var geometry = new StreamGeometry();
        if (points.Count < 2)
            return geometry;

        using var ctx = geometry.Open();
        ctx.BeginFigure(new Point(points[0].X, baselineY), isFilled: true);
        ctx.LineTo(points[0]);
        AppendCurve(ctx, points, smoothness, clampBelowY: baselineY);
        ctx.LineTo(new Point(points[^1].X, baselineY));
        ctx.EndFigure(isClosed: true);
        return geometry;
    }

    private static void AppendCurve(StreamGeometryContext ctx, IReadOnlyList<Point> points, double smoothness, double? clampBelowY)
    {
        int n = points.Count;
        for (int i = 0; i < n - 1; i++)
        {
            var p0 = points[Math.Max(i - 1, 0)];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = points[Math.Min(i + 2, n - 1)];

            double c1X = p1.X + (p2.X - p0.X) * smoothness / 6;
            double c1Y = p1.Y + (p2.Y - p0.Y) * smoothness / 6;
            double c2X = p2.X - (p3.X - p1.X) * smoothness / 6;
            double c2Y = p2.Y - (p3.Y - p1.Y) * smoothness / 6;

            if (clampBelowY is double baseline)
            {
                c1Y = Math.Min(c1Y, baseline);
                c2Y = Math.Min(c2Y, baseline);
            }

            ctx.CubicBezierTo(new Point(c1X, c1Y), new Point(c2X, c2Y), p2);
        }
    }
}
