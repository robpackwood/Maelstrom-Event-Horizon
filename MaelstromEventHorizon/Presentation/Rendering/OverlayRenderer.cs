using MaelstromEventHorizon.Application;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Scores;
using MaelstromEventHorizon.Presentation.Drawing;
using System.Windows;
using System.Windows.Media;

namespace MaelstromEventHorizon.Presentation.Rendering;

internal sealed class OverlayRenderer
{
    internal void DrawOverlay(GameView view, DrawingContext dc)
    {
        if (view.Game.Mode is GameMode.Playing or GameMode.WaveOutro or GameMode.WaveIntro or GameMode.GameOverDelay) return;
        double overlayOpacity = view.Game.Mode is GameMode.NameEntry or GameMode.GameOver
            ? view.Game.GameOverOverlayAlpha
            : 1;
        dc.PushOpacity(overlayOpacity);
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(view.Game.Mode is GameMode.Title or GameMode.Controls ? (byte)92 : (byte)145, 0, 2, 9)), null,
            new Rect(0, 0, GameEngine.Width, GameEngine.Height));

        if (view.Game.Mode == GameMode.Title)
        {
            view.DrawText(dc, "MAELSTROM", 82, 72, 58,
                new SolidColorBrush(Color.FromRgb(225, 247, 255)), FontWeights.Black);
            view.DrawText(dc, "EVENT HORIZON", 88, 111, 19,
                new SolidColorBrush(Color.FromRgb(82, 221, 255)), FontWeights.SemiBold);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(150, 84, 219, 255)), 1), new Point(88, 132), new Point(551, 132));

            string[] menuItems = ["PLAY", "CONTROLS", "MUSIC", "SOUND FX", "FULL SCREEN", "QUIT"];
            for (int i = 0; i < menuItems.Length; i++)
            {
                double baseline = 170 + i * 46;
                bool selected = view.Game.TitleMenuSelection == i;
                Brush brush = new SolidColorBrush(selected ? Color.FromRgb(117, 230, 255) : Color.FromRgb(174, 194, 207));
                if (selected) view.DrawText(dc, ">", 101, baseline, 21, brush, FontWeights.Bold);
                view.DrawText(dc, menuItems[i], 140, baseline, 21, brush, selected ? FontWeights.Bold : FontWeights.SemiBold);
                if (i is 2 or 3)
                {
                    DrawVolumeSlider(view, dc, 323, baseline - 12, 176, i == 2 ? view.Game.MusicVolume : view.Game.EffectsVolume, selected);
                }
                else if (i == 4)
                {
                    var box = new Rect(363, baseline - 18, 22, 22);
                    dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(175, 3, 15, 27)), new Pen(brush, 1.5), box);
                    if (view.Game.FullScreenEnabled)
                    {
                        var check = new Pen(new SolidColorBrush(Color.FromRgb(255, 222, 110)), 2.6)
                        {
                            StartLineCap = PenLineCap.Round,
                            EndLineCap = PenLineCap.Round
                        };
                        dc.DrawLine(check, new Point(368, baseline - 6), new Point(373, baseline - 1));
                        dc.DrawLine(check, new Point(373, baseline - 1), new Point(381, baseline - 12));
                    }
                }
            }

            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(100, 84, 155, 184)), 1), new Point(642, 38), new Point(642, 466));
            DrawTitleHighScores(view, dc);
            view.DrawTitleTickers(dc);
        }
        else if (view.Game.Mode == GameMode.Controls)
        {
            view.DrawControlsMenu(dc);
        }
        else if (view.Game.Mode == GameMode.QuitConfirm)
        {
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(235, 4, 12, 22)),
                new Pen(new SolidColorBrush(Color.FromRgb(104, 207, 235)), 1.4),
                new Rect(382, 243, 516, 224), 5, 5);
            view.DrawCenteredText(dc, "RETURN TO TITLE?", GameEngine.Width / 2, 305, 32,
                new SolidColorBrush(Color.FromRgb(230, 247, 252)), FontWeights.Black);
            view.DrawCenteredText(dc, "THE CURRENT RUN WILL END", GameEngine.Width / 2, 356, 14,
                new SolidColorBrush(Color.FromRgb(255, 180, 128)), FontWeights.SemiBold);
            view.DrawCenteredText(dc, "ENTER  CONFIRM", 524, 421, 14,
                new SolidColorBrush(Color.FromRgb(118, 239, 168)), FontWeights.Bold);
            view.DrawCenteredText(dc, "ESC  CANCEL", 756, 421, 14,
                new SolidColorBrush(Color.FromRgb(128, 213, 241)), FontWeights.Bold);
        }
        else if (view.Game.Mode == GameMode.Paused)
        {
            view.DrawCenteredText(dc, "PAUSED", GameEngine.Width / 2, 350, 46, Brushes.White, FontWeights.Bold);
        }
        else if (view.Game.Mode == GameMode.WaveSummary || view.Game.Mode == GameMode.WaveSummaryExit)
        {
            DrawCashConfetti(view, dc);
            DrawWaveSummary(view, dc);
        }
        else if (view.Game.Mode == GameMode.NameEntry)
        {
            DrawGameOverScene(view, dc, true);
            view.DrawCenteredText(dc, "GAME OVER", GameEngine.Width / 2, 105, 50,
                new SolidColorBrush(Color.FromRgb(255, 105, 97)), FontWeights.Black);
            view.DrawCenteredText(dc, "TOP 10 SCORE", GameEngine.Width / 2, 190, 38,
                new SolidColorBrush(Color.FromRgb(109, 224, 255)), FontWeights.Black);
            view.DrawCenteredText(dc, $"RANK {view.Game.PendingHighScoreRank:00}  /  {view.Money(view.Game.Score)}  /  WAVE {view.Game.Wave}",
                GameEngine.Width / 2, 236, 18, Brushes.White, FontWeights.Bold);
            view.DrawCenteredText(dc, "ENTER PILOT NAME", GameEngine.Width / 2, 342, 15,
                new SolidColorBrush(Color.FromRgb(151, 187, 207)), FontWeights.SemiBold);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(210, 4, 13, 24)),
                new Pen(new SolidColorBrush(Color.FromRgb(66, 190, 228)), 1.4), new Rect(446, 363, 388, 62), 4, 4);
            string cursor = ((int)(view.Game.TotalTime * 2) & 1) == 0 ? "_" : " ";
            view.DrawCenteredText(dc, view.Game.PendingName + cursor, GameEngine.Width / 2, 404, 25, Brushes.White, FontWeights.Bold);
            view.DrawCenteredText(dc, "ENTER TO SAVE", GameEngine.Width / 2, 469, 13,
                new SolidColorBrush(Color.FromRgb(122, 215, 239)), FontWeights.SemiBold);
        }
        else
        {
            DrawGameOverScene(view, dc, false);
            view.DrawCenteredText(dc, "GAME OVER", GameEngine.Width / 2, 92, 48,
                new SolidColorBrush(Color.FromRgb(255, 105, 97)), FontWeights.Black);
            view.DrawCenteredText(dc, $"FINAL SCORE  {view.Money(view.Game.Score)}     WAVE {view.Game.Wave}", GameEngine.Width / 2, 139, 18, Brushes.White, FontWeights.Bold);
            view.DrawCenteredText(dc, "TOP 10 PILOTS", GameEngine.Width / 2, 190, 20,
                new SolidColorBrush(Color.FromRgb(143, 194, 222)), FontWeights.SemiBold);
            view.DrawHighScores(dc);
            view.DrawCenteredText(dc, "PRESS ENTER FOR TITLE", GameEngine.Width / 2, 626, 15, Brushes.White, FontWeights.Bold);
        }
        dc.Pop();
    }

    private void DrawVolumeSlider(GameView view, DrawingContext dc, double x, double y, double width, double value, bool selected)
    {
        value = Math.Clamp(value, 0, 1);
        Color accent = selected ? Color.FromRgb(117, 230, 255) : Color.FromRgb(100, 160, 183);
        var track = new Rect(x, y + 4, width, 7);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(205, 3, 14, 25)),
            new Pen(new SolidColorBrush(Color.FromArgb(145, 111, 161, 181)), 1), track, 3, 3);
        if (value > 0)
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(230, accent.R, accent.G, accent.B)), null,
                new Rect(x, y + 4, width * value, 7), 3, 3);
        for (int i = 0; i <= 4; i++)
        {
            double tickX = x + width * i / 4;
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(125, 183, 220, 231)), .7),
                new Point(tickX, y + 2), new Point(tickX, y + 13));
        }
        double knobX = x + width * value;
        dc.DrawEllipse(new SolidColorBrush(selected ? Color.FromRgb(255, 225, 121) : Color.FromRgb(203, 226, 231)),
            new Pen(new SolidColorBrush(Color.FromRgb(16, 47, 60)), 1.2), new Point(knobX, y + 7.5), 6, 6);
        view.DrawText(dc, $"{Math.Round(value * 100):0}%", x + width + 15, y + 13, 12,
            new SolidColorBrush(selected ? Color.FromRgb(255, 225, 121) : Color.FromRgb(164, 194, 205)), FontWeights.SemiBold);
    }

    private void DrawGameOverScene(GameView view, DrawingContext dc, bool highScore)
    {
        double t = view.Game.TotalTime;
        double pulse = .5 + .5 * Math.Sin(t * 3.2);
        var vignette = new RadialGradientBrush
        {
            GradientOrigin = new Point(.5, .5),
            Center = new Point(.5, .5),
            RadiusX = .76,
            RadiusY = .82
        };
        vignette.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), .28));
        vignette.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(105 + pulse * 42), 0, 7, 14), 1));
        dc.DrawRectangle(vignette, null, new Rect(0, 0, GameEngine.Width, GameEngine.Height));

        for (int y = 35; y < GameEngine.Height; y += 14)
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(10, 124, 213, 234)), .6),
                new Point(30, y), new Point(GameEngine.Width - 30, y));

        Point center = new(GameEngine.Width / 2, highScore ? 340 : 382);
        for (int i = 0; i < 3; i++)
        {
            double radius = 145 + i * 76 + Math.Sin(t * 1.4 + i) * 9;
            byte alpha = (byte)(18 + i * 7);
            var orbitPen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, 89, 212, 242)), 1);
            dc.DrawArc(orbitPen, center, radius, t * (10 + i * 3) + i * 72, 112 + i * 18);
            dc.DrawArc(orbitPen, center, radius, 180 + t * (7 + i * 2), 58 + i * 15);
        }

        for (int i = 0; i < 22; i++)
        {
            double angle = i * Math.PI * 2 / 22 + t * (.025 + view.Hash(193, i) * .035);
            double radiusX = 445 + view.Hash(251, i) * 155;
            double radiusY = 230 + view.Hash(367, i) * 92;
            double x = center.X + Math.Cos(angle) * radiusX;
            double y = center.Y + Math.Sin(angle) * radiusY;
            double size = 3 + view.Hash(419, i) * 8;
            dc.PushTransform(new TranslateTransform(x, y));
            dc.PushTransform(new RotateTransform(t * (35 + view.Hash(487, i) * 145) + i * 31));
            Color metal = i % 4 == 0 ? Color.FromRgb(255, 100, 76) : Color.FromRgb(111, 151, 168);
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(170, metal.R, metal.G, metal.B)),
                new Pen(new SolidColorBrush(Color.FromArgb(140, 222, 241, 245)), .7),
                view.Polygon((-size, -size * .35), (size * .7, -size * .55), (size, size * .2), (-size * .4, size * .65)));
            dc.Pop();
            dc.Pop();
        }

        for (int i = 0; i < 16; i++)
        {
            double phase = (t * (75 + view.Hash(557, i) * 95) + i * 47) % 360 * Math.PI / 180;
            double radius = 300 + view.Hash(613, i) * 285;
            Point spark = new(center.X + Math.Cos(phase) * radius, center.Y + Math.Sin(phase) * radius * .48);
            double length = 4 + view.Hash(677, i) * 13;
            var sparkPen = new Pen(new SolidColorBrush(Color.FromArgb(155, 255, 167, 77)), 1.2);
            dc.DrawLine(sparkPen, spark, new Point(spark.X - Math.Cos(phase) * length, spark.Y - Math.Sin(phase) * length));
        }
    }




    private void DrawWaveSummary(GameView view, DrawingContext dc)
    {
        string title = view.Game.BonusStageFailed
            ? "BONUS STAGE FAILED"
            : view.Game.IsBonusStage ? "BONUS STAGE COMPLETE" : view.Game.IsBossStage
                ? view.Game.BossOnlyMode ? "BOSS ONLY VICTORY" : "ALIEN BOSS DEFEATED"
                : $"WAVE {view.Game.Wave} COMPLETE";
        Brush titleBrush = view.Game.BonusStageFailed
            ? new SolidColorBrush(Color.FromRgb(255, 119, 105))
            : new SolidColorBrush(Color.FromRgb(210, 244, 255));
        view.DrawCenteredText(dc, title, GameEngine.Width / 2, 153, 38, titleBrush, FontWeights.Black);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(135, 77, 192, 220)), 1), new Point(390, 181), new Point(890, 181));

        if (view.Game.BonusStageFailed)
            view.DrawCenteredText(dc, "ASTEROID COLLISION  /  NO BONUS AWARDED", GameEngine.Width / 2, 202, 11,
                new SolidColorBrush(Color.FromRgb(255, 166, 146)), FontWeights.Bold);

        var label = new SolidColorBrush(Color.FromRgb(139, 177, 197));
        var value = new SolidColorBrush(Color.FromRgb(235, 247, 251));
        view.DrawText(dc, view.Game.IsBonusStage ? "DODGE EARNINGS" : "WAVE EARNINGS", 420, 222, 15, label, FontWeights.SemiBold);
        view.DrawText(dc, view.Money(view.Game.SummaryBaseCash), 733, 222, 18, value, FontWeights.Bold);
        view.DrawText(dc, "LEVEL BONUS", 420, 261, 15, label, FontWeights.SemiBold);
        view.DrawText(dc, view.Money(view.Game.SummaryLevelBonusCash), 733, 261, 18,
            new SolidColorBrush(Color.FromRgb(255, 221, 113)), FontWeights.Bold);
        view.DrawText(dc, "COMET CASH", 420, 300, 15, label, FontWeights.SemiBold);
        view.DrawText(dc, view.Money(view.Game.SummaryCometCash), 733, 300, 18, value, FontWeights.Bold);
        view.DrawText(dc, "MULTIPLIER", 420, 339, 15, label, FontWeights.SemiBold);
        view.DrawText(dc, $"x {view.Game.SummaryMultiplier}", 733, 339, 18,
            new SolidColorBrush(Color.FromRgb(217, 159, 255)), FontWeights.Bold);
        view.DrawText(dc, "COMET TOTAL", 420, 378, 15, label, FontWeights.SemiBold);
        view.DrawText(dc, view.Money(view.Game.SummaryCometCash * view.Game.SummaryMultiplier), 733, 378, 18,
            new SolidColorBrush(Color.FromRgb(123, 229, 255)), FontWeights.Bold);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(135, 77, 192, 220)), 1), new Point(410, 405), new Point(870, 405));
        view.DrawText(dc, "WAVE DEPOSIT", 420, 448, 17, Brushes.White, FontWeights.Bold);
        view.DrawText(dc, view.Money(view.Game.SummaryDeposited), 733, 448, 23,
            new SolidColorBrush(Color.FromRgb(114, 255, 157)), FontWeights.Black);
        view.DrawCenteredText(dc, $"BANK TOTAL  {view.Money(view.Game.Score)}", GameEngine.Width / 2, 516, 19,
            new SolidColorBrush(Color.FromRgb(255, 224, 124)), FontWeights.Bold);

        string prompt = view.Game.SummaryInputReady
            ? "PRESS ANY KEY"
            : view.Game.SummaryComplete ? "STANDBY" : "COUNTING WAVE CASH";
        view.DrawCenteredText(dc, prompt, GameEngine.Width / 2, 608, 14,
            new SolidColorBrush(Color.FromRgb(139, 206, 226)), FontWeights.SemiBold);
    }

    internal void DrawTransitionCurtain(GameView view, DrawingContext dc)
    {
        if (view.Game.TransitionAlpha <= 0) return;
        byte alpha = (byte)(255 * Math.Clamp(view.Game.TransitionAlpha, 0, 1));
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(alpha, 0, 1, 5)), null,
            new Rect(0, 0, GameEngine.Width, GameEngine.Height));
    }

    private void DrawCashConfetti(GameView view, DrawingContext dc)
    {
        if (view.Game.CashConfettiTime <= 0) return;
        double elapsed = view.Game.TotalTime;
        for (int i = 0; i < 46; i++)
        {
            double speed = 125 + view.Hash(701, i) * 210;
            double x = (view.Hash(177, i) * GameEngine.Width + Math.Sin(elapsed * (1.4 + view.Hash(911, i)) + i) * 42 + GameEngine.Width) % GameEngine.Width;
            double y = (view.Hash(337, i) * 690 + elapsed * speed) % 790 - 45;
            double angle = elapsed * (110 + view.Hash(513, i) * 280) + i * 31;
            Color billColor = i % 3 == 0 ? Color.FromRgb(104, 232, 143) : Color.FromRgb(66, 178, 112);
            dc.PushTransform(new TranslateTransform(x, y));
            dc.PushTransform(new RotateTransform(angle));
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(220, billColor.R, billColor.G, billColor.B)),
                new Pen(new SolidColorBrush(Color.FromRgb(194, 255, 210)), .7), new Rect(-10, -5, 20, 10));
            if (i % 3 == 0) view.DrawCenteredText(dc, "$", 0, 3, 7,
                new SolidColorBrush(Color.FromRgb(12, 75, 42)), FontWeights.Black);
            dc.Pop();
            dc.Pop();
        }
    }

    private void DrawTitleHighScores(GameView view, DrawingContext dc)
    {
        view.DrawText(dc, "TOP 10 PILOTS", 704, 61, 21, new SolidColorBrush(Color.FromRgb(145, 220, 241)), FontWeights.Bold);
        var dim = new SolidColorBrush(Color.FromRgb(106, 140, 159));
        view.DrawText(dc, "#", 704, 96, 11, dim, FontWeights.SemiBold);
        view.DrawText(dc, "NAME", 758, 96, 11, dim, FontWeights.SemiBold);
        view.DrawText(dc, "SCORE", 1015, 96, 11, dim, FontWeights.SemiBold);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(95, 85, 160, 190)), 1), new Point(700, 108), new Point(1194, 108));

        for (int i = 0; i < 10; i++)
        {
            double baseline = 139 + i * 32;
            bool highlighted = i == view.Game.HighlightedHighScoreIndex;
            if (highlighted)
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(72, 255, 211, 83)),
                    new Pen(new SolidColorBrush(Color.FromArgb(145, 255, 225, 119)), 1),
                    new Rect(695, baseline - 21, 505, 27), 3, 3);
            Color color = highlighted ? Color.FromRgb(255, 225, 112) : Color.FromRgb(174, 203, 216);
            Brush brush = new SolidColorBrush(color);
            FontWeight weight = highlighted ? FontWeights.Bold : FontWeights.SemiBold;
            view.DrawText(dc, $"{i + 1:00}", 704, baseline, 14, brush, weight);
            if (i < view.Game.HighScores.Count)
            {
                HighScoreEntry entry = view.Game.HighScores[i];
                view.DrawText(dc, entry.Name, 758, baseline, 14, brush, weight);
                view.DrawText(dc, view.Money(entry.Score), 1015, baseline, 14, brush, weight);
            }
            else
            {
                view.DrawText(dc, "---", 758, baseline, 14, new SolidColorBrush(Color.FromRgb(65, 85, 99)), FontWeights.Normal);
            }
        }
    }
}
