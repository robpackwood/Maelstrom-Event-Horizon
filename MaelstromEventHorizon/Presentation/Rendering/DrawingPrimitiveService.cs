using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Math;
using MaelstromEventHorizon.Domain.Scores;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace MaelstromEventHorizon.Presentation.Rendering;

internal sealed class DrawingPrimitiveService
{
    internal void DrawHighScores(GameView view, DrawingContext dc)
    {
        var label = new SolidColorBrush(Color.FromRgb(123, 163, 187));
        DrawText(dc, "RANK", 393, 222, 11, label, FontWeights.SemiBold);
        DrawText(dc, "PILOT", 475, 222, 11, label, FontWeights.SemiBold);
        DrawText(dc, "SCORE", 687, 222, 11, label, FontWeights.SemiBold);
        DrawText(dc, "WAVE", 833, 222, 11, label, FontWeights.SemiBold);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(110, 79, 153, 184)), 1), new Point(388, 232), new Point(892, 232));

        for (int i = 0; i < 10; i++)
        {
            double baseline = 263 + i * 30;
            bool highlighted = i == view.Game.HighlightedHighScoreIndex;
            if (highlighted)
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(82, 255, 211, 83)),
                    new Pen(new SolidColorBrush(Color.FromArgb(160, 255, 225, 119)), 1),
                    new Rect(384, baseline - 20, 512, 26), 3, 3);
            Brush rowBrush = new SolidColorBrush(highlighted
                ? Color.FromRgb(255, 225, 112)
                : Color.FromRgb(174, 203, 216));
            FontWeight weight = highlighted ? FontWeights.Bold : FontWeights.SemiBold;
            if (i < view.Game.HighScores.Count)
            {
                HighScoreEntry entry = view.Game.HighScores[i];
                DrawText(dc, $"{i + 1:00}", 400, baseline, 14, rowBrush, weight);
                DrawText(dc, entry.Name, 475, baseline, 14, rowBrush, weight);
                DrawText(dc, Money(entry.Score), 687, baseline, 14, rowBrush, weight);
                DrawText(dc, entry.Wave.ToString("00"), 846, baseline, 14, rowBrush, weight);
            }
            else
            {
                DrawText(dc, $"{i + 1:00}", 400, baseline, 14, new SolidColorBrush(Color.FromRgb(70, 92, 107)), FontWeights.SemiBold);
                DrawText(dc, "---", 475, baseline, 14, new SolidColorBrush(Color.FromRgb(70, 92, 107)), FontWeights.Normal);
            }
        }
    }

    internal Geometry ShipGeometry(double expand)
        => Polygon((27 + expand, 0), (5, -8), (-14 - expand, -16 - expand), (-18 - expand, -8),
            (-9, 0), (-18 - expand, 8), (-14 - expand, 16 + expand), (5, 8));

    internal Geometry ShipDebrisGeometry(int kind) => kind switch
    {
        0 => Polygon((27, 0), (5, -8), (0, 0), (5, 8)),
        1 => Polygon((4, -7), (-14, -16), (-18, -8), (-9, 0), (0, 0)),
        2 => Polygon((0, 0), (-9, 0), (-18, 8), (-14, 16), (4, 7)),
        3 => Polygon((-17, -7), (-8, -4), (-8, 4), (-17, 7)),
        _ => Polygon((-5, -6), (10, 0), (-5, 6), (0, 0))
    };

    internal Geometry AsteroidGeometry(Asteroid rock)
    {
        int count = rock.Size == 3 ? 13 : rock.Size == 2 ? 11 : 9;
        var points = new (double x, double y)[count];
        for (int i = 0; i < count; i++)
        {
            double a = i * Math.PI * 2 / count;
            double noise = .78 + .25 * Hash(rock.Seed, i);
            points[i] = (Math.Cos(a) * rock.Radius * noise, Math.Sin(a) * rock.Radius * noise);
        }
        return Polygon(points);
    }

    internal double Hash(int seed, int index)
    {
        double x = Math.Sin(seed * .000013 + index * 78.233) * 43758.5453;
        return x - Math.Floor(x);
    }

    internal Geometry RegularPolygon(int sides, double radius, double offset)
    {
        var points = new (double x, double y)[sides];
        for (int i = 0; i < sides; i++)
        {
            double a = offset + i * Math.PI * 2 / sides;
            points[i] = (Math.Cos(a) * radius, Math.Sin(a) * radius);
        }
        return Polygon(points);
    }

    internal StreamGeometry Polygon(params (double x, double y)[] points)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(points[0].x, points[0].y), true, true);
        for (int i = 1; i < points.Length; i++) context.LineTo(new Point(points[i].x, points[i].y), true, false);
        geometry.Freeze();
        return geometry;
    }

    internal void DrawGlowGeometry(DrawingContext dc, Geometry geometry, Color color, double width)
    {
        for (int i = 3; i >= 1; i--)
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb((byte)(20 + i * 8), color.R, color.G, color.B)), width * i), geometry);
    }

    internal void DrawGlowEllipse(DrawingContext dc, V2 center, double radius, Color color, int layers, double intensity)
    {
        for (int i = layers; i >= 1; i--)
        {
            double r = radius + i * 4;
            byte alpha = (byte)(Math.Clamp(intensity, 0, 1) * 55 / i);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)), null, Pt(center), r, r);
        }
    }

    internal void DrawText(DrawingContext dc, string text, double x, double baseline, double size, Brush brush, FontWeight weight)
    {
        var ft = Format(text, size, brush, weight);
        dc.DrawText(ft, new Point(x, baseline - ft.Baseline));
    }

    internal void DrawCenteredText(DrawingContext dc, string text, double centerX, double baseline, double size, Brush brush, FontWeight weight)
    {
        var ft = Format(text, size, brush, weight);
        dc.DrawText(ft, new Point(centerX - ft.Width / 2, baseline - ft.Baseline));
    }

    internal FormattedText Format(string text, double size, Brush brush, FontWeight weight)
        => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal), size, brush, 1.0)
        { TextAlignment = TextAlignment.Left };

    internal Point Pt(V2 v) => new(v.X, v.Y);
    internal string Money(int value) => value.ToString("$#,0", CultureInfo.InvariantCulture);
    internal double EaseOut(double x) => 1 - Math.Pow(1 - Math.Clamp(x, 0, 1), 3);
    internal Color FromArgb(uint argb, byte? alpha = null)
        => Color.FromArgb(alpha ?? (byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
    internal Color Lighten(Color c, double amount)
        => Color.FromRgb((byte)(c.R + (255 - c.R) * amount), (byte)(c.G + (255 - c.G) * amount), (byte)(c.B + (255 - c.B) * amount));
    internal Color Darken(Color c, double amount)
        => Color.FromRgb((byte)(c.R * (1 - amount)), (byte)(c.G * (1 - amount)), (byte)(c.B * (1 - amount)));
}
