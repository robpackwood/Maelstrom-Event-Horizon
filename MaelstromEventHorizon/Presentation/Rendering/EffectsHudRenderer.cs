using System.Windows;
using System.Windows.Media;
using MaelstromEventHorizon.Application;
using MaelstromEventHorizon.Domain.Effects;
using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;
using MaelstromEventHorizon.Presentation.Drawing;

namespace MaelstromEventHorizon.Presentation.Rendering;

internal sealed class EffectsHudRenderer
{
    private static readonly Brush ArenaShade = FrozenBrush(Color.FromArgb(185, 1, 8, 14));

    private static readonly Pen ArenaRail =
        FrozenPen(new LinearGradientBrush(Color.FromRgb(21, 54, 64), Color.FromRgb(5, 21, 30), 90), 8);

    private static readonly Pen ArenaGlow = FrozenPen(new SolidColorBrush(Color.FromRgb(104, 245, 203)), 1.7);
    private static readonly Pen ArenaAccent = FrozenPen(new SolidColorBrush(Color.FromArgb(100, 255, 218, 89)), .8);
    private static readonly Brush ArenaCorner = FrozenBrush(Color.FromRgb(255, 218, 91));
    private static readonly Pen ArenaCornerOutline = FrozenPen(new SolidColorBrush(Color.FromRgb(255, 250, 194)), .8);
    private static readonly Brush ArenaCornerCore = FrozenBrush(Color.FromRgb(30, 63, 68));

    private static readonly Point[] ArenaCorners =
    [
        new(15, 15), new(GameEngine.Width - 15, 15), new(15, GameEngine.Height - 15),
        new(GameEngine.Width - 15, GameEngine.Height - 15)
    ];

    private readonly List<string> activePowerupLabels = new(9);
    private readonly Dictionary<(Color Color, int Power), Pen> shotOrbitPens = [];
    private readonly Dictionary<(Color Color, bool Enhanced), (Brush Glow, Brush Body, Pen Outline)> shotVisuals = [];

    internal void DrawFloatingTexts(GameView view, DrawingContext dc)
    {
        foreach (FloatingText text in view.Game.FloatingTexts)
        {
            double life = Math.Clamp(1 - text.Age / text.Lifetime, 0, 1);
            double y = text.Position.Y - text.Age * 34;

            if (!Visible(text.Position.X, y, 140))
            {
                continue;
            }

            byte alpha = (byte)(255 * Math.Min(1, life * 2.4));
            Color color = view.FromArgb(text.Color, alpha);

            view.DrawCenteredText(dc, text.Text, text.Position.X + 1.5, y + 1.5, 18,
                view.Brush(Color.FromArgb((byte)(alpha * .7), 2, 7, 14)), FontWeights.Black);

            view.DrawCenteredText(dc, text.Text, text.Position.X, y, 18, view.Brush(color), FontWeights.Black);
        }
    }

    internal void DrawShots(GameView view, DrawingContext dc)
    {
        foreach (Shot shot in view.Game.Shots)
        {
            if (view.Game.RicochetArenaActive)
            {
                DrawBeachBallShot(view, dc, shot);
                continue;
            }

            if (shot.Sludge)
            {
                DrawSludgeShot(view, dc, shot);
                continue;
            }

            double radius = shot.BossShot ? Math.Max(6.2, shot.Radius) : shot.Enemy ? 5.2 : Math.Max(4.3, shot.Radius);

            Color color = shot.BossShot ? view.FromArgb(shot.Tint) :
                shot.Enemy ? Color.FromRgb(83, 119, 255) : shot.PowerLevel switch
                {
                    1 => Color.FromRgb(53, 238, 210),
                    2 => Color.FromRgb(167, 116, 255),
                    3 => Color.FromRgb(255, 104, 168),
                    _ => Color.FromRgb(47, 201, 255)
                };

            bool enhanced = shot.BossShot || shot is { Enemy: false, PowerLevel: > 0 };
            (Brush Glow, Brush Body, Pen Outline) visual = GetShotVisual(view, color, enhanced, shot.PowerLevel);

            if (shot.Laser)
            {
                V2 tail = shot.Position - shot.Velocity.Normalized * (radius * 5.5);
                dc.DrawLine(new Pen(visual.Glow, radius * 1.8), view.Pt(tail), view.Pt(shot.Position));
                dc.DrawLine(new Pen(Brushes.White, Math.Max(1.2, radius * .42)), view.Pt(tail), view.Pt(shot.Position));
            }

            dc.DrawEllipse(visual.Glow, null, view.Pt(shot.Position), radius * 2.15, radius * 2.15);
            dc.DrawEllipse(visual.Body, visual.Outline, view.Pt(shot.Position), radius, radius);

            if (shot is { Enemy: false, PowerLevel: > 0 })
            {
                double orbit = radius * (1.28 + .05 * Math.Sin(view.Game.TotalTime * 18 + shot.Age * 9));

                dc.DrawArc(GetOrbitPen(color, shot.PowerLevel), view.Pt(shot.Position), orbit,
                    shot.Angle * 180 / Math.PI, 155 + shot.PowerLevel * 28);
            }
        }
    }

