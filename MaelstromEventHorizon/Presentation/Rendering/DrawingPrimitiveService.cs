using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Math;
using MaelstromEventHorizon.Domain.Scores;

namespace MaelstromEventHorizon.Presentation.Rendering;

internal sealed class DrawingPrimitiveService
{
    private const int TextCacheCapacity = 512;
    private const int BrushCacheCapacity = 2_048;
    private const int PenCacheCapacity = 512;
    private readonly ConditionalWeakTable<Asteroid, Geometry> asteroidGeometry = [];
    private readonly Dictionary<uint, SolidColorBrush> brushes = [];
    private readonly Queue<uint> brushCacheOrder = [];

    private readonly Dictionary<(string Text, double Size, uint Color, int FontWeight), FormattedText> formattedText =
        [];

    private readonly Queue<(string Text, double Size, uint Color, int FontWeight)> textCacheOrder = [];

    private readonly Dictionary<(uint Color, double Thickness), Pen> pens = [];
    private readonly Queue<(uint Color, double Thickness)> penCacheOrder = [];
    private readonly Dictionary<int, Geometry> shipDebrisGeometry = [];
    private readonly Dictionary<double, Geometry> shipGeometry = [];

    internal void DrawHighScores(GameView view, DrawingContext dc)
    {
        SolidColorBrush label = new(Color.FromRgb(123, 163, 187));
        DrawText(dc, "RANK", 393, 222, 11, label, FontWeights.SemiBold);
        DrawText(dc, "PILOT", 475, 222, 11, label, FontWeights.SemiBold);
        DrawText(dc, "SCORE", 687, 222, 11, label, FontWeights.SemiBold);
        DrawText(dc, "WAVE", 833, 222, 11, label, FontWeights.SemiBold);

        dc.DrawLine(
            new Pen(new SolidColorBrush(Color.FromArgb(110, 79, 153, 184)), 1), new Point(388, 232),
            new Point(892, 232));

        for (int i = 0; i < 10; i++)
        {
            double baseline = 263 + i * 30;
            bool highlighted = i == view.Game.HighlightedHighScoreIndex;

            if (highlighted)
            {
                dc.DrawRoundedRectangle(
                    new SolidColorBrush(Color.FromArgb(82, 255, 211, 83)),
                    new Pen(new SolidColorBrush(Color.FromArgb(160, 255, 225, 119)), 1),
                    new Rect(384, baseline - 20, 512, 26), 3, 3);
            }

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
                DrawText(
                    dc, $"{i + 1:00}", 400, baseline, 14, new SolidColorBrush(Color.FromRgb(70, 92, 107)),
                    FontWeights.SemiBold);

                DrawText(
                    dc, "---", 475, baseline, 14, new SolidColorBrush(Color.FromRgb(70, 92, 107)),
                    FontWeights.Normal);
            }
        }
    }

    internal Geometry ShipGeometry(double expand)
    {
        if (shipGeometry.TryGetValue(expand, out Geometry? geometry))
        {
            return geometry;
        }

        geometry = Polygon(
            (27 + expand, 0), (5, -8), (-14 - expand, -16 - expand), (-18 - expand, -8), (-9, 0), (-18 - expand, 8),
            (-14 - expand, 16 + expand), (5, 8));

        shipGeometry.Add(expand, geometry);
        return geometry;
    }

    internal Geometry ShipDebrisGeometry(int kind)
    {
        if (shipDebrisGeometry.TryGetValue(kind, out Geometry? geometry))
        {
            return geometry;
        }

        geometry = kind switch
        {
            0 => Polygon((27, 0), (5, -8), (0, 0), (5, 8)),
            1 => Polygon((4, -7), (-14, -16), (-18, -8), (-9, 0), (0, 0)),
            2 => Polygon((0, 0), (-9, 0), (-18, 8), (-14, 16), (4, 7)),
            3 => Polygon((-17, -7), (-8, -4), (-8, 4), (-17, 7)),
            4 => Polygon((-5, -6), (10, 0), (-5, 6), (0, 0)),
            5 => Polygon((-18, -15), (-8, -16), (-7, -6), (-17, -4)),
            _ => Polygon((-17, 4), (-7, 6), (-8, 16), (-18, 15))
        };

        shipDebrisGeometry.Add(kind, geometry);
        return geometry;
    }

    internal Geometry AsteroidGeometry(Asteroid rock)
    {
        return asteroidGeometry.GetValue(rock, CreateAsteroidGeometry);
    }

    private Geometry CreateAsteroidGeometry(Asteroid rock)
    {
        int count = rock.Colossal ? 23 : rock.Mega ? 17 : rock.Size == 3 ? 13 : rock.Size == 2 ? 11 : 9;
        (double x, double y)[] points = new (double x, double y)[count];

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
        (double x, double y)[] points = new (double x, double y)[sides];

        for (int i = 0; i < sides; i++)
        {
            double a = offset + i * Math.PI * 2 / sides;
            points[i] = (Math.Cos(a) * radius, Math.Sin(a) * radius);
        }

        return Polygon(points);
    }

    internal StreamGeometry Polygon(params (double x, double y)[] points)
    {
        StreamGeometry geometry = new();
        using StreamGeometryContext context = geometry.Open();
        context.BeginFigure(new Point(points[0].x, points[0].y), true, true);

        for (int i = 1; i < points.Length; i++)
        {
            context.LineTo(new Point(points[i].x, points[i].y), true, false);
        }

        geometry.Freeze();
        return geometry;
    }

    internal void DrawGlowGeometry(DrawingContext dc, Geometry geometry, Color color, double width)
    {
        for (int i = 3; i >= 1; i--)
        {
            dc.DrawGeometry(
                null, Pen(Color.FromArgb((byte)(20 + i * 8), color.R, color.G, color.B), width * i), geometry);
        }
    }

    internal void DrawGlowEllipse(
        GameView view, DrawingContext dc, V2 center, double radius, Color color, int layers, double intensity)
    {
        TransparentEffectBudget.GlowDetail detail = view.TransparentEffects.ReserveGlow(radius, layers);

        for (int i = detail.Layers; i >= 1; i--)
        {
            double r = radius * detail.RadiusScale + i * 4;
            byte alpha = (byte)(Math.Clamp(intensity, 0, 1) * 55 / i);
            dc.DrawEllipse(Brush(Color.FromArgb(alpha, color.R, color.G, color.B)), null, view.Pt(center), r, r);
        }
    }

    internal void DrawText(
        DrawingContext dc, string text, double x, double baseline, double size, Brush brush, FontWeight weight)
    {
        FormattedText ft = Format(text, size, brush, weight);
        dc.DrawText(ft, new Point(x, baseline - ft.Baseline));
    }

    internal void DrawCenteredText(
        DrawingContext dc, string text, double centerX, double baseline, double size, Brush brush, FontWeight weight)
    {
        FormattedText ft = Format(text, size, brush, weight);
        dc.DrawText(ft, new Point(centerX - ft.Width / 2, baseline - ft.Baseline));
    }

    internal FormattedText Format(string text, double size, Brush brush, FontWeight weight)
    {
        if (brush is not SolidColorBrush solid)
        {
            return CreateFormattedText(text, size, brush, weight);
        }

        Color color = QuantizeColor(solid.Color);
        (string text, double size, uint, int) key = (text, size, ColorKey(color), weight.ToOpenTypeWeight());

        if (formattedText.TryGetValue(key, out FormattedText? cached))
        {
            return cached;
        }

        if (formattedText.Count >= TextCacheCapacity)
        {
            formattedText.Remove(textCacheOrder.Dequeue());
        }

        cached = CreateFormattedText(text, size, Brush(color), weight);
        formattedText.Add(key, cached);
        textCacheOrder.Enqueue(key);
        return cached;
    }

    internal SolidColorBrush Brush(Color color)
    {
        color = QuantizeColor(color);
        uint key = ColorKey(color);

        if (brushes.TryGetValue(key, out SolidColorBrush? brush))
        {
            return brush;
        }

        if (brushes.Count >= BrushCacheCapacity)
        {
            brushes.Remove(brushCacheOrder.Dequeue());
        }

        brush = new SolidColorBrush(color);
        brush.Freeze();
        brushes.Add(key, brush);
        brushCacheOrder.Enqueue(key);
        return brush;
    }

    internal Pen Pen(Color color, double thickness)
    {
        color = QuantizeColor(color);
        thickness = Math.Max(.25, Math.Round(thickness * 4) / 4);
        (uint, double thickness) key = (ColorKey(color), thickness);

        if (pens.TryGetValue(key, out Pen? pen))
        {
            return pen;
        }

        if (pens.Count >= PenCacheCapacity)
        {
            pens.Remove(penCacheOrder.Dequeue());
        }

        pen = new Pen(Brush(color), thickness);
        pen.Freeze();
        pens.Add(key, pen);
        penCacheOrder.Enqueue(key);
        return pen;
    }

    private static Color QuantizeColor(Color color)
    {
        byte alpha = (byte)Math.Clamp((int)Math.Round(color.A / 17d) * 17, 0, 255);
        return alpha == color.A ? color : Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static FormattedText CreateFormattedText(string text, double size, Brush brush, FontWeight weight)
    {
        return new FormattedText(
            text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal), size, brush,
            1.0)
        { TextAlignment = TextAlignment.Left };
    }

    private static uint ColorKey(Color color)
    {
        return ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
    }

    internal Point Pt(V2 v)
    {
        return new Point(v.X, v.Y);
    }

    internal string Money(int value)
    {
        return value.ToString("$#,0", CultureInfo.InvariantCulture);
    }

    internal double EaseOut(double x)
    {
        return 1 - Math.Pow(1 - Math.Clamp(x, 0, 1), 3);
    }

    internal Color FromArgb(uint argb, byte? alpha = null)
    {
        return Color.FromArgb(alpha ?? (byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
    }

    internal Color Lighten(Color c, double amount)
    {
        return Color.FromRgb(
            (byte)(c.R + (255 - c.R) * amount), (byte)(c.G + (255 - c.G) * amount),
            (byte)(c.B + (255 - c.B) * amount));
    }

    internal Color Darken(Color c, double amount)
    {
        return Color.FromRgb((byte)(c.R * (1 - amount)), (byte)(c.G * (1 - amount)), (byte)(c.B * (1 - amount)));
    }
}
