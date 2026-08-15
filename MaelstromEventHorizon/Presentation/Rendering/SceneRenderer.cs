using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MaelstromEventHorizon.Application;
using MaelstromEventHorizon.Domain.Effects;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;
using MaelstromEventHorizon.Presentation.Drawing;

namespace MaelstromEventHorizon.Presentation.Rendering;

internal sealed class SceneRenderer
{
    private static readonly V2[] BackgroundPanDirections =
    [
        new(1, 0),
        new(.707, .707),
        new(0, 1),
        new(-.707, .707),
        new(-1, 0),
        new(-.707, -.707),
        new(0, -1),
        new(.707, -.707)
    ];

    internal void DrawGameCanvas(GameView view, DrawingContext dc)
    {
        view.TransparentEffects.Reset(view.Game.VisualQuality);
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, GameEngine.Width, GameEngine.Height)));
        DrawBackdrop(view, dc);
        bool waveCameraActive = TryPushWaveTransitionCamera(view, dc);
        V2 shake = view.Game.ScreenShakeOffset;
        dc.PushTransform(new TranslateTransform(shake.X, shake.Y));
        DrawStars(view, dc);
        DrawBonusStageEnvironment(view, dc);
        view.DrawVortices(dc);
        view.DrawNovas(dc);
        view.DrawComets(dc);
        view.DrawPickups(dc);
        view.DrawAsteroids(dc);
        view.DrawFighters(dc);
        view.DrawBosses(dc);
        view.DrawMines(dc);
        view.DrawShots(dc);
        view.DrawShip(dc);
        view.DrawShipDebris(dc);
        view.DrawParticles(dc);
        view.DrawShockwaves(dc);
        view.DrawFloatingTexts(dc);
        dc.Pop();

        if (waveCameraActive)
        {
            dc.Pop();
        }

        view.DrawHud(dc);
        view.DrawOverlay(dc);
        dc.Pop();

        if (view.Game.RicochetArenaActive)
        {
            view.DrawArenaFrame(dc);
        }

        view.DrawTransitionCurtain(dc);
    }

    private static bool TryPushWaveTransitionCamera(GameView view, DrawingContext dc)
    {
        if (view.Game.Mode is not (GameMode.WaveIntro or GameMode.WaveOutro))
        {
            return false;
        }

        bool outro = view.Game.Mode == GameMode.WaveOutro;
        double duration = outro ? 2.1 : GameEngine.WaveFadeInDuration;
        double elapsed = outro ? view.Game.TransitionElapsed - GameEngine.WaveExitDelay : view.Game.TransitionElapsed;
        double progress = Math.Clamp(elapsed / duration, 0, 1);
        double easedProgress = progress * progress * (3 - 2 * progress);
        double zoom = outro ? 1 + 2.5 * easedProgress : 3.1 - 2.1 * easedProgress;
        V2 ship = view.Game.Player.Position;
        double focusX = ship.X;
        double focusY = ship.Y;

        if (outro && view.Game.TransitionElapsed < GameEngine.WaveExitDelay)
        {
            double panProgress = Math.Clamp(view.Game.TransitionElapsed / GameEngine.WaveExitDelay, 0, 1);
            double easedPan = panProgress * panProgress * (3 - 2 * panProgress);
            focusX = GameEngine.Width * .5 + (ship.X - GameEngine.Width * .5) * easedPan;
            focusY = GameEngine.Height * .5 + (ship.Y - GameEngine.Height * .5) * easedPan;
        }

        dc.PushTransform(new MatrixTransform(new Matrix(
            zoom, 0, 0, zoom, GameEngine.Width * .5 - focusX * zoom, GameEngine.Height * .5 - focusY * zoom)));

        return true;
    }

    private void DrawBackdrop(GameView view, DrawingContext dc)
    {
        bool titleScene = view.Game.Mode is GameMode.Title or GameMode.Controls;
        int waveIndex = Math.Max(0, view.Game.Wave - 1);

        BitmapSource? selectedBackground = titleScene
            ? view.Background
            : view.WaveBackgrounds[waveIndex % view.WaveBackgrounds.Length] ?? view.Background;

        if (selectedBackground is not null)
        {
            int cycle = waveIndex / view.WaveBackgrounds.Length;
            double overscan = 62 + waveIndex % 4 * 4;
            V2 panDirection = BackgroundPanDirections[waveIndex % BackgroundPanDirections.Length];
            double travel = (view.Game.TotalTime * .05 + waveIndex * .37) % 2;
            double drift = titleScene ? 0 : (1 - Math.Abs(travel - 1) * 2) * (42 + waveIndex % 3 * 5);
            double panX = panDirection.X * drift;
            double panY = panDirection.Y * drift;

            dc.PushTransform(new ScaleTransform(
                cycle % 2 == 1 ? -1 : 1, cycle % 4 >= 2 ? -1 : 1, GameEngine.Width / 2, GameEngine.Height / 2));

            dc.DrawImage(selectedBackground,
                new Rect(
                    -overscan + panX, -overscan + panY, GameEngine.Width + overscan * 2,
                    GameEngine.Height + overscan * 2));

            dc.Pop();

            Color grade = GameView.WaveGrades[waveIndex % GameView.WaveGrades.Length];

            dc.DrawRectangle(
                view.Brush(Color.FromArgb(titleScene ? (byte)36 : (byte)58, grade.R, grade.G, grade.B)), null,
                new Rect(0, 0, GameEngine.Width, GameEngine.Height));

            dc.DrawRectangle(
                view.Brush(Color.FromArgb(titleScene ? (byte)30 : (byte)52, 0, 2, 8)), null,
                new Rect(0, 0, GameEngine.Width, GameEngine.Height));
        }
        else
        {
            RadialGradientBrush fallback = new(Color.FromRgb(12, 25, 57), Color.FromRgb(0, 2, 9))
            { RadiusX = .82, RadiusY = .82 };

            dc.DrawRectangle(fallback, null, new Rect(0, 0, GameEngine.Width, GameEngine.Height));
        }

        dc.DrawRectangle(GameView.VignetteBrush, null, new Rect(0, 0, GameEngine.Width, GameEngine.Height));

        if (view.Game.Mode == GameMode.WaveOutro)
        {
            double progress = Math.Clamp((view.Game.TransitionElapsed - GameEngine.WaveExitDelay) / 2.1, 0, 1);
            byte alpha = (byte)(148 * progress * progress);

            dc.DrawRectangle(
                view.Brush(Color.FromArgb(alpha, 0, 2, 8)), null,
                new Rect(0, 0, GameEngine.Width, GameEngine.Height));
        }
    }

    private void DrawStars(GameView view, DrawingContext dc)
    {
        // The outro/intro camera can look outside the playfield as it follows the ship.
        // Repeat the star field in every direction so that space continues beyond the
        // original screen-sized field instead of revealing an empty area.
        int starFieldTiles = view.Game.Mode is GameMode.WaveIntro or GameMode.WaveOutro ? 1 : 0;

        foreach (Star star in view.Game.Stars)
        {
            double depthSpeed = .45 + star.Depth * .72;
            double x = star.Position.X;
            double y = star.Position.Y;

            if (view.Game.IsBonusStage)
            {
                Vector starDrift = view.Game.BonusStageVariant switch
                {
                    BonusStageKind.Crossfire => new Vector(Math.Sin(star.Phase) * 110, Math.Cos(star.Phase) * 95),
                    BonusStageKind.SlalomGates => new Vector(-138, 0),
                    BonusStageKind.SpiralSwarm => new Vector(-52, 78),
                    _ => new Vector(-92, 56)
                };

                x = PositiveModulo(x + view.Game.BonusTravelTime * starDrift.X * depthSpeed, GameEngine.Width);
                y = PositiveModulo(y + view.Game.BonusTravelTime * starDrift.Y * depthSpeed, GameEngine.Height);
            }

            double twinkle = .48 + .52 * Math.Sin(view.Game.TotalTime * (1.2 + star.Depth * 2.5) + star.Phase);
            double size = .65 + star.Depth * 1.55 + twinkle * .6;
            byte alpha = (byte)(85 + twinkle * 155);

            SolidColorBrush brush = view.Brush(Color.FromArgb(
                alpha, (byte)(180 + star.Depth * 70), (byte)(205 + star.Depth * 45), 255));

            for (int tileY = -starFieldTiles; tileY <= starFieldTiles; tileY++)
            {
                for (int tileX = -starFieldTiles; tileX <= starFieldTiles; tileX++)
                {
                    double starX = x + tileX * GameEngine.Width;
                    double starY = y + tileY * GameEngine.Height;
                    Point position = new(starX, starY);
                    dc.DrawEllipse(brush, null, position, size, size);

                    if (view.Game.Player.Thrusting && star.Depth > .56 && !view.Game.IsBonusStage)
                    {
                        double streak = (star.Depth - .45) * 20;

                        dc.DrawLine(view.Pen(
                            Color.FromArgb((byte)(alpha * .38), 146, 212, 255), .55 + star.Depth * .6),
                            new Point(starX - streak, starY), new Point(starX + size, starY));
                    }

                    if (star.Depth > .82 && twinkle > .78)
                    {
                        Pen pen = view.Pen(Color.FromArgb((byte)(alpha / 2), 160, 205, 255), .7);
                        dc.DrawLine(pen, new Point(starX - size * 3, starY), new Point(starX + size * 3, starY));
                        dc.DrawLine(pen, new Point(starX, starY - size * 3), new Point(starX, starY + size * 3));
                    }
                }
            }
        }
    }

    private void DrawBonusStageEnvironment(GameView view, DrawingContext dc)
    {
        if (!view.Game.IsBonusStage)
        {
            return;
        }

        Color guide = view.Game.BonusStageVariant switch
        {
            BonusStageKind.Crossfire => Color.FromRgb(255, 104, 116),
            BonusStageKind.SlalomGates => Color.FromRgb(97, 243, 188),
            BonusStageKind.SpiralSwarm => Color.FromRgb(210, 126, 255),
            _ => Color.FromRgb(105, 219, 255)
        };

        if (view.Game.BonusStageVariant == BonusStageKind.SlalomGates)
        {
            for (int i = 1; i < 9; i++)
            {
                dc.DrawLine(
                    view.Pen(Color.FromArgb(23, guide.R, guide.G, guide.B), 1),
                    new Point(0, i * GameEngine.Height / 9), new Point(GameEngine.Width, i * GameEngine.Height / 9));
            }
        }
        else if (view.Game.BonusStageVariant == BonusStageKind.SpiralSwarm)
        {
            for (int i = 0; i < 5; i++)
            {
                dc.DrawArc(
                    view.Pen(Color.FromArgb((byte)(25 + i * 7), guide.R, guide.G, guide.B), 1.2),
                    new Point(GameEngine.Width / 2, GameEngine.Height / 2), 130 + i * 105,
                    view.Game.BonusTravelTime * (12 + i * 2) + i * 43, 215);
            }
        }
        else
        {
            double slope = view.Game.BonusStageVariant == BonusStageKind.Crossfire ? 0 : .56;

            for (int i = -3; i < 10; i++)
            {
                double y = PositiveModulo(i * 105 + view.Game.BonusTravelTime * 95, GameEngine.Height + 210) - 105;

                dc.DrawLine(
                    view.Pen(Color.FromArgb(25, guide.R, guide.G, guide.B), 1.2), new Point(0, y),
                    new Point(GameEngine.Width, y + GameEngine.Width * slope));
            }
        }
    }

    internal double PositiveModulo(double value, double modulus)
    {
        return (value % modulus + modulus) % modulus;
    }
}