    private Pen GetOrbitPen(Color color, int power)
    {
        (Color color, int power) key = (color, power);

        if (shotOrbitPens.TryGetValue(key, out Pen? pen))
        {
            return pen;
        }

        return shotOrbitPens[key] =
            FrozenPen(new SolidColorBrush(Color.FromArgb((byte)(95 + power * 35), color.R, color.G, color.B)),
                .7 + power * .28);
    }

    private (Brush Glow, Brush Body, Pen Outline) GetShotVisual(GameView view, Color color, bool enhanced, int power)
    {
        (Color color, bool enhanced) key = (color, enhanced);

        if (shotVisuals.TryGetValue(key, out (Brush Glow, Brush Body, Pen Outline) cached))
        {
            return cached;
        }

        RadialGradientBrush glow = new();
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(105, color.R, color.G, color.B), 0));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(35, color.R, color.G, color.B), .48));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));

        RadialGradientBrush sphere = new()
        {
            GradientOrigin = new Point(.32, .27),
            Center = new Point(.4, .36),
            RadiusX = .7,
            RadiusY = .7
        };

        sphere.GradientStops.Add(new GradientStop(Colors.White, 0));

        sphere.GradientStops.Add(new GradientStop(enhanced
            ? view.Lighten(color, .58)
            : Color.FromRgb(151, 226, 255), .24));

        sphere.GradientStops.Add(new GradientStop(color, .6));

        sphere.GradientStops.Add(new GradientStop(enhanced
            ? view.Darken(color, .72)
            : Color.FromRgb(9, 31, 103), 1));

        glow.Freeze();
        sphere.Freeze();

        Pen outline = new(
            new SolidColorBrush(enhanced ? view.Lighten(color, .38) : Color.FromArgb(210, 174, 230, 255)),
            .65 + power * .18);

        outline.Freeze();
        return shotVisuals[key] = (glow, sphere, outline);
    }

    private void DrawSludgeShot(GameView view, DrawingContext dc, Shot shot)
    {
        double radius = Math.Max(4.2, shot.Radius);
        Color color = view.FromArgb(shot.Tint);
        V2 heading = shot.Velocity.Normalized;
        V2 trail = -heading;
        int trailDrops = shot.SludgeVomit ? 2 : 4;

        for (int i = trailDrops; i >= 1; i--)
        {
            double size = radius * (.18 + i * .08);

            V2 position = shot.Position + trail * (radius * (.55 + i * .54)) +
                          new V2(-heading.Y, heading.X) * Math.Sin(shot.Angle + i * 1.9) * radius * .2;

            dc.DrawEllipse(view.Brush(Color.FromArgb((byte)(35 + i * 17), color.R, color.G, color.B)),
                null, view.Pt(position), size, size * .8);
        }

        view.DrawGlowEllipse(dc, shot.Position, radius * 1.18, color, shot.SplitAge > 0 ? 4 : 2, .3);
        dc.PushTransform(new TranslateTransform(shot.Position.X, shot.Position.Y));
        dc.PushTransform(new RotateTransform(Math.Atan2(heading.Y, heading.X) * 180 / Math.PI));

        if (shot.SludgeVomit)
        {
            dc.PushTransform(new ScaleTransform(1.32, .82));
        }

        RadialGradientBrush body = new()
        {
            GradientOrigin = new Point(.28, .22),
            Center = new Point(.35, .3),
            RadiusX = .72,
            RadiusY = .72
        };

        body.GradientStops.Add(new GradientStop(Color.FromRgb(225, 255, 157), 0));
        body.GradientStops.Add(new GradientStop(view.Lighten(color, .28), .3));
        body.GradientStops.Add(new GradientStop(color, .67));
        body.GradientStops.Add(new GradientStop(view.Darken(color, .68), 1));
        int lobes = shot.SplitAge > 0 ? 7 : 4;

        for (int i = 0; i < lobes; i++)
        {
            double angle = i * Math.PI * 2 / lobes + shot.Angle * .31;
            double wobble = .68 + Math.Sin(shot.Age * 11 + i * 2.1) * .08;
            Point center = new(Math.Cos(angle) * radius * wobble, Math.Sin(angle) * radius * wobble);
            double lobe = radius * (.34 + .09 * Math.Sin(i * 3.7 + shot.Angle));
            dc.DrawEllipse(body, null, center, lobe, lobe * .9);
        }

        dc.DrawEllipse(body, new Pen(view.Brush(Color.FromArgb(205, 168, 235, 91)), .85),
            new Point(), radius, radius * .91);

        dc.DrawEllipse(view.Brush(Color.FromArgb(175, 230, 255, 161)), null,
            new Point(-radius * .3, -radius * .27), radius * .18, radius * .13);

        dc.DrawEllipse(view.Brush(Color.FromArgb(145, 42, 83, 27)), null,
            new Point(radius * .27, radius * .21), radius * .15, radius * .19);

        if (shot.SludgeVomit)
        {
            dc.Pop();
        }

        dc.Pop();
        dc.Pop();
    }

    private void DrawBeachBallShot(GameView view, DrawingContext dc, Shot shot)
    {
        double radius = Math.Max(7.5, shot.Radius * 1.35);
        Color glowColor = shot.Enemy ? Color.FromRgb(255, 111, 103) : Color.FromRgb(91, 226, 255);
        view.DrawGlowEllipse(dc, shot.Position, radius * 1.25, glowColor, 3, .35);
        dc.PushTransform(new TranslateTransform(shot.Position.X, shot.Position.Y));
        dc.PushTransform(new RotateTransform(shot.Angle * 180 / Math.PI));
        dc.PushClip(new EllipseGeometry(new Point(), radius, radius));
        dc.DrawEllipse(Brushes.White, null, new Point(), radius, radius);

        Color[] colors = shot.Enemy
            ? [Color.FromRgb(255, 76, 67), Color.FromRgb(255, 221, 75), Color.FromRgb(63, 174, 255)]
            : [Color.FromRgb(41, 196, 255), Color.FromRgb(255, 214, 64), Color.FromRgb(255, 91, 116)];

        for (int i = 0; i < 6; i++)
        {
            double a0 = (i * 60 - 8) * Math.PI / 180;
            double a1 = (i * 60 + 30) * Math.PI / 180;

            dc.DrawGeometry(view.Brush(colors[i % colors.Length]), null,
                view.Polygon((0, 0), (Math.Cos(a0) * radius * 1.35, Math.Sin(a0) * radius * 1.35),
                    (Math.Cos(a1) * radius * 1.35, Math.Sin(a1) * radius * 1.35)));
        }

        RadialGradientBrush shade = new()
        {
            GradientOrigin = new Point(.32, .25),
            Center = new Point(.42, .38),
            RadiusX = .72,
            RadiusY = .72
        };

        shade.GradientStops.Add(new GradientStop(Color.FromArgb(120, 255, 255, 255), 0));
        shade.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), .42));
        shade.GradientStops.Add(new GradientStop(Color.FromArgb(105, 0, 18, 36), 1));
        dc.DrawEllipse(shade, null, new Point(), radius, radius);
        dc.Pop();
        dc.DrawEllipse(null, new Pen(view.Brush(Color.FromRgb(232, 251, 255)), 1.1), new Point(), radius, radius);

        dc.DrawEllipse(Brushes.White, new Pen(view.Brush(Color.FromRgb(44, 95, 121)), .6), new Point(), radius * .22,
            radius * .22);

        dc.Pop();
        dc.Pop();
    }

    internal void DrawArenaFrame(DrawingContext dc)
    {
        const double inset = 10;
        Rect inner = new(inset, inset, GameEngine.Width - inset * 2, GameEngine.Height - inset * 2);
        dc.DrawRectangle(ArenaShade, null, new Rect(0, 0, GameEngine.Width, inset));
        dc.DrawRectangle(ArenaShade, null, new Rect(0, GameEngine.Height - inset, GameEngine.Width, inset));
        dc.DrawRectangle(ArenaShade, null, new Rect(0, 0, inset, GameEngine.Height));
        dc.DrawRectangle(ArenaShade, null, new Rect(GameEngine.Width - inset, 0, inset, GameEngine.Height));
        dc.DrawRoundedRectangle(null, ArenaRail, inner, 3, 3);
        dc.DrawRoundedRectangle(null, ArenaGlow, inner, 3, 3);

        dc.DrawRoundedRectangle(null, ArenaAccent,
            new Rect(14, 14, GameEngine.Width - 28, GameEngine.Height - 28), 2, 2);

        foreach (Point corner in ArenaCorners)
        {
            dc.DrawEllipse(ArenaCorner, ArenaCornerOutline, corner, 4, 4);
            dc.DrawEllipse(ArenaCornerCore, null, corner, 1.5, 1.5);
        }
    }

    private static SolidColorBrush FrozenBrush(Color color)
    {
        SolidColorBrush brush = new(color);
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(Brush brush, double thickness)
    {
        brush.Freeze();
        Pen pen = new(brush, thickness);
        pen.Freeze();
        return pen;
    }

    internal void DrawParticles(GameView view, DrawingContext dc)
    {
        int maximum = view.Game.VisualQuality switch { 0 => 220, 1 => 520, _ => 900 };
        int stride = Math.Max(1, (view.Game.Particles.Count + maximum - 1) / maximum);

        for (int index = 0; index < view.Game.Particles.Count; index += stride)
        {
            Particle p = view.Game.Particles[index];

            if (!Visible(p.Position.X, p.Position.Y, 42))
            {
                continue;
            }

            double life = Math.Clamp(1 - p.Age / p.Lifetime, 0, 1);
            Color c = view.FromArgb(p.Color, (byte)(255 * life));
            double r = p.StartSize * (.35 + life * .8);
            dc.DrawEllipse(view.Brush(c), null, view.Pt(p.Position), r, r);

            double haloScale = r > 3 && view.Game.VisualQuality > 0
                ? view.TransparentEffects.ReserveDisk(r * 2.8)
                : 0;

            if (haloScale > 0)
            {
                dc.DrawEllipse(view.Brush(Color.FromArgb((byte)(45 * life), c.R, c.G, c.B)), null, view.Pt(p.Position),
                    r * 2.8 * haloScale, r * 2.8 * haloScale);

                if (view.Game.VisualQuality > 1 && haloScale > .6)
                {
                    V2 trail = p.Velocity.Normalized * Math.Min(18, p.Velocity.Length * .045) * life;

                    dc.DrawLine(view.Pen(Color.FromArgb((byte)(105 * life), c.R, c.G, c.B), Math.Max(.65, r * .38)),
                        view.Pt(p.Position), view.Pt(p.Position - trail));
                }
            }
        }
    }

    internal void DrawShockwaves(GameView view, DrawingContext dc)
    {
        foreach (Shockwave ring in view.Game.Shockwaves)
        {
            double p = ring.Age / ring.Lifetime;
            double r = ring.MaxRadius * view.EaseOut(p);
            Color color = view.FromArgb(ring.Color, (byte)(220 * (1 - p)));
            double thickness = Math.Max(.7, 5 * (1 - p));

            if (!Visible(ring.Position.X, ring.Position.Y, r + 12))
            {
                continue;
            }

            double scale = view.TransparentEffects.ReserveRing(r, thickness);

            if (scale == 0)
            {
                continue;
            }

            dc.DrawEllipse(null, view.Pen(color, thickness * scale), view.Pt(ring.Position), r, r);
        }
    }

    private static bool Visible(double x, double y, double padding)
    {
        return x >= -padding && x <= GameEngine.Width + padding && y >= -padding && y <= GameEngine.Height + padding;
    }

    private static string BossLabel(GameView view, AlienBoss boss)
    {
        string name = view.Game.BossName(boss.Kind);
        return boss.IsRepeatEncounter ? $"MEGA {name}" : name;
    }

    internal void DrawHudFrame(GameView view, DrawingContext dc)
    {
        if (view.Game.Mode is GameMode.Title or GameMode.Controls)
        {
            return;
        }

        SolidColorBrush dim = view.Brush(Color.FromRgb(137, 168, 191));
        view.DrawText(dc, "SCORE", 30, 29, 12, dim, FontWeights.SemiBold);
        view.DrawText(dc, "LEVEL", 274, 29, 12, dim, FontWeights.SemiBold);
        view.DrawText(dc, view.Game.IsBonusStage ? "SHIELD OFFLINE" : "SHIELD", 30, 682, 12,
            view.Game.IsBonusStage ? view.Brush(Color.FromRgb(255, 132, 103)) : dim, FontWeights.SemiBold);

        double shieldBarX = view.Game.IsBonusStage ? 139 : 91;
        double shieldBarSpan = view.Game.IsBonusStage ? 132 : 180;
        dc.DrawRoundedRectangle(view.Brush(Color.FromArgb(125, 5, 17, 29)), view.Pen(Color.FromRgb(55, 91, 113), 1),
            new Rect(shieldBarX, 683, shieldBarSpan, 12), 3, 3);
    }

    internal void DrawHudValues(GameView view, DrawingContext dc)
    {
        if (view.Game.Mode is GameMode.Title or GameMode.Controls)
        {
            return;
        }

        view.DrawText(dc, view.Money(view.Game.Score), 88, 31, 20, Brushes.White, FontWeights.Bold);
        Brush levelBrush = view.Brush(view.Game.LevelBonusCash > 1_000
            ? Color.FromRgb(255, 221, 113)
            : Color.FromRgb(142, 169, 181));
        view.DrawText(dc, view.Money(view.Game.LevelBonusCash), 332, 31, 20, levelBrush, FontWeights.Bold);
        AlienBoss? activeBoss = null;

        for (int i = 0; i < view.Game.Bosses.Count; i++)
        {
            if (view.Game.Bosses[i].Alive)
            {
                activeBoss = view.Game.Bosses[i];
                break;
            }
        }

        string stageLabel = activeBoss is not null
            ? view.Game.BossOnlyMode
                ? $"BOSS ONLY {(view.Game.Wave - 1) / 5:00}  -  {BossLabel(view, activeBoss)}  -  TOP 10 DISABLED"
                : $"BOSS WAVE {view.Game.Wave:00}  -  {BossLabel(view, activeBoss)}"
            : view.Game.BonusOnlyMode
                ? $"BONUS ONLY {view.Game.Wave / 5:00}  -  {view.Game.BonusStageName}  -  DODGED {view.Game.BonusAsteroidsDodged:00}/{view.Game.BonusAsteroidTotal:00}"
                : view.Game.IsBonusStage
                    ? $"BONUS {view.Game.Wave:00}  -  {view.Game.BonusStageName}  -  DODGED {view.Game.BonusAsteroidsDodged:00}/{view.Game.BonusAsteroidTotal:00}"
                    : $"WAVE {view.Game.Wave:00}";

        bool expandedTopHud = view.Game.IsBonusStage || activeBoss is not null;
        double stageBaseline = expandedTopHud ? 72 : 29;

        view.DrawCenteredText(dc, stageLabel, GameEngine.Width / 2, stageBaseline, expandedTopHud ? 15 : 18,
            view.Brush(view.Game.IsBonusStage ? Color.FromRgb(255, 221, 113) :
                activeBoss is not null ? view.BossColor(activeBoss.Kind) : Color.FromRgb(191, 224, 241)),
            FontWeights.Bold);

        if (view.Game.IsBonusStage)
        {
            view.DrawCenteredText(dc, $"{view.Game.BonusStageObjective}   /   WEAPONS + SHIELDS OFFLINE" +
                                      (view.Game.BonusOnlyMode ? "   /   TOP 10 DISABLED" : ""),
                GameEngine.Width / 2, 91, 9.5, view.Brush(Color.FromRgb(255, 177, 108)), FontWeights.Black);
        }

        view.DrawText(dc, $"SHIPS  {view.Game.Lives}", 1138, 30, 17, Brushes.White, FontWeights.Bold);
        string renderer = view.UseGpuPlayfield ? "D3D" : view.HardwareRenderingTier == 0 ? "SOFTWARE" : $"WPF T{view.HardwareRenderingTier}";
        view.DrawText(dc, $"{view.FramesPerSecond:0} FPS  {renderer}", 1120, 51, 10,
            view.Brush(!view.UseGpuPlayfield && view.HardwareRenderingTier == 0
                ? Color.FromRgb(255, 164, 114) : Color.FromRgb(137, 210, 230)),
            FontWeights.SemiBold);
        if (activeBoss is not null)
        {
            const double bossBarWidth = 390;
            double health = Math.Clamp(activeBoss.HitPoints / (double)activeBoss.MaxHitPoints, 0, 1);
            Rect barRect = new(GameEngine.Width / 2 - bossBarWidth / 2, 88, bossBarWidth, 12);

            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(190, 4, 9, 14)),
                new Pen(new SolidColorBrush(Color.FromArgb(185, 202, 239, 245)), 1), barRect, 3, 3);

            LinearGradientBrush healthBrush = new(view.Lighten(view.BossColor(activeBoss.Kind), .35),
                view.Darken(view.BossColor(activeBoss.Kind), .42), 0);

            dc.DrawRoundedRectangle(healthBrush, null,
                new Rect(barRect.X + 2, barRect.Y + 2, (bossBarWidth - 4) * health, 8), 2, 2);
        }

        if (view.Game.BossCountdownActive)
        {
            int seconds = Math.Max(1, (int)Math.Ceiling(view.Game.BossCountdown));
            Brush countdownBrush = seconds == 1 ? Brushes.OrangeRed : Brushes.Gold;

            view.DrawCenteredText(dc, $"BOSS ENGAGES IN {seconds}", GameEngine.Width / 2, 330, 30, countdownBrush,
                FontWeights.Bold);

            view.DrawCenteredText(dc, "SHIP SYSTEMS LOCKED", GameEngine.Width / 2, 368, 14, Brushes.White,
                FontWeights.Bold);
        }

        double shieldBarX = view.Game.IsBonusStage ? 139 : 91;
        double shieldBarSpan = view.Game.IsBonusStage ? 132 : 180;

        double shieldWidth = Math.Max(0, (shieldBarSpan - 4) * view.Game.Player.Shield / 100);

        LinearGradientBrush shieldBrush = view.Game.IsBonusStage
            ? new LinearGradientBrush(Color.FromRgb(82, 88, 94), Color.FromRgb(47, 53, 61), 0)
            : new LinearGradientBrush(Color.FromRgb(54, 215, 255), Color.FromRgb(90, 124, 255), 0);

        dc.DrawRoundedRectangle(shieldBrush, null, new Rect(shieldBarX + 2, 685, shieldWidth, 8), 2, 2);

        if (view.Game.Multiplier > 1)
        {
            view.DrawText(dc, $"{view.Game.Multiplier}X", 1194, 674, 26, view.Brush(Color.FromRgb(220, 155, 255)),
                FontWeights.Bold);
        }

        if (view.Game.IsDemoMode)
        {
            double pulse = .78 + (Math.Sin(view.Game.TotalTime * 3.2) + 1) * .11;
            byte alpha = (byte)(255 * pulse);

            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(120, 2, 13, 23)),
                new Pen(new SolidColorBrush(Color.FromArgb(150, 87, 217, 247)), 1), new Rect(438, 48, 404, 31));

            view.DrawCenteredText(dc, "PRESS ANY KEY TO RETURN TO TITLE", GameEngine.Width / 2, 69, 13,
                new SolidColorBrush(Color.FromArgb(alpha, 187, 239, 252)), FontWeights.Bold);
        }

        List<string> active = activePowerupLabels;
        active.Clear();

        if (view.Game.ReflectionShieldActive)
        {
            active.Add("REFLECTION SHIELD");
        }

        if (view.Game.AirBrakesActive)
        {
            active.Add("AIR BRAKES");
        }

        if (view.Game.LuckActive)
        {
            active.Add("LUCK");
        }

        if (view.Game.TripleFireActive)
        {
            active.Add("TRIPLE FIRE");
        }

        if (view.Game.RiftVolleyActive)
        {
            active.Add("RIFT VOLLEY");
        }

        if (view.Game.LongRangeActive)
        {
            active.Add("LONG RANGE");
        }

        if (view.Game.LaserShotsActive)
        {
            active.Add("LASER SHOTS");
        }

        if (view.Game.DoubleShotSizeActive)
        {
            active.Add("DOUBLE SHOT SIZE");
        }

        if (view.Game.RicochetArenaActive)
        {
            active.Add("RICOCHET ARENA");
        }

        if (view.Game.Player.Giant)
        {
            active.Add("GIANT SHIP");
        }

        if (view.Game.FreezeTime > 0)
        {
            active.Add("TIME FREEZE");
        }

        if (active.Count > 0)
        {
            string activeLabel = string.Join("   |   ", active);
            double activeSize = active.Count >= 6 ? 9.5 : active.Count >= 5 ? 10.5 : 12;
            FormattedText measured = view.Format(activeLabel, activeSize, Brushes.White, FontWeights.SemiBold);

            if (measured.Width > 730)
            {
                activeSize = Math.Max(7.5, activeSize * 730 / measured.Width);
                measured = view.Format(activeLabel, activeSize, Brushes.White, FontWeights.SemiBold);
            }

            double panelWidth = Math.Min(760, measured.Width + 30);
            double panelPulse = .72 + .28 * Math.Sin(view.Game.TotalTime * 4.2);
            Color panelGlow = view.Game.FreezeTime > 0 ? Color.FromRgb(126, 240, 255) : Color.FromRgb(86, 193, 215);

            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(150, 1, 10, 18)),
                new Pen(
                    new SolidColorBrush(Color.FromArgb((byte)(90 + panelPulse * 80), panelGlow.R, panelGlow.G,
                        panelGlow.B)), 1 + panelPulse * .5),
                new Rect(GameEngine.Width / 2 - panelWidth / 2, 674, panelWidth, 29), 3, 3);

            view.DrawCenteredText(dc, activeLabel, GameEngine.Width / 2, 694, activeSize,
                new SolidColorBrush(Color.FromRgb(121, 232, 255)), FontWeights.SemiBold);
        }

        // Wave summaries provide their own title and status text. Suppress every transient gameplay
        // banner here so bonus failures, power-ups, and extra ships earned during cash counting
        // cannot overlap the summary.
        if (view.Game is { BannerTime: > 0, Mode: GameMode.Playing })
        {
            double alpha = Math.Min(1, view.Game.BannerTime * 2);
            SolidColorBrush b = new(Color.FromArgb((byte)(230 * alpha), 230, 246, 255));

            view.DrawCenteredText(dc, view.Game.Banner, GameEngine.Width / 2, expandedTopHud ? 133 : 105, 24, b,
                FontWeights.Bold);
        }
    }
}
