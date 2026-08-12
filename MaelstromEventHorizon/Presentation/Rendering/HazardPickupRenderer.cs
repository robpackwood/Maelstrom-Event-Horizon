using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MaelstromEventHorizon.Application;
using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;
using MaelstromEventHorizon.Presentation.Drawing;

namespace MaelstromEventHorizon.Presentation.Rendering;

internal sealed class HazardPickupRenderer
{
    internal void DrawMines(GameView view, DrawingContext dc)
    {
        foreach (HomingMine mine in view.Game.Mines)
        {
            view.DrawGlowEllipse(dc, mine.Position, 15, Color.FromRgb(255, 205, 63), 6, .65);
            dc.PushTransform(new TranslateTransform(mine.Position.X, mine.Position.Y));
            dc.PushTransform(new RotateTransform(mine.Angle * 180 / Math.PI));
            dc.DrawImage(view.MineBodySprite, new Rect(-27, -27, 54, 54));

            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 55, 45)), null, new Point(),
                3 + Math.Sin(view.Game.TotalTime * 14), 3 + Math.Sin(view.Game.TotalTime * 14));

            dc.Pop();
            dc.Pop();
        }
    }

    internal void DrawMineBody(GameView view, DrawingContext dc)
    {
        for (int i = 0; i < 8; i++)
        {
            double a = i * Math.PI / 4;
            LinearGradientBrush spike = new(Color.FromRgb(255, 225, 112), Color.FromRgb(112, 42, 22), 90);

            Geometry spikeShape = view.Polygon(
                (Math.Cos(a - .13) * 8, Math.Sin(a - .13) * 8),
                (Math.Cos(a) * 22, Math.Sin(a) * 22),
                (Math.Cos(a + .13) * 8, Math.Sin(a + .13) * 8));

            dc.DrawGeometry(spike, new Pen(new SolidColorBrush(Color.FromRgb(255, 154, 49)), .8), spikeShape);
        }

        RadialGradientBrush core = new(Color.FromRgb(255, 250, 178), Color.FromRgb(112, 22, 18));
        dc.DrawEllipse(core, new Pen(new SolidColorBrush(Color.FromRgb(255, 217, 76)), 1.5), new Point(), 11, 11);
        dc.DrawArc(new Pen(new SolidColorBrush(Color.FromArgb(180, 255, 244, 166)), 1.1), new Point(), 8, -40, 115);
    }

    internal void DrawVortices(GameView view, DrawingContext dc)
    {
        foreach (GravityVortex vortex in view.Game.Vortices)
        {
            double pulse = Math.Sin(view.Game.TotalTime * 4 + vortex.Position.X) * 3;
            double distortion = 74 + pulse + Math.Sin(view.Game.TotalTime * 7) * 5;

            dc.DrawEllipse(null, view.Pen(Color.FromArgb(48, 165, 103, 255), 1.2), view.Pt(vortex.Position),
                distortion, distortion * .64);

            for (int i = 6; i >= 0; i--)
            {
                double r = 26 + i * 10 + pulse;
                byte a = (byte)(22 + (6 - i) * 17);
                Pen pen = new(new SolidColorBrush(Color.FromArgb(a, (byte)(98 + i * 13), 76, 255)), 3.5 - i * .25);
                dc.DrawArc(pen, view.Pt(vortex.Position), r, vortex.Angle * 180 / Math.PI + i * 39, 235);
            }

            dc.DrawImage(view.VortexCoreSprite, new Rect(vortex.Position.X - 42, vortex.Position.Y - 42, 84, 84));
        }
    }

    internal void DrawVortexCore(DrawingContext dc)
    {
        RadialGradientBrush disk = new();
        disk.GradientStops.Add(new GradientStop(Color.FromRgb(0, 0, 0), .25));
        disk.GradientStops.Add(new GradientStop(Color.FromRgb(12, 3, 24), .62));
        disk.GradientStops.Add(new GradientStop(Color.FromArgb(0, 115, 70, 255), 1));
        dc.DrawEllipse(disk, new Pen(new SolidColorBrush(Color.FromArgb(180, 145, 96, 255)), 2), new Point(), 36, 36);

        dc.DrawEllipse(Brushes.Black,
            new Pen(new SolidColorBrush(Color.FromRgb(206, 164, 255)), 1), new Point(), 13, 13);

        dc.DrawEllipse(new RadialGradientBrush(Color.FromArgb(0, 255, 255, 255), Color.FromArgb(150, 191, 124, 255)),
            null, new Point(-7, -8), 18, 13);
    }

    internal void DrawNovas(GameView view, DrawingContext dc)
    {
        foreach (Nova nova in view.Game.Novas)
        {
            double progress = nova.Age / Nova.Fuse;
            double pulse = 1 + Math.Sin(nova.Age * (5 + progress * 22)) * (.08 + progress * .13);
            double radius = (20 + progress * 30) * pulse;

            dc.DrawEllipse(null, view.Pen(Color.FromArgb((byte)(42 + progress * 85), 255, 164, 67), 1.1),
                view.Pt(nova.Position), radius * 2.15, radius * 2.15);

            view.DrawGlowEllipse(dc, nova.Position, radius, Color.FromRgb(255, 176, 61), 10, .8);
            double coreSpan = radius * 2.08;

            dc.DrawImage(view.NovaCoreSprite,
                new Rect(nova.Position.X - coreSpan / 2, nova.Position.Y - coreSpan / 2, coreSpan, coreSpan));

            for (int i = 0; i < 6; i++)
            {
                double a = i * Math.PI / 3 + nova.Angle;
                Pen pen = new(new SolidColorBrush(Color.FromArgb((byte)(80 + progress * 120), 255, 218, 90)), 1.4);

                dc.DrawLine(pen, view.Pt(nova.Position + V2.FromAngle(a) * radius * .55),
                    view.Pt(nova.Position + V2.FromAngle(a) * radius * 1.8));
            }
        }
    }

    internal void DrawNovaCore(DrawingContext dc)
    {
        RadialGradientBrush star = new();
        star.GradientStops.Add(new GradientStop(Colors.White, 0));
        star.GradientStops.Add(new GradientStop(Color.FromRgb(255, 245, 145), .22));
        star.GradientStops.Add(new GradientStop(Color.FromRgb(255, 72, 35), .63));
        star.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 32, 20), 1));
        dc.DrawEllipse(star, null, new Point(), 50, 50);
    }

    internal void DrawPickups(GameView view, DrawingContext dc)
    {
        foreach (Pickup pickup in view.Game.Pickups)
        {
            bool urgentPickup = pickup.Kind is PickupKind.Canister or PickupKind.RescueShip or PickupKind.TimeFreeze
                or PickupKind.SmartBomb or PickupKind.RicochetArena;

            double pulse = urgentPickup
                ? 1 + (Math.Sin(view.Game.TotalTime * 8.5 + pickup.Position.X) + 1) * .115
                : 1 + Math.Sin(view.Game.TotalTime * 6 + pickup.Position.X) * .08;

            double pulsePhase = view.Game.TotalTime * 8.5 + pickup.Position.X;

            Color color = pickup.Kind switch
            {
                PickupKind.Canister => Color.FromRgb(80, 234, 255),
                PickupKind.Multiplier => Color.FromRgb(194, 101, 255),
                PickupKind.Bonus => Color.FromRgb(255, 213, 75),
                PickupKind.TimeFreeze => Color.FromRgb(126, 238, 255),
                PickupKind.SmartBomb => Color.FromRgb(255, 91, 74),
                PickupKind.RicochetArena => Color.FromRgb(255, 174, 93),
                _ => Color.FromRgb(91, 255, 148)
            };

            view.DrawGlowEllipse(dc, pickup.Position, pickup.Radius * pulse, color, urgentPickup ? 9 : 7,
                urgentPickup ? .8 : .55);

            if (pickup.Kind is PickupKind.TimeFreeze or PickupKind.SmartBomb or PickupKind.RicochetArena)
            {
                double beaconRadius = (pickup.Radius + 9 + Math.Sin(pulsePhase) * 2) * pulse;

                dc.DrawEllipse(null, view.Pen(Color.FromArgb(180, color.R, color.G, color.B), 1.5),
                    view.Pt(pickup.Position), beaconRadius * 2, beaconRadius * 2);

                dc.DrawArc(view.Pen(Color.FromArgb(230, 255, 255, 255), 2.2), view.Pt(pickup.Position),
                    beaconRadius + 4,
                    pickup.Angle * 180 / Math.PI, 84);
            }

            dc.PushTransform(new TranslateTransform(pickup.Position.X, pickup.Position.Y));
            dc.PushTransform(new RotateTransform(pickup.Angle * 180 / Math.PI));

            if (urgentPickup)
            {
                dc.PushTransform(new ScaleTransform(pulse, pulse));
            }

            if (pickup.Kind == PickupKind.Canister)
            {
                if (view.CanisterSprite is not null)
                {
                    dc.DrawImage(view.CanisterSprite, new Rect(-21, -21, 42, 42));
                }
                else
                {
                    LinearGradientBrush shell = new(Color.FromRgb(223, 252, 255), view.Darken(color, .68), 45);

                    dc.DrawRoundedRectangle(shell,
                        new Pen(new SolidColorBrush(color), 1.5), new Rect(-11, -16, 22, 32), 5, 5);

                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(8, 31, 47)), null, new Rect(-8, -7, 16, 14));
                    dc.DrawLine(new Pen(new SolidColorBrush(color), 2), new Point(-5, 0), new Point(5, 0));
                    dc.DrawLine(new Pen(new SolidColorBrush(color), 2), new Point(0, -5), new Point(0, 5));
                }
            }
            else if (pickup.Kind == PickupKind.RescueShip)
            {
                double hullCanvas = 72 * Ship.BaseVisualScale;
                dc.DrawImage(view.RescueShipSprite, new Rect(-hullCanvas / 2, -hullCanvas / 2, hullCanvas, hullCanvas));
                double navPulse = .72 + Math.Sin(view.Game.TotalTime * 8.5) * .18;

                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(210 * navPulse), 94, 255, 177)),
                    new Pen(new SolidColorBrush(Color.FromRgb(174, 255, 213)), .55 * Ship.BaseVisualScale),
                    new Point(10 * Ship.BaseVisualScale, 0), 2.1 * Ship.BaseVisualScale, 2.1 * Ship.BaseVisualScale);
            }
            else if (pickup.Kind == PickupKind.TimeFreeze)
            {
                if (view.TimeFreezeSprite is not null)
                {
                    dc.DrawImage(view.TimeFreezeSprite, new Rect(-28, -28, 56, 56));
                    goto PickupComplete;
                }

                Pen rim = new(new SolidColorBrush(view.Lighten(color, .5)), 2.5);

                dc.DrawEllipse(new RadialGradientBrush(
                        Colors.White, view.Darken(color, .72)), rim, new Point(), 17, 17);

                dc.DrawEllipse(new SolidColorBrush(
                        Color.FromArgb(150, 6, 30, 58)), new Pen(Brushes.White, 1), new Point(), 12, 12);

                for (int i = 0; i < 8; i++)
                {
                    double angle = i * Math.PI / 4;
                    V2 tick = V2.FromAngle(angle);
                    dc.DrawLine(rim, new Point(tick.X * 12, tick.Y * 12), new Point(tick.X * 15, tick.Y * 15));
                }

                Geometry hourglass = view.Polygon(
                    (-7, -8), (7, -8), (3.5, -2), (3.5, 2), (7, 8), (-7, 8), (-3.5, 2), (-3.5, -2));

                dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(210, 225, 255, 255)),
                    new Pen(new SolidColorBrush(view.Darken(color, .5)), 1.2), hourglass);

                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(112, 230, 255)), null, new Point(0, 4.5), 2.6, 1.8);
            }
            else if (pickup.Kind == PickupKind.RicochetArena)
            {
                if (view.RicochetArenaSprite is not null)
                {
                    dc.DrawImage(view.RicochetArenaSprite, new Rect(-28, -28, 56, 56));
                    goto PickupComplete;
                }

                Geometry arena = view.RegularPolygon(6, 18, Math.PI / 6);
                Geometry innerArena = view.RegularPolygon(6, 11, Math.PI / 6);

                dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(125, color.R, color.G, color.B)),
                    new Pen(new SolidColorBrush(view.Lighten(color, .45)), 2.5), arena);

                dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(175, 20, 28, 58)),
                    new Pen(new SolidColorBrush(Color.FromRgb(255, 245, 210)), 1.15), innerArena);

                for (int i = 0; i < 3; i++)
                {
                    double angle = Math.PI / 6 + i * Math.PI * 2 / 3;
                    V2 node = V2.FromAngle(angle) * 14;

                    dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 244, 195)),
                        new Pen(new SolidColorBrush(color), 1), new Point(node.X, node.Y), 3.4, 3.4);
                }

                dc.DrawLine(new Pen(Brushes.White, 2.4), new Point(-9, 7), new Point(4, -5));
                dc.DrawLine(new Pen(Brushes.White, 2.4), new Point(4, -5), new Point(10, 3));
                dc.DrawLine(new Pen(Brushes.White, 2.4), new Point(4, -5), new Point(3, 2));
            }
            else if (pickup.Kind == PickupKind.SmartBomb)
            {
                if (view.SmartBombSprite is not null)
                {
                    dc.DrawImage(view.SmartBombSprite, new Rect(-28, -28, 56, 56));
                    goto PickupComplete;
                }

                for (int i = 0; i < 4; i++)
                {
                    double angle = i * Math.PI / 2 + Math.PI / 4;
                    V2 fin = V2.FromAngle(angle);

                    Geometry finShape = view.Polygon((fin.X * 9, fin.Y * 9),
                        (fin.X * 21 - fin.Y * 4, fin.Y * 21 + fin.X * 4),
                        (fin.X * 16 + fin.Y * 4, fin.Y * 16 - fin.X * 4));

                    dc.DrawGeometry(new SolidColorBrush(view.Darken(color, .5)),
                        new Pen(new SolidColorBrush(view.Lighten(color, .28)), 1), finShape);
                }

                dc.DrawEllipse(new RadialGradientBrush(Color.FromRgb(255, 247, 185), Color.FromRgb(112, 15, 23)),
                    new Pen(new SolidColorBrush(view.Lighten(color, .42)), 2.5), new Point(), 16, 16);

                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(170, 35, 0, 10)), new Pen(Brushes.White, 1),
                    new Point(), 8, 8);

                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(255, 239, 140)), 2.5), new Point(5, -13),
                    new Point(12, -21));

                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 247, 112)), new Pen(Brushes.White, 1),
                    new Point(13, -22), 4, 4);

                view.DrawCenteredText(dc, "!", 0, 5.5, 14, Brushes.White, FontWeights.Black);
            }
            else
            {
                BitmapImage? sprite = pickup.Kind == PickupKind.Multiplier ? view.MultiplierSprite : view.DollarSprite;

                if (sprite is not null)
                {
                    dc.DrawImage(sprite, new Rect(-22, -22, 44, 44));
                }
                else
                {
                    Geometry badge = view.RegularPolygon(6, 15, -Math.PI / 6);

                    dc.DrawGeometry(new SolidColorBrush(view.Darken(color, .6)),
                        new Pen(new SolidColorBrush(view.Lighten(color, .5)), 2), badge);
                }

                string value = pickup.Kind == PickupKind.Multiplier ? $"{pickup.Value}x" : $"${pickup.Value / 1000}K";

                view.DrawCenteredText(dc, value, 1, 7.5, 11, new SolidColorBrush(Color.FromArgb(210, 0, 0, 0)),
                    FontWeights.Black);

                view.DrawCenteredText(dc, value, 0, 6.5, 11, Brushes.White, FontWeights.Black);
            }

            PickupComplete:

            if (urgentPickup)
            {
                dc.Pop();
            }

            dc.Pop();
            dc.Pop();
        }
    }

    internal void DrawComets(GameView view, DrawingContext dc)
    {
        foreach (Comet comet in view.Game.Comets)
        {
            int copyRadius = view.Game.RicochetArenaActive ? 0 : 1;

            for (int x = -copyRadius; x <= copyRadius; x++)
            {
                for (int y = -copyRadius; y <= copyRadius; y++)
                {
                    V2 position = comet.Position + new V2(x * GameEngine.Width, y * GameEngine.Height);

                    if (position.X < -Comet.TrailLength - 30 ||
                        position.X > GameEngine.Width + Comet.TrailLength + 30 ||
                        position.Y < -Comet.TrailLength - 30 || position.Y > GameEngine.Height + Comet.TrailLength + 30)
                    {
                        continue;
                    }

                    DrawComet(view, dc, comet, position);
                }
            }
        }
    }

    private void DrawComet(GameView view, DrawingContext dc, Comet comet, V2 position)
    {
        Color color = view.FromArgb(comet.Tint);
        double pulse = 1 + (Math.Sin(view.Game.TotalTime * 9 + comet.Position.X) + 1) * .09;
        V2 back = -comet.Velocity.Normalized;
        dc.PushTransform(new TranslateTransform(position.X, position.Y));
        dc.PushTransform(new RotateTransform(Math.Atan2(back.Y, back.X) * 180 / Math.PI));
        dc.DrawImage(view.CometTailSprite!, new Rect(0, -18, Comet.TrailLength, 36));
        dc.Pop();
        dc.Pop();

        BitmapSource headSprite = view.CometHeadSprites.TryGetValue(comet.Tint, out BitmapSource? sprite)
            ? sprite
            : view.CometHeadSprites[0xffffc65b];

        view.DrawGlowEllipse(dc, position, 27 * pulse, color, 5, .58);
        double headSize = 80 * pulse;
        dc.DrawImage(headSprite, new Rect(position.X - headSize / 2, position.Y - headSize / 2, headSize, headSize));

        view.DrawCenteredText(dc, comet.Value >= 1000 ? $"${comet.Value / 1000.0:0.#}K" : $"${comet.Value}",
            position.X, position.Y + 4, 8.5, new SolidColorBrush(Color.FromRgb(30, 22, 28)), FontWeights.Black);
    }

    internal void DrawCometHead(GameView view, DrawingContext dc, Color color)
    {
        view.DrawGlowEllipse(dc, V2.Zero, 18, color, 4, .48);
        RadialGradientBrush core = new();
        core.GradientStops.Add(new GradientStop(Colors.White, 0));
        core.GradientStops.Add(new GradientStop(view.Lighten(color, .42), .32));
        core.GradientStops.Add(new GradientStop(view.Darken(color, .48), 1));
        dc.DrawEllipse(core, new Pen(new SolidColorBrush(Color.FromArgb(210, 255, 255, 255)), 1), new Point(), 16, 16);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(145, 255, 255, 255)), null, new Point(-5, -6), 4.2, 2.8);
    }
}
