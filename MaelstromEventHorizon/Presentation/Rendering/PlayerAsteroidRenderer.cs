using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Presentation.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MaelstromEventHorizon.Presentation.Rendering;

internal sealed class PlayerAsteroidRenderer
{
    internal void DrawShip(GameView view, DrawingContext dc)
    {
        Ship ship = view.Game.Player;
        if (view.Game.PlayerRespawning) return;
        if (view.Game.Mode is GameMode.Title or GameMode.Controls or GameMode.Paused) return;
        if (view.Game is { Lives: <= 0, Mode: GameMode.GameOverDelay or GameMode.NameEntry or GameMode.GameOver }) return;
        if (view.Game.Mode == GameMode.Playing && ship is { Invulnerable: > 0, SpawnShieldTime: <= 0 } &&
            ((int)(view.Game.TotalTime * 12) & 1) == 0) return;
        dc.PushTransform(new TranslateTransform(ship.Position.X, ship.Position.Y));
        dc.PushTransform(new RotateTransform(ship.Angle * 180 / Math.PI));
        double visualScale = ship.VisualScale;

        if (ship.Thrusting)
        {
            dc.PushTransform(new ScaleTransform(visualScale, visualScale));
            var plume = new StreamGeometry();
            using (var c = plume.Open())
            {
                c.BeginFigure(new Point(-14, -7), true, true);
                c.LineTo(new Point(-36 - 8 * Math.Sin(view.Game.TotalTime * 38), 0), true, false);
                c.LineTo(new Point(-14, 7), true, false);
            }
            Color plumeTip = Color.FromRgb(45, 115, 255);
            var plumeBrush = new LinearGradientBrush(Color.FromArgb(245, 245, 255, 255),
                Color.FromArgb(30, plumeTip.R, plumeTip.G, plumeTip.B), new Point(1, .5), new Point(0, .5));
            dc.DrawGeometry(plumeBrush, new Pen(new SolidColorBrush(Color.FromArgb(120, 80, 220, 255)), 2), plume);
            dc.Pop();
        }

        BitmapSource hullSprite = ship.Giant ? view.GiantPlayerShipSprite : view.PlayerShipSprite;
        double hullCanvas = (ship.Giant ? 80 : 72) * visualScale;
        dc.DrawImage(hullSprite, new Rect(-hullCanvas / 2, -hullCanvas / 2, hullCanvas, hullCanvas));
        double navPulse = .72 + Math.Sin(view.Game.TotalTime * 8.5) * .18;
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(210 * navPulse), 255, 74, 62)),
            new Pen(new SolidColorBrush(Color.FromRgb(255, 184, 132)), .55 * visualScale),
            new Point(10 * visualScale, 0), 2.1 * visualScale, 2.1 * visualScale);
        dc.Pop();
        dc.Pop();

        if (ship.Shielding || ship.SpawnShieldTime > 0)
        {
            double pulse = 2 + Math.Sin(view.Game.TotalTime * 12) * 2;
            bool spawnShield = ship.SpawnShieldTime > 0;
            Color shieldColor = spawnShield ? Color.FromRgb(105, 255, 191) : Color.FromRgb(61, 225, 255);
            view.DrawGlowEllipse(dc, ship.Position, 29 * ship.VisualScale + pulse, shieldColor, 4, spawnShield ? .72 : .55);
            var shieldPen = new Pen(new SolidColorBrush(Color.FromArgb(220, shieldColor.R, shieldColor.G, shieldColor.B)), 1.9);
            dc.DrawEllipse(null, shieldPen, view.Pt(ship.Position), 29 * ship.VisualScale + pulse, 29 * ship.VisualScale + pulse);
            dc.DrawArc(shieldPen, view.Pt(ship.Position), 34 * ship.VisualScale + pulse, view.Game.TotalTime * 95, 122);
        }

        if (view.Game.ShieldImpactTime > 0)
        {
            double intensity = Math.Clamp(view.Game.ShieldImpactTime / .42, 0, 1);
            double expansion = (1 - intensity) * 18;
            Color impactColor = Color.FromRgb(202, 255, 255);
            view.DrawGlowEllipse(dc, ship.Position, 35 * ship.VisualScale + expansion, impactColor, 5, .92 * intensity);
            var ringPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(245 * intensity), 181, 255, 255)),
                1.5 + intensity * 2.2)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            double radius = 37 * ship.VisualScale + expansion;
            dc.DrawArc(ringPen, view.Pt(ship.Position), radius, view.Game.TotalTime * 180, 146);
            dc.DrawArc(ringPen, view.Pt(ship.Position), radius, view.Game.TotalTime * 180 + 188, 98);

            double contactRadius = 7 + expansion * .55;
            view.DrawGlowEllipse(dc, view.Game.ShieldImpactPoint, contactRadius, Color.FromRgb(236, 255, 255), 4, intensity);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(210 * intensity), 228, 255, 255)),
                null, view.Pt(view.Game.ShieldImpactPoint), 2.5 + intensity * 2.5, 2.5 + intensity * 2.5);
        }
    }

    internal void DrawDetailedShipHull(GameView view, DrawingContext dc, double time, Color accent, bool armored)
    {
        if (armored)
        {
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(88, accent.R, accent.G, accent.B)),
                new Pen(new SolidColorBrush(view.Lighten(accent, .62)), 2.2), view.ShipGeometry(5));
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(45, 58, 65)),
                new Pen(new SolidColorBrush(Color.FromRgb(203, 224, 224)), 1),
                view.Polygon((-19, -18), (-5, -12), (-9, -7), (-24, -10)));
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(38, 49, 57)),
                new Pen(new SolidColorBrush(Color.FromRgb(175, 204, 208)), 1),
                view.Polygon((-19, 18), (-5, 12), (-9, 7), (-24, 10)));
        }

        var engineShell = new LinearGradientBrush(Color.FromRgb(190, 209, 208), Color.FromRgb(25, 34, 42), 0);
        dc.DrawRoundedRectangle(engineShell, new Pen(new SolidColorBrush(Color.FromRgb(213, 229, 225)), .9),
            new Rect(-18, -14, 12, 6), 2, 2);
        dc.DrawRoundedRectangle(engineShell, new Pen(new SolidColorBrush(Color.FromRgb(176, 201, 202)), .9),
            new Rect(-18, 8, 12, 6), 2, 2);
        Color engineCore = view.Lighten(accent, .55);
        dc.DrawEllipse(new RadialGradientBrush(Colors.White, accent), null, new Point(-17, -11), 2.4, 1.7);
        dc.DrawEllipse(new RadialGradientBrush(Colors.White, accent), null, new Point(-17, 11), 2.4, 1.7);

        var hull = new LinearGradientBrush
        {
            StartPoint = new Point(.18, .05),
            EndPoint = new Point(.82, .95)
        };
        hull.GradientStops.Add(new GradientStop(Color.FromRgb(251, 253, 246), 0));
        hull.GradientStops.Add(new GradientStop(Color.FromRgb(176, 194, 198), .25));
        hull.GradientStops.Add(new GradientStop(Color.FromRgb(74, 91, 101), .53));
        hull.GradientStops.Add(new GradientStop(Color.FromRgb(29, 42, 52), .76));
        hull.GradientStops.Add(new GradientStop(Color.FromRgb(163, 184, 184), 1));
        dc.DrawGeometry(hull, new Pen(new SolidColorBrush(Color.FromRgb(16, 25, 33)), 2), view.ShipGeometry(0));

        var upperPlate = new LinearGradientBrush(Color.FromRgb(186, 207, 208), Color.FromRgb(48, 66, 76), 70);
        var lowerPlate = new LinearGradientBrush(Color.FromRgb(118, 143, 150), Color.FromRgb(30, 45, 55), 110);
        dc.DrawGeometry(upperPlate, new Pen(new SolidColorBrush(Color.FromRgb(226, 237, 232)), .85),
            view.Polygon((-14, -11), (-3, -7), (7, -2), (1, 0), (-9, -3)));
        dc.DrawGeometry(lowerPlate, new Pen(new SolidColorBrush(Color.FromRgb(151, 181, 185)), .85),
            view.Polygon((-14, 11), (-3, 7), (7, 2), (1, 0), (-9, 3)));
        dc.DrawGeometry(new LinearGradientBrush(view.Lighten(accent, .24), view.Darken(accent, .68), 0),
            new Pen(new SolidColorBrush(Color.FromArgb(190, 205, 246, 255)), .75),
            view.Polygon((7, -3), (25, 0), (7, 3), (11, 0)));

        var seamPen = new Pen(new SolidColorBrush(Color.FromArgb(165, 12, 25, 34)), .7);
        dc.DrawLine(seamPen, new Point(-10, -4), new Point(1, -1.5));
        dc.DrawLine(seamPen, new Point(-10, 4), new Point(1, 1.5));
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(175, accent.R, accent.G, accent.B)), .8),
            new Point(-12, 0), new Point(13, 0));

        var canopy = new RadialGradientBrush
        {
            GradientOrigin = new Point(.28, .2),
            Center = new Point(.38, .34),
            RadiusX = .78,
            RadiusY = .78
        };
        canopy.GradientStops.Add(new GradientStop(Color.FromRgb(220, 255, 255), 0));
        canopy.GradientStops.Add(new GradientStop(view.Lighten(accent, .3), .25));
        canopy.GradientStops.Add(new GradientStop(Color.FromRgb(24, 73, 88), .58));
        canopy.GradientStops.Add(new GradientStop(Color.FromRgb(3, 13, 21), 1));
        Geometry canopyShape = view.Polygon((-5, -6), (11, 0), (-5, 6), (0, 0));
        dc.DrawGeometry(canopy, new Pen(new SolidColorBrush(Color.FromRgb(190, 233, 236)), 1.1), canopyShape);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(185, 237, 255, 255)), .8),
            new Point(-2, -4), new Point(7, -1));

        double lampPulse = .72 + Math.Sin(time * 8.5) * .18;
        byte lampAlpha = (byte)(210 * lampPulse);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(lampAlpha, 255, 74, 62)),
            new Pen(new SolidColorBrush(Color.FromRgb(255, 184, 132)), .55), new Point(10, 0), 2.1, 2.1);
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(87, 255, 166)), null, new Point(-7, -12), 1.35, 1.35);
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 93, 88)), null, new Point(-7, 12), 1.35, 1.35);
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 3; i++)
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(145, engineCore.R, engineCore.G, engineCore.B)), .65),
                    new Point(-14 + i * 3, side * 9), new Point(-13 + i * 3, side * 12));
    }


    internal void DrawShipDebris(GameView view, DrawingContext dc)
    {
        foreach (var piece in view.Game.ShipDebrisPieces)
        {
            double life = Math.Clamp(1 - piece.Age / piece.Lifetime, 0, 1);
            byte alpha = (byte)(255 * Math.Min(1, life * 1.8));
            dc.PushTransform(new TranslateTransform(piece.Position.X, piece.Position.Y));
            dc.PushTransform(new RotateTransform(piece.Angle * 180 / Math.PI));
            dc.PushTransform(new ScaleTransform(Ship.BaseVisualScale, Ship.BaseVisualScale));

            Geometry shape = view.ShipDebrisGeometry(piece.Kind);
            Color fill = piece.Kind == 4 ? Color.FromArgb(alpha, 67, 160, 181) : Color.FromArgb(alpha, 108, 126, 132);
            Color edge = Color.FromArgb(alpha, 222, 234, 231);
            dc.DrawGeometry(new SolidColorBrush(fill), new Pen(new SolidColorBrush(edge), 1.2), shape);
            if (piece.Kind is 1 or 2)
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(alpha, 255, 106, 60)), 1.3),
                    new Point(-4, 0), new Point(6, 0));

            dc.Pop();
            dc.Pop();
            dc.Pop();
        }
    }

    internal void DrawAsteroids(GameView view, DrawingContext dc)
    {
        foreach (Asteroid rock in view.Game.Asteroids)
            DrawAsteroid(view, dc, rock);
    }

    private void DrawAsteroid(GameView view, DrawingContext dc, Asteroid rock)
    {
        dc.PushTransform(new TranslateTransform(rock.Position.X, rock.Position.Y));
        dc.PushTransform(new RotateTransform(rock.Angle * 180 / Math.PI));
        Geometry shape = view.AsteroidGeometry(rock);
        if (rock.Steel)
        {
            if (view.MetalAsteroidSprite is not null)
            {
                double span = rock.Radius * 2.3;
                dc.DrawImage(view.MetalAsteroidSprite, new Rect(-span / 2, -span / 2, span, span));
            }
            else
            {
                var metal = new RadialGradientBrush
                {
                    GradientOrigin = new Point(.28, .22),
                    Center = new Point(.35, .3),
                    RadiusX = .72,
                    RadiusY = .72
                };
                metal.GradientStops.Add(new GradientStop(Color.FromRgb(244, 253, 255), 0));
                metal.GradientStops.Add(new GradientStop(Color.FromRgb(92, 132, 156), .34));
                metal.GradientStops.Add(new GradientStop(Color.FromRgb(16, 29, 43), .72));
                metal.GradientStops.Add(new GradientStop(Color.FromRgb(135, 226, 255), 1));
                dc.DrawGeometry(metal, new Pen(new SolidColorBrush(Color.FromRgb(192, 247, 255)), 2), shape);
                for (int i = 0; i < 4; i++)
                {
                    double a = i * Math.PI / 2 + .35;
                    Point p = new(Math.Cos(a) * rock.Radius * .52, Math.Sin(a) * rock.Radius * .52);
                    dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(180, 234, 247)), new Pen(Brushes.Black, 1), p, 2.8, 2.8);
                }
            }
        }
        else
        {
            BitmapImage? sprite = view.AsteroidSprites[(rock.Seed & int.MaxValue) % view.AsteroidSprites.Length];
            if (sprite is not null)
            {
                double span = rock.Radius * 2.2;
                dc.DrawImage(sprite, new Rect(-span / 2, -span / 2, span, span));
            }
            else
            {
                var stone = new RadialGradientBrush
                {
                    GradientOrigin = new Point(.28, .22),
                    Center = new Point(.32, .28),
                    RadiusX = .8,
                    RadiusY = .8
                };
                stone.GradientStops.Add(new GradientStop(Color.FromRgb(178, 145, 121), 0));
                stone.GradientStops.Add(new GradientStop(Color.FromRgb(73, 56, 57), .48));
                stone.GradientStops.Add(new GradientStop(Color.FromRgb(22, 21, 31), 1));
                dc.DrawGeometry(stone, new Pen(new SolidColorBrush(Color.FromRgb(230, 178, 127)), 1.25), shape);
                dc.PushClip(shape);
                DrawRockTexture(view, dc, rock);
                dc.Pop();
                DrawCrater(dc, rock.Radius * -.2, rock.Radius * -.18, rock.Radius * .18);
                DrawCrater(dc, rock.Radius * .26, rock.Radius * .18, rock.Radius * .11);
                DrawCrater(dc, rock.Radius * -.12, rock.Radius * .37, rock.Radius * .08);
            }
        }
        dc.Pop();
        dc.Pop();
    }

    private void DrawCrater(DrawingContext dc, double x, double y, double r)
    {
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(150, 18, 15, 22)), new Pen(new SolidColorBrush(Color.FromArgb(150, 198, 151, 116)), 1), new Point(x, y), r, r * .65);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(55, 255, 214, 174)), null, new Point(x - r * .25, y - r * .22), r * .34, r * .18);
    }

    private void DrawRockTexture(GameView view, DrawingContext dc, Asteroid rock)
    {
        int marks = rock.Size == 3 ? 30 : rock.Size == 2 ? 19 : 11;
        for (int i = 0; i < marks; i++)
        {
            double a = view.Hash(rock.Seed + 417, i) * Math.PI * 2;
            double distance = Math.Sqrt(view.Hash(rock.Seed - 971, i)) * rock.Radius * .82;
            double size = (.018 + view.Hash(rock.Seed + 73, i) * .065) * rock.Radius;
            Point p = new(Math.Cos(a) * distance, Math.Sin(a) * distance);
            bool light = view.Hash(rock.Seed + 11, i) > .58;
            Color color = light ? Color.FromArgb(75, 226, 187, 143) : Color.FromArgb(105, 29, 24, 30);
            dc.DrawEllipse(new SolidColorBrush(color), null, p, size * 1.4, size);
        }

        var vein = new Pen(new SolidColorBrush(Color.FromArgb(100, 35, 27, 31)), Math.Max(.7, rock.Radius * .022));
        for (int i = 0; i < Math.Max(2, rock.Size + 1); i++)
        {
            double a = view.Hash(rock.Seed + 800, i) * Math.PI * 2;
            Point p0 = new(Math.Cos(a) * rock.Radius * .15, Math.Sin(a) * rock.Radius * .15);
            Point p1 = new(Math.Cos(a + .34) * rock.Radius * .72, Math.Sin(a + .34) * rock.Radius * .72);
            dc.DrawLine(vein, p0, p1);
        }
    }
}
