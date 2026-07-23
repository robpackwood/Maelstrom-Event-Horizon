using MaelstromEventHorizon.Application;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;
using MaelstromEventHorizon.Presentation.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MaelstromEventHorizon.Presentation.Rendering;

internal sealed class SceneRenderer
{
    internal void DrawGameCanvas(GameView view, DrawingContext dc)
    {
        DrawBackdrop(view, dc);
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
        view.DrawHud(dc);
        view.DrawOverlay(dc);
        dc.Pop();
        if (view.Game.RicochetArenaActive) view.DrawArenaFrame(dc);
        view.DrawTransitionCurtain(dc);
    }

    internal void DrawRetroFrame(GameView view, DrawingContext dc)
    {
        const int pixelWidth = 640;
        const int pixelHeight = 360;
        var visual = new DrawingVisual();
        using (DrawingContext lowResolution = visual.RenderOpen())
        {
            lowResolution.PushTransform(new ScaleTransform(
                pixelWidth / GameEngine.Width, pixelHeight / GameEngine.Height));
            DrawGameCanvas(view, lowResolution);
            lowResolution.Pop();
        }

        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        var reducedColor = new FormatConvertedBitmap(bitmap, PixelFormats.Bgr565, null, 0);
        RenderOptions.SetBitmapScalingMode(reducedColor, BitmapScalingMode.NearestNeighbor);
        reducedColor.Freeze();
        dc.DrawImage(reducedColor, new Rect(0, 0, GameEngine.Width, GameEngine.Height));
        var scanline = new SolidColorBrush(Color.FromArgb(22, 0, 4, 8));
        for (double y = 2; y < GameEngine.Height; y += 4)
            dc.DrawRectangle(scanline, null, new Rect(0, y, GameEngine.Width, 1));
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
            if (view.Game.IsBonusStage)
            {
                Vector drift = view.Game.BonusStageVariant switch
                {
                    BonusStageKind.Crossfire => new Vector(-44, Math.Sin(view.Game.BonusTravelTime * .7) * 16),
                    BonusStageKind.SlalomGates => new Vector(-72, 0),
                    BonusStageKind.SpiralSwarm => new Vector(-18, 28),
                    _ => new Vector(-34, 20)
                };
                int backgroundIndex = waveIndex % view.WaveBackgrounds.Length;
                ImageBrush movingBackdrop = view.BonusBackgroundBrushes[backgroundIndex] ??=
                    GameView.CreateBonusBackgroundBrush(selectedBackground);
                if (movingBackdrop.Transform is TranslateTransform transform)
                {
                    transform.X = view.Game.BonusTravelTime * drift.X;
                    transform.Y = view.Game.BonusTravelTime * drift.Y;
                }
                dc.DrawRectangle(movingBackdrop, null, new Rect(0, 0, GameEngine.Width, GameEngine.Height));
            }
            else
            {
                int cycle = waveIndex / view.WaveBackgrounds.Length;
                double overscan = 12 + waveIndex % 4 * 3;
                double panX = Math.Sin(view.Game.TotalTime * .018 + waveIndex * 1.73) * 7;
                double panY = Math.Cos(view.Game.TotalTime * .014 + waveIndex * .91) * 5;
                dc.PushTransform(new ScaleTransform(cycle % 2 == 1 ? -1 : 1, cycle % 4 >= 2 ? -1 : 1,
                    GameEngine.Width / 2, GameEngine.Height / 2));
                dc.DrawImage(selectedBackground,
                    new Rect(-overscan + panX, -overscan + panY, GameEngine.Width + overscan * 2, GameEngine.Height + overscan * 2));
                dc.Pop();
            }

            Color grade = GameView.WaveGrades[waveIndex % GameView.WaveGrades.Length];
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(titleScene ? (byte)36 : (byte)58, grade.R, grade.G, grade.B)), null,
                new Rect(0, 0, GameEngine.Width, GameEngine.Height));
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(titleScene ? (byte)30 : (byte)52, 0, 2, 8)), null,
                new Rect(0, 0, GameEngine.Width, GameEngine.Height));
        }
        else
        {
            var fallback = new RadialGradientBrush(Color.FromRgb(12, 25, 57), Color.FromRgb(0, 2, 9))
            { RadiusX = .82, RadiusY = .82 };
            dc.DrawRectangle(fallback, null, new Rect(0, 0, GameEngine.Width, GameEngine.Height));
        }

        dc.DrawRectangle(GameView.VignetteBrush, null, new Rect(0, 0, GameEngine.Width, GameEngine.Height));
    }

    private void DrawStars(GameView view, DrawingContext dc)
    {
        foreach (var star in view.Game.Stars)
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
            var brush = new SolidColorBrush(Color.FromArgb(alpha,
                (byte)(180 + star.Depth * 70), (byte)(205 + star.Depth * 45), 255));
            var position = new Point(x, y);
            dc.DrawEllipse(brush, null, position, size, size);
            if (star.Depth > .82 && twinkle > .78)
            {
                var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(alpha / 2), 160, 205, 255)), .7);
                dc.DrawLine(pen, new Point(x - size * 3, y), new Point(x + size * 3, y));
                dc.DrawLine(pen, new Point(x, y - size * 3), new Point(x, y + size * 3));
            }
        }
    }

    private void DrawBonusStageEnvironment(GameView view, DrawingContext dc)
    {
        if (!view.Game.IsBonusStage) return;
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
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(23, guide.R, guide.G, guide.B)), 1),
                    new Point(0, i * GameEngine.Height / 9), new Point(GameEngine.Width, i * GameEngine.Height / 9));
        }
        else if (view.Game.BonusStageVariant == BonusStageKind.SpiralSwarm)
        {
            for (int i = 0; i < 5; i++)
                dc.DrawArc(new Pen(new SolidColorBrush(Color.FromArgb((byte)(25 + i * 7), guide.R, guide.G, guide.B)), 1.2),
                    new Point(GameEngine.Width / 2, GameEngine.Height / 2), 130 + i * 105, view.Game.BonusTravelTime * (12 + i * 2) + i * 43, 215);
        }
        else
        {
            double slope = view.Game.BonusStageVariant == BonusStageKind.Crossfire ? 0 : .56;
            for (int i = -3; i < 10; i++)
            {
                double y = PositiveModulo(i * 105 + view.Game.BonusTravelTime * 95, GameEngine.Height + 210) - 105;
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(25, guide.R, guide.G, guide.B)), 1.2),
                    new Point(0, y), new Point(GameEngine.Width, y + GameEngine.Width * slope));
            }
        }
    }

    internal double PositiveModulo(double value, double modulus) => (value % modulus + modulus) % modulus;
}
