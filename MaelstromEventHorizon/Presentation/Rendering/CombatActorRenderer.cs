using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;
using MaelstromEventHorizon.Presentation.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MaelstromEventHorizon.Presentation.Rendering;

internal sealed class CombatActorRenderer
{
    internal void DrawFighters(GameView view, DrawingContext dc)
    {
        foreach (var fighter in view.Game.Fighters)
        {
            bool little = fighter.Kind == FighterKind.Interceptor;
            Color glow = little ? Color.FromRgb(63, 228, 255) : Color.FromRgb(255, 57, 112);
            view.DrawGlowEllipse(dc, fighter.Position, fighter.Radius * .78, glow, 3, .2);
            dc.PushTransform(new TranslateTransform(fighter.Position.X, fighter.Position.Y));
            dc.PushTransform(new RotateTransform(fighter.Angle * 180 / Math.PI));
            BitmapImage? sprite = little ? view.InterceptorSprite : view.RaiderSprite;
            if (sprite is not null)
            {
                double span = little ? 52 : 72;
                dc.DrawImage(sprite, new Rect(-span / 2, -span / 2, span, span));
            }
            else
            {
                Geometry wings = little
                    ? view.Polygon((22, 0), (1, -12), (-18, -15), (-10, 0), (-18, 15), (1, 12))
                    : view.Polygon((27, 0), (6, -13), (-25, -21), (-16, -2), (-27, 0), (-16, 2), (-25, 21), (6, 13));
                view.DrawGlowGeometry(dc, wings, glow, 8);
                var body = new LinearGradientBrush(view.Lighten(glow, .75), view.Darken(glow, .72), new Point(1, 0), new Point(0, 1));
                dc.DrawGeometry(body, new Pen(new SolidColorBrush(view.Lighten(glow, .4)), 1.5), wings);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(5, 12, 24)), new Pen(new SolidColorBrush(glow), 1), new Point(7, 0), 7, 5);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 246, 170)), null, new Point(13, 0), 2, 2);
            }
            dc.Pop();
            dc.Pop();
        }
    }

    internal void DrawBosses(GameView view, DrawingContext dc)
    {
        foreach (AlienBoss boss in view.Game.Bosses.Where(boss => boss.Alive))
        {
            Color tint = BossColor(boss.Kind);
            double hover = Math.Sin(boss.Age * (boss.Kind == AlienBossKind.BoneBroodmother ? 1.1 : 1.65)) *
                           (boss.Kind == AlienBossKind.EyeTyrant ? 5 : 2.5);
            double breathe = Math.Sin(boss.Age * 2.25);
            double scaleX = 1 + breathe * (boss.Kind == AlienBossKind.VoidLeech ? .025 : .012);
            double scaleY = 1 - breathe * (boss.Kind == AlienBossKind.SludgeMaw ? .025 : .012);
            var position = new V2(boss.Position.X, boss.Position.Y + hover);
            view.DrawGlowEllipse(dc, position, boss.Radius * .92, tint, 3, .14);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(75, 0, 0, 0)), null,
                new Point(position.X + 8, position.Y + boss.Radius * .64), boss.Radius * .72, boss.Radius * .19);
            dc.PushTransform(new TranslateTransform(position.X, position.Y));
            dc.PushTransform(new ScaleTransform(scaleX, scaleY));
            BitmapSource[] frames = view.BossSpriteFrames[(int)boss.Kind];
            int cycleLength = (frames.Length - 1) * 2;
            int cycleFrame = (int)(boss.Age * GameView.BossSpriteFrameRate) % cycleLength;
            int frame = cycleFrame < frames.Length ? cycleFrame : cycleLength - cycleFrame;
            Rect spriteRect = BossSpriteRect(boss.Kind);
            dc.DrawImage(frames[frame], spriteRect);
            DrawAttackTell(dc, boss, tint);
            if (boss.HurtFlash > 0)
            {
                dc.PushOpacity(Math.Clamp(boss.HurtFlash / .14, 0, 1) * .82);
                dc.PushOpacityMask(new ImageBrush(frames[frame]) { Stretch = Stretch.Fill });
                dc.DrawRectangle(Brushes.White, null, spriteRect);
                dc.Pop();
                dc.Pop();
            }
            dc.Pop();
            dc.Pop();
        }
    }

    private static Rect BossSpriteRect(AlienBossKind kind) => kind switch
    {
        AlienBossKind.SludgeMaw => new Rect(-110, -103, 220, 206),
        AlienBossKind.EyeTyrant => new Rect(-105, -105, 210, 210),
        AlienBossKind.BoneBroodmother => new Rect(-116, -105, 232, 210),
        _ => new Rect(-108, -108, 216, 216)
    };

    private static void DrawAttackTell(DrawingContext dc, AlienBoss boss, Color tint)
    {
        double timer = boss.Kind is AlienBossKind.SludgeMaw or AlienBossKind.BoneBroodmother
            ? Math.Min(boss.AttackTimer, boss.SpecialTimer)
            : boss.AttackTimer;
        if (timer is <= 0 or >= .7) return;
        double charge = 1 - timer / .7;
        Point focus = boss.Kind switch
        {
            AlienBossKind.SludgeMaw => new Point(0, 25),
            AlienBossKind.EyeTyrant => new Point(1, 0),
            AlienBossKind.BoneBroodmother => new Point(0, 27),
            _ => new Point(0, 0)
        };
        byte alpha = (byte)(70 + charge * 145);
        var glow = new RadialGradientBrush(Color.FromArgb(alpha, 255, 245, 192),
            Color.FromArgb(0, tint.R, tint.G, tint.B));
        double radius = 14 + charge * 14;
        dc.DrawEllipse(glow, null, focus, radius, radius);
    }

    internal void DrawBossBody(GameView view, DrawingContext dc, AlienBoss boss)
    {
        switch (boss.Kind)
        {
            case AlienBossKind.SludgeMaw: DrawSludgeMaw(view, dc, boss); break;
            case AlienBossKind.EyeTyrant: DrawEyeTyrant(view, dc, boss); break;
            case AlienBossKind.BoneBroodmother: DrawBoneBroodmother(view, dc, boss); break;
            case AlienBossKind.VoidLeech: DrawVoidLeech(view, dc, boss); break;
        }
    }

    private void DrawSludgeMaw(GameView view, DrawingContext dc, AlienBoss boss)
    {
        for (int i = 0; i < 6; i++)
            DrawOrganicTentacle(view, dc, i * Math.PI / 3 + .18, 54 + i % 2 * 13,
                Math.Sin(boss.Age * 2.2 + i) * 10, Color.FromRgb(77, 124, 39), 10 - i % 3);

        var points = new (double x, double y)[18];
        for (int i = 0; i < points.Length; i++)
        {
            double angle = i * Math.PI * 2 / points.Length;
            double radius = 61 + Math.Sin(angle * 5 + boss.Age * 2.3) * 7 + Math.Sin(angle * 9) * 4;
            points[i] = (Math.Cos(angle) * radius, Math.Sin(angle) * radius * .84);
        }
        var flesh = new RadialGradientBrush
        {
            GradientOrigin = new Point(.34, .28),
            Center = new Point(.42, .38),
            RadiusX = .72,
            RadiusY = .72
        };
        flesh.GradientStops.Add(new GradientStop(Color.FromRgb(209, 241, 105), 0));
        flesh.GradientStops.Add(new GradientStop(Color.FromRgb(92, 158, 48), .52));
        flesh.GradientStops.Add(new GradientStop(Color.FromRgb(27, 66, 25), 1));
        Geometry fleshShape = view.Polygon(points);
        dc.DrawGeometry(flesh, new Pen(new SolidColorBrush(Color.FromRgb(166, 235, 91)), 2.2), fleshShape);
        DrawBossSurfaceTexture(dc, fleshShape, new Point(), 58, 47,
            Color.FromRgb(31, 74, 24), Color.FromRgb(218, 251, 128), 17, 44);

        for (int i = 0; i < 8; i++)
        {
            double angle = i * Math.PI / 4 + .18;
            Point inner = new(Math.Cos(angle) * 12, Math.Sin(angle) * 9);
            Point outer = new(Math.Cos(angle) * (47 + i % 2 * 5), Math.Sin(angle) * (35 + i % 3 * 4));
            var vein = new Pen(new SolidColorBrush(Color.FromArgb(125, 48, 91, 34)), 1.15)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            dc.DrawLine(vein, inner, outer);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(90, 166, 222, 76)), .7),
                new Point((inner.X + outer.X) * .58, (inner.Y + outer.Y) * .58),
                new Point(outer.X - Math.Sin(angle) * 7, outer.Y + Math.Cos(angle) * 7));
        }
        dc.DrawEllipse(new RadialGradientBrush(Color.FromArgb(115, 239, 255, 170), Colors.Transparent),
            null, new Point(-18, -14), 31, 22);

        for (int i = 0; i < 9; i++)
        {
            double angle = i * 2.31;
            Point pustule = new(Math.Cos(angle) * (28 + i % 3 * 8), Math.Sin(angle) * (20 + i % 2 * 10));
            dc.DrawEllipse(new RadialGradientBrush(Color.FromRgb(235, 255, 139), Color.FromRgb(63, 105, 38)),
                new Pen(new SolidColorBrush(Color.FromRgb(35, 73, 29)), 1), pustule, 4 + i % 2, 3 + i % 3);
        }
        for (int i = -1; i <= 1; i++)
        {
            double sway = Math.Sin(boss.Age * 2.7 + i) * 3;
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(81, 142, 46)), 6),
                new Point(i * 25, -30), new Point(i * 31 + sway, -58 - Math.Abs(i) * 4));
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(232, 245, 151)),
                new Pen(new SolidColorBrush(Color.FromRgb(30, 57, 22)), 2), new Point(i * 31 + sway, -61 - Math.Abs(i) * 4), 10, 9);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(16, 24, 12)), null,
                new Point(i * 31 + sway + 2, -61 - Math.Abs(i) * 4), 4, 5);
        }
        dc.DrawEllipse(new RadialGradientBrush(Color.FromRgb(45, 13, 18), Colors.Black),
            new Pen(new SolidColorBrush(Color.FromRgb(42, 69, 28)), 3), new Point(4, 15), 35, 26);
        for (int i = 0; i < 8; i++)
        {
            double x = -24 + i * 8;
            Geometry tooth = i % 2 == 0
                ? view.Polygon((x, -5), (x + 6, -5), (x + 3, 8))
                : view.Polygon((x, 35), (x + 6, 35), (x + 3, 23));
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(232, 225, 155)), null, tooth);
        }
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(190, 160, 245, 74)), 3),
            new Point(14, 38), new Point(12 + Math.Sin(boss.Age * 3) * 4, 61));
        for (int i = 0; i < 3; i++)
        {
            double bubble = 2.2 + i * 1.1 + Math.Sin(boss.Age * 5 + i) * .45;
            dc.DrawEllipse(new RadialGradientBrush(Color.FromRgb(207, 255, 119), Color.FromRgb(55, 104, 31)),
                new Pen(new SolidColorBrush(Color.FromArgb(170, 191, 242, 102)), .7),
                new Point(17 + i * 5, 51 + i * 8), bubble, bubble);
        }
    }

    private void DrawEyeTyrant(GameView view, DrawingContext dc, AlienBoss boss)
    {
        for (int i = 0; i < 10; i++)
            DrawOrganicTentacle(view, dc, i * Math.PI * .2, 70 + i % 3 * 9,
                Math.Sin(boss.Age * 2.8 + i * .8) * 14, Color.FromRgb(100, 42, 120), 8 - i % 2);

        var skin = new RadialGradientBrush
        {
            GradientOrigin = new Point(.32, .24),
            Center = new Point(.4, .34),
            RadiusX = .74,
            RadiusY = .74
        };
        skin.GradientStops.Add(new GradientStop(Color.FromRgb(229, 136, 236), 0));
        skin.GradientStops.Add(new GradientStop(Color.FromRgb(122, 50, 142), .55));
        skin.GradientStops.Add(new GradientStop(Color.FromRgb(38, 14, 65), 1));
        var skinShape = new EllipseGeometry(new Point(), 59, 52);
        dc.DrawGeometry(skin, new Pen(new SolidColorBrush(Color.FromRgb(220, 118, 244)), 2.2), skinShape);
        DrawBossSurfaceTexture(dc, skinShape, new Point(), 56, 49,
            Color.FromRgb(67, 18, 81), Color.FromRgb(248, 169, 239), 31, 40);
        for (int i = 0; i < 12; i++)
        {
            double a = i * Math.PI / 6 + .2;
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(135, 75, 20, 74)), 1.2),
                new Point(Math.Cos(a) * 28, Math.Sin(a) * 22),
                new Point(Math.Cos(a) * 51, Math.Sin(a) * 44));
        }
        var scleraShape = new EllipseGeometry(new Point(3, 0), 39, 29);
        dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(241, 230, 184)),
            new Pen(new SolidColorBrush(Color.FromRgb(70, 28, 75)), 3), scleraShape);
        DrawBossSurfaceTexture(dc, scleraShape, new Point(3, 0), 36, 26,
            Color.FromRgb(151, 46, 78), Color.FromRgb(255, 249, 211), 43, 18);
        for (int i = 0; i < 10; i++)
        {
            double angle = i * Math.PI / 5;
            Point outer = new(3 + Math.Cos(angle) * 36, Math.Sin(angle) * 26);
            Point inner = new(5 + Math.Cos(angle) * 22, Math.Sin(angle) * 18);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(150, 186, 73, 107)), 1), outer, inner);
        }
        dc.DrawEllipse(new RadialGradientBrush(Color.FromRgb(255, 229, 101), Color.FromRgb(146, 45, 14)),
            null, new Point(7, 0), 18, 18);
        dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(210, 255, 188, 63)), 1.4),
            new Point(7, 0), 13, 13);
        dc.DrawArc(new Pen(new SolidColorBrush(Color.FromArgb(210, 255, 240, 151)), 1.8),
            new Point(7, 0), 21, boss.Age * 36, 116);
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(9, 4, 15)), null, new Point(10, 0), 7, 17);
        dc.DrawEllipse(Brushes.White, null, new Point(3, -7), 5, 3.5);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(190, 255, 255, 255)), null, new Point(7, -3), 2.2, 4.2);
        for (int i = 0; i < 5; i++)
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(245, 135, 196)),
                new Pen(new SolidColorBrush(Color.FromRgb(73, 25, 76)), .8),
                new Point(-42 + i * 19, 35 + Math.Sin(i) * 4), 5, 4);
    }

    private void DrawBoneBroodmother(GameView view, DrawingContext dc, AlienBoss boss)
    {
        for (int i = 0; i < 8; i++)
            DrawOrganicTentacle(view, dc, i * Math.PI / 4 + .2, 50 + i % 2 * 14,
                Math.Sin(boss.Age * 1.7 + i) * 8, Color.FromRgb(112, 38, 29), 7);

        var body = new RadialGradientBrush(Color.FromRgb(255, 150, 92), Color.FromRgb(75, 18, 24));
        var bodyShape = new EllipseGeometry(new Point(-3, 3), 65, 51);
        dc.DrawGeometry(body, new Pen(new SolidColorBrush(Color.FromRgb(255, 174, 97)), 2), bodyShape);
        DrawBossSurfaceTexture(dc, bodyShape, new Point(-3, 3), 61, 47,
            Color.FromRgb(78, 21, 27), Color.FromRgb(255, 193, 118), 59, 46);
        for (int i = 0; i < 5; i++)
        {
            double y = -34 + i * 16;
            double width = 31 + Math.Sin(i * 1.7) * 5;
            Geometry carapace = view.Polygon((-width, y - 7), (0, y - 12), (width, y - 7),
                (width - 7, y + 6), (0, y + 10), (-width + 7, y + 6));
            dc.DrawGeometry(new LinearGradientBrush(Color.FromArgb(145, 250, 205, 151), Color.FromArgb(95, 88, 38, 37), 90),
                new Pen(new SolidColorBrush(Color.FromArgb(150, 255, 194, 119)), .8), carapace);
            DrawBossSurfaceTexture(dc, carapace, new Point(0, y), width * .88, 8,
                Color.FromRgb(78, 43, 39), Color.FromRgb(255, 225, 170), 71 + i, 8);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            Geometry plate = view.Polygon((side * 8, -45), (side * 49, -35), (side * 69, -10),
                (side * 53, 2), (side * 23, -10));
            dc.DrawGeometry(new LinearGradientBrush(Color.FromRgb(246, 225, 173), Color.FromRgb(126, 85, 61), 90),
                new Pen(new SolidColorBrush(Color.FromRgb(255, 211, 143)), 1.5), plate);
            Geometry mandible = view.Polygon((side * 19, 22), (side * 61, 34), (side * 75, 52),
                (side * 48, 45), (side * 6, 32));
            dc.DrawGeometry(new LinearGradientBrush(Color.FromRgb(230, 205, 152), Color.FromRgb(92, 49, 39), 45),
                new Pen(new SolidColorBrush(Color.FromRgb(255, 188, 110)), 1.4), mandible);
            for (int rib = 0; rib < 3; rib++)
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(170, 255, 218, 157)), 1.1),
                    new Point(side * (18 + rib * 8), -23 + rib * 15),
                    new Point(side * (48 + rib * 4), -16 + rib * 14));
        }
        for (int i = 0; i < 6; i++)
        {
            double x = -31 + i * 12.5;
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 231, 96)),
                new Pen(new SolidColorBrush(Color.FromRgb(72, 19, 22)), 1.4), new Point(x, 3 + Math.Abs(i - 2.5) * 2), 5.5, 4.5);
            dc.DrawEllipse(Brushes.Black, null, new Point(x + 1, 4 + Math.Abs(i - 2.5) * 2), 2, 2.7);
        }
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(39, 6, 12)),
            new Pen(new SolidColorBrush(Color.FromRgb(218, 76, 56)), 2), new Point(0, 27), 22, 13);
        for (int i = 0; i < 7; i++)
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(244, 224, 169)), null,
                view.Polygon((-17 + i * 5.5, 17), (-13 + i * 5.5, 17), (-15 + i * 5.5, 28)));
    }

    private void DrawVoidLeech(GameView view, DrawingContext dc, AlienBoss boss)
    {
        var connectiveTissue = new Pen(new LinearGradientBrush(Color.FromRgb(83, 205, 186), Color.FromRgb(18, 62, 73), 45), 15)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        for (int i = 0; i < 9; i++)
        {
            double a0 = i * Math.PI * 2 / 9 + boss.Age * .12;
            double a1 = (i + 1) * Math.PI * 2 / 9 + boss.Age * .12;
            Point p0 = new(Math.Cos(a0) * (47 + Math.Sin(boss.Age * 2 + i) * 8),
                Math.Sin(a0) * (47 + Math.Sin(boss.Age * 2 + i) * 8) * .72);
            Point p1 = new(Math.Cos(a1) * (47 + Math.Sin(boss.Age * 2 + i + 1) * 8),
                Math.Sin(a1) * (47 + Math.Sin(boss.Age * 2 + i + 1) * 8) * .72);
            dc.DrawLine(connectiveTissue, p0, p1);
        }
        for (int i = 0; i < 9; i++)
        {
            double angle = i * Math.PI * 2 / 9 + boss.Age * .12;
            double radius = 47 + Math.Sin(boss.Age * 2 + i) * 8;
            Point segment = new(Math.Cos(angle) * radius, Math.Sin(angle) * radius * .72);
            double size = 24 - i % 3 * 3;
            var segmentShape = new EllipseGeometry(segment, size, size * .72);
            dc.DrawGeometry(new RadialGradientBrush(Color.FromRgb(105, 240, 210), Color.FromRgb(13, 59, 69)),
                new Pen(new SolidColorBrush(Color.FromRgb(96, 255, 223)), 1.4), segmentShape);
            DrawBossSurfaceTexture(dc, segmentShape, segment, size * .9, size * .62,
                Color.FromRgb(4, 47, 54), Color.FromRgb(164, 255, 229), 89 + i, 7);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(170, 9, 41, 48)), null, segment, size * .43, size * .3);
            dc.DrawArc(new Pen(new SolidColorBrush(Color.FromArgb(165, 189, 255, 234)), .9),
                segment, size * .72, boss.Age * 42 + i * 31, 105);
        }
        var leechCoreShape = new EllipseGeometry(new Point(), 45, 42);
        dc.DrawGeometry(new RadialGradientBrush(Color.FromRgb(72, 174, 160), Color.FromRgb(3, 19, 28)),
            new Pen(new SolidColorBrush(Color.FromRgb(93, 246, 218)), 2.4), leechCoreShape);
        DrawBossSurfaceTexture(dc, leechCoreShape, new Point(), 42, 39,
            Color.FromRgb(2, 32, 41), Color.FromRgb(129, 248, 219), 113, 36);
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(23, 2, 15)),
            new Pen(new SolidColorBrush(Color.FromRgb(198, 77, 113)), 3), new Point(), 31, 29);
        for (int i = 0; i < 16; i++)
        {
            double angle = i * Math.PI / 8;
            V2 outer = V2.FromAngle(angle) * 29;
            V2 inner = V2.FromAngle(angle) * (i % 2 == 0 ? 13 : 17);
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(220, 235, 179)), null,
                view.Polygon((outer.X + Math.Sin(angle) * 3, outer.Y - Math.Cos(angle) * 3),
                    (outer.X - Math.Sin(angle) * 3, outer.Y + Math.Cos(angle) * 3), (inner.X, inner.Y)));
        }
        dc.DrawEllipse(new RadialGradientBrush(Color.FromRgb(118, 24, 66), Colors.Black), null, new Point(), 12, 12);
        for (int ring = 0; ring < 3; ring++)
            dc.DrawArc(new Pen(new SolidColorBrush(Color.FromArgb((byte)(155 - ring * 34), 255, 85, 153)), 1.2),
                new Point(), 15 + ring * 5, -boss.Age * (44 + ring * 8) + ring * 57, 128);
        for (int i = 0; i < 4; i++)
        {
            double a = i * Math.PI / 2 + .5;
            Point eye = new(Math.Cos(a) * 38, Math.Sin(a) * 35);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 97, 139)), null, eye, 3.5, 3.5);
        }
    }

    private void DrawBossSurfaceTexture(DrawingContext dc,
        Geometry clip,
        Point center,
        double radiusX,
        double radiusY,
        Color shadow,
        Color highlight,
        int seed,
        int detailCount)
    {
        dc.PushClip(clip);
        for (int i = 0; i < detailCount; i++)
        {
            double angle = TextureNoise(seed, i, 1) * Math.PI * 2;
            double distance = Math.Sqrt(TextureNoise(seed, i, 2)) * .94;
            Point spot = new(
                center.X + Math.Cos(angle) * radiusX * distance,
                center.Y + Math.Sin(angle) * radiusY * distance);
            double size = .55 + TextureNoise(seed, i, 3) * 1.8;
            byte alpha = (byte)(38 + TextureNoise(seed, i, 4) * 58);
            Color poreColor = i % 4 == 0
                ? Color.FromArgb((byte)Math.Min(118, alpha + 20), highlight.R, highlight.G, highlight.B)
                : Color.FromArgb(alpha, shadow.R, shadow.G, shadow.B);
            dc.DrawEllipse(new SolidColorBrush(poreColor), null, spot, size, size * (.55 + TextureNoise(seed, i, 5) * .55));

            if (i % 7 != 0) continue;
            double scratchAngle = TextureNoise(seed, i, 6) * Math.PI;
            double scratchLength = 4 + TextureNoise(seed, i, 7) * 10;
            V2 direction = V2.FromAngle(scratchAngle);
            var scratchPen = new Pen(new SolidColorBrush(Color.FromArgb(72, highlight.R, highlight.G, highlight.B)), .65)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            dc.DrawLine(scratchPen,
                new Point(spot.X - direction.X * scratchLength * .5, spot.Y - direction.Y * scratchLength * .5),
                new Point(spot.X + direction.X * scratchLength * .5, spot.Y + direction.Y * scratchLength * .5));
        }
        dc.Pop();
    }

    private double TextureNoise(int seed, int index, int channel)
    {
        double value = Math.Sin(seed * 91.731 + index * 17.113 + channel * 43.717) * 43758.5453;
        return value - Math.Floor(value);
    }

    private void DrawOrganicTentacle(GameView view, DrawingContext dc, double angle, double length, double wave, Color color, double width)
    {
        V2 direction = V2.FromAngle(angle);
        V2 normal = new(-direction.Y, direction.X);
        Point start = new(direction.X * 34, direction.Y * 30);
        Point c1 = new(direction.X * length * .55 + normal.X * wave, direction.Y * length * .55 + normal.Y * wave);
        Point c2 = new(direction.X * length * .86 - normal.X * wave * .45, direction.Y * length * .86 - normal.Y * wave * .45);
        Point end = new(direction.X * length, direction.Y * length);
        var path = new StreamGeometry();
        using (StreamGeometryContext context = path.Open())
        {
            context.BeginFigure(start, false, false);
            context.BezierTo(c1, c2, end, true, false);
        }
        var shadowPen = new Pen(new SolidColorBrush(Color.FromArgb(170, view.Darken(color, .62).R,
            view.Darken(color, .62).G, view.Darken(color, .62).B)), width + 3)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawGeometry(null, shadowPen, path);
        var pen = new Pen(new LinearGradientBrush(view.Lighten(color, .34), view.Darken(color, .4), angle * 180 / Math.PI), width)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawGeometry(null, pen, path);
        dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(105, view.Lighten(color, .62).R,
            view.Lighten(color, .62).G, view.Lighten(color, .62).B)), Math.Max(1, width * .18))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        }, path);
        dc.DrawEllipse(new SolidColorBrush(view.Lighten(color, .25)), null, end, width * .36, width * .36);
    }

    internal Color BossColor(AlienBossKind kind) => kind switch
    {
        AlienBossKind.SludgeMaw => Color.FromRgb(143, 232, 79),
        AlienBossKind.EyeTyrant => Color.FromRgb(217, 118, 255),
        AlienBossKind.BoneBroodmother => Color.FromRgb(255, 140, 77),
        _ => Color.FromRgb(86, 241, 210)
    };
}
