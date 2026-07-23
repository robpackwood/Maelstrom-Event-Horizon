using System.Windows;
using System.Windows.Media;

namespace MaelstromEventHorizon.Presentation.Drawing;

internal static class DrawingContextArcExtensions
{
    public static void DrawArc(
        this DrawingContext dc,
        Pen pen,
        Point center,
        double radius,
        double startDegrees,
        double sweepDegrees)
    {
        double start = startDegrees * Math.PI / 180;
        double end = (startDegrees + sweepDegrees) * Math.PI / 180;
        Point p0 = new(center.X + Math.Cos(start) * radius, center.Y + Math.Sin(start) * radius);
        Point p1 = new(center.X + Math.Cos(end) * radius, center.Y + Math.Sin(end) * radius);
        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(p0, false, false);
            context.ArcTo(p1, new Size(radius, radius), 0, Math.Abs(sweepDegrees) > 180,
                sweepDegrees >= 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise, true, false);
        }
        dc.DrawGeometry(null, pen, geometry);
    }
}
