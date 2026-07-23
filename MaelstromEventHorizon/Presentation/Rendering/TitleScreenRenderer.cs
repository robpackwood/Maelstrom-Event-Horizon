using MaelstromEventHorizon.Application;
using MaelstromEventHorizon.Application.Input;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;
using MaelstromEventHorizon.Presentation.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MaelstromEventHorizon.Presentation.Rendering;

internal sealed class TitleScreenRenderer
{
    internal void DrawTitleTickers(GameView view, DrawingContext dc)
    {
        var itemBand = new Rect(0, 480, GameEngine.Width, 159);
        var objectiveBand = new Rect(0, 640, GameEngine.Width, 78);
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(228, 2, 11, 20)), null, itemBand);
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(238, 1, 7, 14)), null, objectiveBand);
        var divider = new Pen(new SolidColorBrush(Color.FromArgb(150, 72, 177, 207)), 1);
        dc.DrawLine(divider, new Point(0, 480), new Point(GameEngine.Width, 480));
        dc.DrawLine(divider, new Point(0, 640), new Point(GameEngine.Width, 640));
        dc.DrawLine(divider, new Point(150, 496), new Point(150, 624));
        dc.DrawLine(divider, new Point(640, 480), new Point(640, 640));
        dc.DrawLine(divider, new Point(820, 496), new Point(820, 624));

        view.DrawCenteredText(dc, "HAZARDS", 75, 549, 15,
            new SolidColorBrush(Color.FromRgb(255, 137, 112)), FontWeights.Bold);
        view.DrawCenteredText(dc, "THREATS + DANGERS", 75, 574, 9,
            new SolidColorBrush(Color.FromRgb(174, 111, 102)), FontWeights.SemiBold);
        view.DrawCenteredText(dc, "ITEM GUIDE", 730, 549, 15,
            new SolidColorBrush(Color.FromRgb(119, 227, 255)), FontWeights.Bold);
        view.DrawCenteredText(dc, "PICKUPS + POWERUPS", 730, 574, 9,
            new SolidColorBrush(Color.FromRgb(99, 153, 177)), FontWeights.SemiBold);

        DrawGuideTicker(view, dc, GameView.TitleHazardGuide, new Rect(151, 481, 488, 158), 57, 170);
        DrawGuideTicker(view, dc, GameView.TitleItemGuide, new Rect(821, 481, 458, 158), 62, 0);

        dc.PushClip(new RectangleGeometry(new Rect(0, 641, GameEngine.Width, 76)));
        FormattedText objectiveHeader = view.Format("MISSION OBJECTIVES", 12.5,
            new SolidColorBrush(Color.FromRgb(255, 215, 112)), FontWeights.Bold);
        FormattedText objectives = view.Format(GameView.TitleObjectives, 12.5,
            new SolidColorBrush(Color.FromRgb(207, 226, 233)), FontWeights.SemiBold);
        const double objectiveGap = 120;
        double objectiveCycleWidth = objectiveHeader.Width + 32 + objectives.Width + objectiveGap;
        double objectiveX = 24 - view.PositiveModulo(view.Game.TotalTime * 44, objectiveCycleWidth);
        while (objectiveX < GameEngine.Width)
        {
            dc.DrawText(objectiveHeader, new Point(objectiveX, 682 - objectiveHeader.Baseline));
            objectiveX += objectiveHeader.Width + 32;
            dc.DrawText(objectives, new Point(objectiveX, 682 - objectives.Baseline));
            objectiveX += objectives.Width + objectiveGap;
        }
        dc.Pop();

        FormattedText version = view.Format("1.0.0", 10,
            new SolidColorBrush(Color.FromRgb(111, 145, 160)), FontWeights.SemiBold);
        dc.DrawText(version, new Point(GameEngine.Width - version.Width - 12, 708 - version.Baseline));
    }

    private void DrawGuideTicker(GameView view, DrawingContext dc,
        (GameView.TickerIcon Icon, string Name, string Description, uint Tint)[] entries,
        Rect clip, double speed, double phase)
    {
        dc.PushClip(new RectangleGeometry(clip));
        double cycleWidth = MeasureGuideCycle(view, entries);
        double x = clip.X + 20 - view.PositiveModulo(view.Game.TotalTime * speed + phase, cycleWidth);
        while (x < clip.Right)
        {
            foreach (var entry in entries)
            {
                Color tint = view.FromArgb(entry.Tint);
                var iconCenter = new Point(x + 17, 560);
                dc.PushTransform(new ScaleTransform(1.2, 1.2, iconCenter.X, iconCenter.Y));
                DrawTickerIcon(view, dc, entry.Icon, iconCenter, tint);
                dc.Pop();
                x += 42;
                FormattedText name = view.Format(entry.Name, 14, new SolidColorBrush(tint), FontWeights.Bold);
                dc.DrawText(name, new Point(x, 565 - name.Baseline));
                x += name.Width + 10;
                FormattedText description = view.Format(entry.Description, 13,
                    new SolidColorBrush(Color.FromRgb(171, 194, 207)), FontWeights.Normal);
                dc.DrawText(description, new Point(x, 565 - description.Baseline));
                x += description.Width + 50;
            }
        }
        dc.Pop();
    }

    private void DrawTickerIcon(GameView view, DrawingContext dc, GameView.TickerIcon icon, Point center, Color tint)
    {
        BitmapImage? sprite = icon switch
        {
            GameView.TickerIcon.Canister => view.CanisterSprite,
            GameView.TickerIcon.Cash => view.DollarSprite,
            GameView.TickerIcon.Multiplier => view.MultiplierSprite,
            GameView.TickerIcon.Asteroid => view.AsteroidSprites[0],
            GameView.TickerIcon.SteelAsteroid or GameView.TickerIcon.MetalStorm => view.MetalAsteroidSprite,
            GameView.TickerIcon.Raider => view.RaiderSprite,
            GameView.TickerIcon.Interceptor => view.InterceptorSprite,
            _ => null
        };
        if (sprite is not null)
        {
            double width = icon is GameView.TickerIcon.Raider or GameView.TickerIcon.Interceptor ? 30 : 27;
            double height = icon is GameView.TickerIcon.Raider or GameView.TickerIcon.Interceptor ? 24 : 27;
            dc.DrawImage(sprite, new Rect(center.X - width / 2, center.Y - height / 2, width, height));
            return;
        }

        var brush = new SolidColorBrush(tint);
        var pale = new SolidColorBrush(view.Lighten(tint, .48));
        var pen = new Pen(pale, 1.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.PushTransform(new TranslateTransform(center.X, center.Y));
        switch (icon)
        {
            case GameView.TickerIcon.RapidFire:
                for (int y = -7; y <= 7; y += 7)
                {
                    dc.DrawLine(pen, new Point(-12, y), new Point(5, y));
                    dc.DrawEllipse(brush, null, new Point(9, y), 3.5, 2.4);
                }
                break;
            case GameView.TickerIcon.AirBrakes:
                dc.DrawLine(pen, new Point(-12, -9), new Point(-4, 0));
                dc.DrawLine(pen, new Point(-4, 0), new Point(-12, 9));
                dc.DrawLine(pen, new Point(12, -9), new Point(4, 0));
                dc.DrawLine(pen, new Point(4, 0), new Point(12, 9));
                dc.DrawRectangle(brush, null, new Rect(-2, -10, 4, 20));
                break;
            case GameView.TickerIcon.Luck:
                dc.DrawEllipse(brush, pen, new Point(-5, -5), 5.5, 5.5);
                dc.DrawEllipse(brush, pen, new Point(5, -5), 5.5, 5.5);
                dc.DrawEllipse(brush, pen, new Point(-5, 5), 5.5, 5.5);
                dc.DrawEllipse(brush, pen, new Point(5, 5), 5.5, 5.5);
                dc.DrawLine(pen, new Point(1, 7), new Point(8, 13));
                break;
            case GameView.TickerIcon.TripleFire:
                foreach (double angle in new[] { -.48, 0.0, .48 })
                {
                    Point end = new(12 * Math.Cos(angle), 12 * Math.Sin(angle));
                    dc.DrawLine(pen, new Point(-10, 0), end);
                    dc.DrawEllipse(brush, null, end, 3, 3);
                }
                break;
            case GameView.TickerIcon.LongRange:
                dc.DrawLine(pen, new Point(-13, 0), new Point(10, 0));
                dc.DrawLine(pen, new Point(5, -5), new Point(11, 0));
                dc.DrawLine(pen, new Point(5, 5), new Point(11, 0));
                dc.DrawEllipse(brush, null, new Point(-10, 0), 3, 3);
                break;
            case GameView.TickerIcon.Shield:
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(55, tint.R, tint.G, tint.B)), pen,
                    new Point(0, 0), 12, 12);
                dc.DrawArc(new Pen(pale, 2.4), new Point(0, 0), 9, -60, 150);
                dc.DrawArc(new Pen(pale, 2.4), new Point(0, 0), 9, 130, 95);
                break;
            case GameView.TickerIcon.Freeze:
                for (int i = 0; i < 3; i++)
                {
                    double angle = i * Math.PI / 3;
                    V2 axis = V2.FromAngle(angle) * 12;
                    dc.DrawLine(pen, new Point(-axis.X, -axis.Y), new Point(axis.X, axis.Y));
                }
                dc.DrawEllipse(pale, null, new Point(0, 0), 3, 3);
                break;
            case GameView.TickerIcon.SmartBomb:
                var bomb = new RadialGradientBrush(view.Lighten(tint, .6), view.Darken(tint, .52));
                dc.DrawEllipse(bomb, pen, new Point(0, 2), 10, 10);
                dc.DrawLine(pen, new Point(5, -7), new Point(10, -13));
                dc.DrawLine(new Pen(Brushes.White, 1.8), new Point(9, -13), new Point(13, -10));
                break;
            case GameView.TickerIcon.RetroVision:
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(13, 25, 38)), pen, new Rect(-13, -10, 26, 20));
                Color[] pixels =
                [
                    view.Lighten(tint, .45), tint, Color.FromRgb(255, 99, 92),
                    Color.FromRgb(92, 221, 255), Color.FromRgb(125, 246, 151), Color.FromRgb(190, 113, 255)
                ];
                for (int i = 0; i < pixels.Length; i++)
                {
                    int row = i / 3;
                    dc.DrawRectangle(new SolidColorBrush(pixels[i]), null,
                        new Rect(-9 + i % 3 * 6, -6 + row * 7, 5, 6));
                }
                dc.DrawLine(pen, new Point(-5, 11), new Point(5, 11));
                dc.DrawLine(pen, new Point(0, 10), new Point(0, 14));
                break;
            case GameView.TickerIcon.RicochetArena:
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(70, tint.R, tint.G, tint.B)),
                    new Pen(pale, 2), new Rect(-13, -11, 26, 22));
                dc.DrawLine(pen, new Point(-9, 7), new Point(8, -6));
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 238, 211)),
                    new Pen(new SolidColorBrush(tint), 1), new Point(8, -6), 5, 5);
                dc.DrawArc(new Pen(new SolidColorBrush(Color.FromRgb(255, 93, 101)), 2), new Point(8, -6), 4, -35, 75);
                dc.DrawArc(new Pen(new SolidColorBrush(Color.FromRgb(255, 220, 71)), 2), new Point(8, -6), 4, 85, 75);
                dc.DrawArc(new Pen(new SolidColorBrush(Color.FromRgb(70, 183, 255)), 2), new Point(8, -6), 4, 205, 75);
                break;
            case GameView.TickerIcon.GiantShip:
                dc.PushTransform(new ScaleTransform(.52, .52));
                dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(75, tint.R, tint.G, tint.B)),
                    new Pen(pale, 3.4), view.ShipGeometry(5));
                dc.DrawGeometry(new LinearGradientBrush(Colors.White, tint, 35),
                    new Pen(new SolidColorBrush(view.Darken(tint, .42)), 1.8), view.ShipGeometry(0));
                dc.Pop();
                dc.DrawLine(pen, new Point(-13, 12), new Point(-13, -12));
                dc.DrawLine(pen, new Point(-17, -8), new Point(-13, -13));
                dc.DrawLine(pen, new Point(-9, -8), new Point(-13, -13));
                break;
            case GameView.TickerIcon.Comet:
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(65, tint.R, tint.G, tint.B)), 5),
                    new Point(-14, 5), new Point(5, -2));
                dc.DrawLine(pen, new Point(-14, 9), new Point(6, 0));
                dc.DrawEllipse(new RadialGradientBrush(Colors.White, tint), pen, new Point(7, -1), 7, 7);
                break;
            case GameView.TickerIcon.RescueShip:
                dc.PushTransform(new ScaleTransform(.55, .55));
                dc.DrawGeometry(brush, pen, view.ShipGeometry(0));
                dc.Pop();
                break;
            case GameView.TickerIcon.Mine:
                for (int i = 0; i < 8; i++)
                {
                    V2 direction = V2.FromAngle(i * Math.PI / 4);
                    dc.DrawLine(pen, new Point(direction.X * 7, direction.Y * 7),
                        new Point(direction.X * 14, direction.Y * 14));
                }
                dc.DrawEllipse(new RadialGradientBrush(view.Lighten(tint, .5), view.Darken(tint, .5)), pen,
                    new Point(0, 0), 8, 8);
                break;
            case GameView.TickerIcon.BlackHole:
                dc.DrawEllipse(Brushes.Black, new Pen(brush, 2.6), new Point(0, 0), 7, 7);
                dc.DrawArc(pen, new Point(0, 0), 12, view.Game.TotalTime * 150, 225);
                dc.DrawArc(new Pen(brush, 1.2), new Point(0, 0), 9, -view.Game.TotalTime * 190, 150);
                break;
            case GameView.TickerIcon.Supernova:
                for (int i = 0; i < 8; i++)
                {
                    V2 direction = V2.FromAngle(i * Math.PI / 4);
                    dc.DrawLine(pen, new Point(direction.X * 4, direction.Y * 4),
                        new Point(direction.X * 14, direction.Y * 14));
                }
                dc.DrawEllipse(new RadialGradientBrush(Colors.White, tint), null, new Point(0, 0), 7, 7);
                break;
            case GameView.TickerIcon.AlienBoss:
                for (int i = 0; i < 6; i++)
                {
                    V2 direction = V2.FromAngle(i * Math.PI / 3);
                    dc.DrawLine(new Pen(new SolidColorBrush(view.Darken(tint, .25)), 3.2)
                    {
                        StartLineCap = PenLineCap.Round,
                        EndLineCap = PenLineCap.Round
                    }, new Point(direction.X * 7, direction.Y * 7),
                        new Point(direction.X * 15, direction.Y * 15));
                }
                dc.DrawEllipse(new RadialGradientBrush(view.Lighten(tint, .45), view.Darken(tint, .55)), pen,
                    new Point(), 11, 10);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(243, 235, 177)), null, new Point(1, -1), 7, 5);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(15, 8, 18)), null, new Point(2, -1), 2.5, 4.5);
                dc.DrawArc(new Pen(new SolidColorBrush(Color.FromRgb(42, 20, 24)), 1.4), new Point(0, 4), 6, 15, 150);
                break;
        }
        dc.Pop();
    }

    private static double MeasureGuideCycle(GameView view,
        (GameView.TickerIcon Icon, string Name, string Description, uint Tint)[] entries)
    {
        double width = 0;
        foreach (var entry in entries)
        {
            width += 42;
            width += view.Format(entry.Name, 14, Brushes.White, FontWeights.Bold).Width + 10;
            width += view.Format(entry.Description, 13, Brushes.White, FontWeights.Normal).Width + 50;
        }
        return width;
    }

    internal void DrawControlsMenu(GameView view, DrawingContext dc)
    {
        view.DrawCenteredText(dc, "CONTROLS", GameEngine.Width / 2, 91, 40, Brushes.White, FontWeights.Black);
        view.DrawCenteredText(dc, "KEYBOARD", GameEngine.Width / 2, 127, 14,
            new SolidColorBrush(Color.FromRgb(95, 209, 238)), FontWeights.SemiBold);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(110, 80, 166, 197)), 1), new Point(344, 151), new Point(936, 151));

        for (int i = 0; i < ControlBindings.Actions.Length; i++)
        {
            GameAction action = ControlBindings.Actions[i];
            bool selected = view.Game.ControlSelection == i;
            double baseline = 203 + i * 49;
            Brush labelBrush = new SolidColorBrush(selected ? Color.FromRgb(122, 229, 255) : Color.FromRgb(177, 199, 211));
            if (selected) view.DrawText(dc, ">", 354, baseline, 17, labelBrush, FontWeights.Bold);
            view.DrawText(dc, ControlBindings.ActionName(action), 390, baseline, 17, labelBrush, selected ? FontWeights.Bold : FontWeights.SemiBold);

            string keyName = selected && view.Game.WaitingForBinding
                ? (((int)(view.Game.TotalTime * 3) & 1) == 0 ? "PRESS A KEY" : "")
                : ControlBindings.KeyName(view.Game.Bindings[action]);
            Brush keyBrush = new SolidColorBrush(selected ? Color.FromRgb(240, 249, 252) : Color.FromRgb(120, 158, 178));
            view.DrawText(dc, keyName, 730, baseline, 17, keyBrush, FontWeights.Bold);
        }

        view.DrawCenteredText(dc, "ENTER  CHANGE       R  DEFAULTS       ESC  BACK", GameEngine.Width / 2, 642, 12,
            new SolidColorBrush(Color.FromRgb(103, 175, 199)), FontWeights.SemiBold);
    }
}
