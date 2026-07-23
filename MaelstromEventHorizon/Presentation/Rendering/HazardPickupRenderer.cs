using MaelstromEventHorizon.Application;
using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;
using MaelstromEventHorizon.Presentation.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MaelstromEventHorizon.Presentation.Rendering;

internal sealed class HazardPickupRenderer
{
    internal void DrawMines(GameView view, DrawingContext dc)
    {
        foreach (var mine in view.Game.Mines)
        {
            view.DrawGlowEllipse(dc, mine.Position, 15, Color.FromRgb(255, 205, 63), 6, .65);
            dc.PushTransform(new TranslateTransform(mine.Position.X, mine.Position.Y));
            dc.PushTransform(new RotateTransform(mine.Angle * 180 / Math.PI));
            dc.DrawImage(view.MineBodySprite, new Rect(-27, -27, 54, 54));
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 55, 45)), null, new Point(), 3 + Math.Sin(view.Game.TotalTime * 14), 3 + Math.Sin(view.Game.TotalTime * 14));
            dc.Pop();
            dc.Pop();
        }
    }

    internal void DrawMineBody(GameView view, DrawingContext dc)
    {
        for (int i = 0; i < 8; i++)
        {
            double a = i * Math.PI / 4;
            var spike = new LinearGradientBrush(Color.FromRgb(255, 225, 112), Color.FromRgb(112, 42, 22), 90);
            Geometry spikeShape = view.Polygon(
                (Math.Cos(a - .13) * 8, Math.Sin(a - .13) * 8),
                (Math.Cos(a) * 22, Math.Sin(a) * 22),
                (Math.Cos(a + .13) * 8, Math.Sin(a + .13) * 8));
            dc.DrawGeometry(spike, new Pen(new SolidColorBrush(Color.FromRgb(255, 154, 49)), .8), spikeShape);
        }
        var core = new RadialGradientBrush(Color.FromRgb(255, 250, 178), Color.FromRgb(112, 22, 18));
        dc.DrawEllipse(core, new Pen(new SolidColorBrush(Color.FromRgb(255, 217, 76)), 1.5), new Point(), 11, 11);
        dc.DrawArc(new Pen(new SolidColorBrush(Color.FromArgb(180, 255, 244, 166)), 1.1), new Point(), 8, -40, 115);
    }

    internal void DrawVortices(GameView view, DrawingContext dc)
    {
        foreach (var vortex in view.Game.Vortices)
        {
            double pulse = Math.Sin(view.Game.TotalTime * 4 + vortex.Position.X) * 3;
            for (int i = 6; i >= 0; i--)
            {
                double r = 26 + i * 10 + pulse;
                byte a = (byte)(22 + (6 - i) * 17);
                var pen = new Pen(new SolidColorBrush(Color.FromArgb(a, (byte)(98 + i * 13), 76, 255)), 3.5 - i * .25);
                dc.DrawArc(pen, view.Pt(vortex.Position), r, vortex.Angle * 180 / Math.PI + i * 39, 235);
            }
            dc.DrawImage(view.VortexCoreSprite, new Rect(vortex.Position.X - 42, vortex.Position.Y - 42, 84, 84));
        }
    }

    internal void DrawVortexCore(DrawingContext dc)
    {
        var disk = new RadialGradientBrush();
        disk.GradientStops.Add(new GradientStop(Color.FromRgb(0, 0, 0), .25));
        disk.GradientStops.Add(new GradientStop(Color.FromRgb(12, 3, 24), .62));
        disk.GradientStops.Add(new GradientStop(Color.FromArgb(0, 115, 70, 255), 1));
        dc.DrawEllipse(disk, new Pen(new SolidColorBrush(Color.FromArgb(180, 145, 96, 255)), 2), new Point(), 36, 36);
        dc.DrawEllipse(Brushes.Black, new Pen(new SolidColorBrush(Color.FromRgb(206, 164, 255)), 1), new Point(), 13, 13);
        dc.DrawEllipse(new RadialGradientBrush(Color.FromArgb(0, 255, 255, 255), Color.FromArgb(150, 191, 124, 255)),
            null, new Point(-7, -8), 18, 13);
    }

    internal void DrawNovas(GameView view, DrawingContext dc)
    {
        foreach (var nova in view.Game.Novas)
        {
            double progress = nova.Age / Nova.Fuse;
            double pulse = 1 + Math.Sin(nova.Age * (5 + progress * 22)) * (.08 + progress * .13);
            double radius = (20 + progress * 30) * pulse;
            view.DrawGlowEllipse(dc, nova.Position, radius, Color.FromRgb(255, 176, 61), 10, .8);
            double coreSpan = radius * 2.08;
            dc.DrawImage(view.NovaCoreSprite,
                new Rect(nova.Position.X - coreSpan / 2, nova.Position.Y - coreSpan / 2, coreSpan, coreSpan));
            for (int i = 0; i < 6; i++)
            {
                double a = i * Math.PI / 3 + nova.Angle;
                var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(80 + progress * 120), 255, 218, 90)), 1.4);
                dc.DrawLine(pen, view.Pt(nova.Position + V2.FromAngle(a) * radius * .55), view.Pt(nova.Position + V2.FromAngle(a) * radius * 1.8));
            }
        }
    }

    internal void DrawNovaCore(DrawingContext dc)
    {
        var star = new RadialGradientBrush();
        star.GradientStops.Add(new GradientStop(Colors.White, 0));
        star.GradientStops.Add(new GradientStop(Color.FromRgb(255, 245, 145), .22));
        star.GradientStops.Add(new GradientStop(Color.FromRgb(255, 72, 35), .63));
        star.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 32, 20), 1));
        dc.DrawEllipse(star, null, new Point(), 50, 50);
    }

    internal void DrawPickups(GameView view, DrawingContext dc)
    {
        foreach (var pickup in view.Game.Pickups)
        {
            double pulse = 1 + Math.Sin(view.Game.TotalTime * 6 + pickup.Position.X) * .08;
            Color color = pickup.Kind switch
            {
                PickupKind.Canister => Color.FromRgb(80, 234, 255),
                PickupKind.Multiplier => Color.FromRgb(194, 101, 255),
                PickupKind.Bonus => Color.FromRgb(255, 213, 75),
                _ => Color.FromRgb(91, 255, 148)
            };
            view.DrawGlowEllipse(dc, pickup.Position, pickup.Radius * pulse, color, 7, .55);
            dc.PushTransform(new TranslateTransform(pickup.Position.X, pickup.Position.Y));
            dc.PushTransform(new RotateTransform(pickup.Angle * 180 / Math.PI));
            if (pickup.Kind == PickupKind.Canister)
            {
                if (view.CanisterSprite is not null)
                    dc.DrawImage(view.CanisterSprite, new Rect(-21, -21, 42, 42));
                else
                {
                    var shell = new LinearGradientBrush(Color.FromRgb(223, 252, 255), view.Darken(color, .68), 45);
                    dc.DrawRoundedRectangle(shell, new Pen(new SolidColorBrush(color), 1.5), new Rect(-11, -16, 22, 32), 5, 5);
                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(8, 31, 47)), null, new Rect(-8, -7, 16, 14));
                    dc.DrawLine(new Pen(new SolidColorBrush(color), 2), new Point(-5, 0), new Point(5, 0));
                    dc.DrawLine(new Pen(new SolidColorBrush(color), 2), new Point(0, -5), new Point(0, 5));
                }
            }
            else if (pickup.Kind == PickupKind.RescueShip)
            {
                dc.PushTransform(new ScaleTransform(.8, .8));
                dc.DrawImage(view.RescueShipSprite, new Rect(-36, -36, 72, 72));
                dc.Pop();
            }
            else
            {
                BitmapImage? sprite = pickup.Kind == PickupKind.Multiplier ? view.MultiplierSprite : view.DollarSprite;
                if (sprite is not null)
                    dc.DrawImage(sprite, new Rect(-22, -22, 44, 44));
                else
                {
                    Geometry badge = view.RegularPolygon(6, 15, -Math.PI / 6);
                    dc.DrawGeometry(new SolidColorBrush(view.Darken(color, .6)), new Pen(new SolidColorBrush(view.Lighten(color, .5)), 2), badge);
                }

                string value = pickup.Kind == PickupKind.Multiplier ? $"{pickup.Value}x" : $"${pickup.Value / 1000}K";
                view.DrawCenteredText(dc, value, 1, 7.5, 11, new SolidColorBrush(Color.FromArgb(210, 0, 0, 0)), FontWeights.Black);
                view.DrawCenteredText(dc, value, 0, 6.5, 11, Brushes.White, FontWeights.Black);
            }
            dc.Pop();
            dc.Pop();
        }
    }

    internal void DrawComets(GameView view, DrawingContext dc)
    {
        foreach (var comet in view.Game.Comets)
        {
            int copyRadius = view.Game.RicochetArenaActive ? 0 : 1;
            for (int x = -copyRadius; x <= copyRadius; x++)
            {
                for (int y = -copyRadius; y <= copyRadius; y++)
                {
                    V2 position = comet.Position + new V2(x * GameEngine.Width, y * GameEngine.Height);
                    if (position.X < -Comet.TrailLength - 30 || position.X > GameEngine.Width + Comet.TrailLength + 30 ||
                        position.Y < -Comet.TrailLength - 30 || position.Y > GameEngine.Height + Comet.TrailLength + 30) continue;
                    DrawComet(view, dc, comet, position);
                }
            }
        }
    }

    private void DrawComet(GameView view, DrawingContext dc, Comet comet, V2 position)
    {
        Color color = view.FromArgb(comet.Tint);
        V2 back = -comet.Velocity.Normalized;
        V2 side = new(-back.Y, back.X);
        Point head = view.Pt(position);
        var outerTail = new StreamGeometry();
        using (var c = outerTail.Open())
        {
            c.BeginFigure(view.Pt(position + side * 14), true, true);
            c.LineTo(view.Pt(position + back * Comet.TrailLength), true, false);
            c.LineTo(view.Pt(position - side * 14), true, false);
        }
        var outerBrush = new LinearGradientBrush(Color.FromArgb(0, color.R, color.G, color.B), Color.FromArgb(205, color.R, color.G, color.B),
            view.Pt(position + back * Comet.TrailLength), head)
        { MappingMode = BrushMappingMode.Absolute };
        dc.DrawGeometry(outerBrush, null, outerTail);

        var innerTail = new StreamGeometry();
        using (var c = innerTail.Open())
        {
            c.BeginFigure(view.Pt(position + side * 5), true, true);
            c.LineTo(view.Pt(position + back * (Comet.TrailLength * .78)), true, false);
            c.LineTo(view.Pt(position - side * 5), true, false);
        }
        var innerBrush = new LinearGradientBrush(Color.FromArgb(0, 255, 255, 255), Color.FromArgb(225, 255, 255, 255),
            view.Pt(position + back * (Comet.TrailLength * .78)), head)
        { MappingMode = BrushMappingMode.Absolute };
        dc.DrawGeometry(innerBrush, null, innerTail);

        BitmapSource headSprite = view.CometHeadSprites.TryGetValue(comet.Tint, out BitmapSource? sprite)
            ? sprite
            : view.CometHeadSprites[0xffffc65b];
        dc.DrawImage(headSprite, new Rect(position.X - 40, position.Y - 40, 80, 80));
        view.DrawCenteredText(dc, comet.Value >= 1000 ? $"${comet.Value / 1000.0:0.#}K" : $"${comet.Value}",
            position.X, position.Y + 4, 8.5, new SolidColorBrush(Color.FromRgb(30, 22, 28)), FontWeights.Black);
    }

    internal void DrawCometHead(GameView view, DrawingContext dc, Color color)
    {
        view.DrawGlowEllipse(dc, V2.Zero, 18, color, 4, .48);
        var core = new RadialGradientBrush();
        core.GradientStops.Add(new GradientStop(Colors.White, 0));
        core.GradientStops.Add(new GradientStop(view.Lighten(color, .42), .32));
        core.GradientStops.Add(new GradientStop(view.Darken(color, .48), 1));
        dc.DrawEllipse(core, new Pen(new SolidColorBrush(Color.FromArgb(210, 255, 255, 255)), 1), new Point(), 16, 16);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(145, 255, 255, 255)), null, new Point(-5, -6), 4.2, 2.8);
    }
}
