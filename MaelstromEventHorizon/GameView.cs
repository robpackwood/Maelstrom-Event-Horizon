using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MaelstromEventHorizon;

internal sealed class GameView : FrameworkElement
{
    private enum TickerIcon
    {
        Canister, RapidFire, AirBrakes, Luck, TripleFire, LongRange, Shield, Freeze, SmartBomb,
        Cash, Multiplier, Comet, RescueShip, Asteroid, SteelAsteroid, Raider, Interceptor, Mine,
        BlackHole, Supernova, MetalStorm
    }

    private readonly GameEngine game;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly BitmapImage? background;
    private readonly BitmapImage?[] asteroidSprites;
    private readonly BitmapImage? metalAsteroidSprite;
    private readonly BitmapImage? raiderSprite;
    private readonly BitmapImage? interceptorSprite;
    private readonly BitmapImage? canisterSprite;
    private readonly BitmapImage? dollarSprite;
    private readonly BitmapImage? multiplierSprite;
    private double previousTime;
    private static readonly (TickerIcon Icon, string Name, string Description, uint Tint)[] TitleItemGuide =
    [
        (TickerIcon.Canister, "ITEM CANISTER", "contains one random ship upgrade", 0xff50eaff),
        (TickerIcon.RapidFire, "RAPID FIRE", "hold fire for 15-round bursts", 0xffff6e72),
        (TickerIcon.AirBrakes, "AIR BRAKES", "adds strong momentum control", 0xff73d8ff),
        (TickerIcon.Luck, "LUCK OF THE IRISH", "triggers every special event once this wave", 0xff72f09a),
        (TickerIcon.TripleFire, "TRIPLE FIRE", "launches a three-way shot", 0xffffb85d),
        (TickerIcon.LongRange, "LONG RANGE", "keeps shots active farther", 0xff8fd5ff),
        (TickerIcon.Shield, "SHIELD ENERGY", "restores shield charge", 0xff64edff),
        (TickerIcon.Freeze, "TIME FREEZE", "stops hostile motion for eight seconds", 0xffb18cff),
        (TickerIcon.SmartBomb, "SMART BOMB", "fractures asteroids and clears nearby threats", 0xffff7b68),
        (TickerIcon.Cash, "CASH BONUS", "adds dollars to the current wave", 0xffffd66b),
        (TickerIcon.Multiplier, "MULTIPLIER", "multiplies banked comet cash", 0xffd08aff),
        (TickerIcon.Comet, "COMET", "shoot any part for $500 to $5,000", 0xff73e7ff),
        (TickerIcon.RescueShip, "RESCUE SHIP", "touch it to gain one extra life", 0xff79f3b2)
    ];
    private static readonly (TickerIcon Icon, string Name, string Description, uint Tint)[] TitleHazardGuide =
    [
        (TickerIcon.Asteroid, "ASTEROID", "fractures into smaller rocks when shot", 0xffd5c5aa),
        (TickerIcon.SteelAsteroid, "STEEL ASTEROID", "resists gunfire and hits hard", 0xffd4edf5),
        (TickerIcon.Raider, "RAIDER", "pursues the ship and fires scattered shots", 0xffff6984),
        (TickerIcon.Interceptor, "INTERCEPTOR", "moves faster and leads its fire", 0xff69e8ff),
        (TickerIcon.Mine, "HOMING MINE", "tracks the ship and detonates on contact", 0xffffdf58),
        (TickerIcon.BlackHole, "BLACK HOLE", "pulls the ship inward; contact is fatal", 0xffc187ff),
        (TickerIcon.Supernova, "SUPERNOVA", "shoot it before it detonates", 0xffff8c50),
        (TickerIcon.MetalStorm, "METAL STORM", "crosses the arena once; dodge every rock", 0xffb9e8f7)
    ];
    private const string TitleObjectives =
        "BREAK ASTEROIDS  /  SURVIVE EACH WAVE  /  DESTROY ENEMY SHIPS  /  SHOOT COMETS FOR CASH  /  " +
        "COLLECT UPGRADES  /  BANK WAVE EARNINGS  /  DODGE THE METAL STORM EVERY 5 WAVES  /  " +
        "EARN AN EXTRA SHIP EVERY $50,000  /  CHASE THE HIGHEST SCORE";

    public GameView(bool fullScreenEnabled = false, Action<bool>? fullScreenChanged = null)
    {
        game = new GameEngine(fullScreenEnabled, fullScreenChanged);
        Focusable = true;
        SnapsToDevicePixels = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        asteroidSprites =
        [
            LoadSprite("asteroid-basalt.png"),
            LoadSprite("asteroid-fractured.png"),
            LoadSprite("asteroid-iron.png")
        ];
        metalAsteroidSprite = LoadSprite("asteroid-metal.png");
        raiderSprite = LoadSprite("enemy-raider.png");
        interceptorSprite = LoadSprite("enemy-interceptor.png");
        canisterSprite = LoadSprite("item-canister.png");
        dollarSprite = LoadSprite("bonus-dollar.png");
        multiplierSprite = LoadSprite("bonus-multiplier.png");
        try
        {
            string path = GameAssets.PathFor("deep-space.png");
            background = new BitmapImage(new Uri(path, UriKind.Absolute));
            background.Freeze();
        }
        catch { }

        Loaded += (_, _) =>
        {
            Focus();
            Keyboard.Focus(this);
            CompositionTarget.Rendering += RenderFrame;
        };
        Unloaded += (_, _) => CompositionTarget.Rendering -= RenderFrame;
        MouseDown += (_, _) => Focus();
        TextInput += (_, e) =>
        {
            if (game.Mode != GameMode.NameEntry) return;
            game.HandleTextInput(e.Text);
            e.Handled = true;
        };
        KeyDown += (_, e) =>
        {
            if (game.Mode == GameMode.NameEntry)
            {
                if (game.HandleNameEntryKey(e.Key)) e.Handled = true;
                return;
            }
            if (game.HandleCommandKey(e.Key, e.IsRepeat)) e.Handled = true;
        };
    }

    private static BitmapImage? LoadSprite(string filename)
    {
        try
        {
            string path = GameAssets.PathFor("Sprites", filename);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private void RenderFrame(object? sender, EventArgs e)
    {
        double now = clock.Elapsed.TotalSeconds;
        double dt = previousTime == 0 ? 1.0 / 60 : now - previousTime;
        previousTime = now;
        game.Update(dt);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (ActualWidth < 1 || ActualHeight < 1) return;

        double scale = Math.Min(ActualWidth / GameEngine.Width, ActualHeight / GameEngine.Height);
        double x = (ActualWidth - GameEngine.Width * scale) / 2;
        double y = (ActualHeight - GameEngine.Height * scale) / 2;
        dc.PushClip(new RectangleGeometry(new Rect(x, y, GameEngine.Width * scale, GameEngine.Height * scale)));
        dc.PushTransform(new TranslateTransform(x, y));
        dc.PushTransform(new ScaleTransform(scale, scale));
        DrawBackdrop(dc);
        V2 shake = game.ScreenShakeOffset;
        dc.PushTransform(new TranslateTransform(shake.X, shake.Y));
        DrawStars(dc);
        DrawVortices(dc);
        DrawNovas(dc);
        DrawComets(dc);
        DrawPickups(dc);
        DrawAsteroids(dc);
        DrawFighters(dc);
        DrawMines(dc);
        DrawShots(dc);
        DrawShip(dc);
        DrawShipDebris(dc);
        DrawParticles(dc);
        DrawShockwaves(dc);
        DrawFloatingTexts(dc);
        DrawHud(dc);
        DrawOverlay(dc);
        dc.Pop();
        DrawTransitionCurtain(dc);

        dc.Pop();
        dc.Pop();
        dc.Pop();
    }

    private void DrawBackdrop(DrawingContext dc)
    {
        if (background is not null)
        {
            if (game.IsBonusStage)
            {
                var movingBackdrop = new ImageBrush(background)
                {
                    Stretch = Stretch.Fill,
                    TileMode = TileMode.FlipXY,
                    ViewportUnits = BrushMappingMode.Absolute,
                    Viewport = new Rect(0, 0, GameEngine.Width, GameEngine.Height),
                    ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
                    Viewbox = new Rect(0, 0, 1, 1),
                    Transform = new TranslateTransform(-game.BonusTravelTime * 34, game.BonusTravelTime * 20)
                };
                dc.DrawRectangle(movingBackdrop, null, new Rect(0, 0, GameEngine.Width, GameEngine.Height));
            }
            else dc.DrawImage(background, new Rect(0, 0, GameEngine.Width, GameEngine.Height));
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(48, 0, 3, 12)), null,
                new Rect(0, 0, GameEngine.Width, GameEngine.Height));
        }
        else
        {
            var fallback = new RadialGradientBrush(Color.FromRgb(12, 25, 57), Color.FromRgb(0, 2, 9))
            { RadiusX = .82, RadiusY = .82 };
            dc.DrawRectangle(fallback, null, new Rect(0, 0, GameEngine.Width, GameEngine.Height));
        }

        var vignette = new RadialGradientBrush
        {
            GradientOrigin = new Point(.5, .48), Center = new Point(.5, .48), RadiusX = .72, RadiusY = .78
        };
        vignette.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), .35));
        vignette.GradientStops.Add(new GradientStop(Color.FromArgb(155, 0, 0, 5), 1));
        dc.DrawRectangle(vignette, null, new Rect(0, 0, GameEngine.Width, GameEngine.Height));
    }

    private void DrawStars(DrawingContext dc)
    {
        foreach (var star in game.Stars)
        {
            double depthSpeed = .45 + star.Depth * .72;
            double x = star.Position.X;
            double y = star.Position.Y;
            if (game.IsBonusStage)
            {
                x = PositiveModulo(x - game.BonusTravelTime * 92 * depthSpeed, GameEngine.Width);
                y = PositiveModulo(y + game.BonusTravelTime * 56 * depthSpeed, GameEngine.Height);
            }
            double twinkle = .48 + .52 * Math.Sin(game.TotalTime * (1.2 + star.Depth * 2.5) + star.Phase);
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

    private static double PositiveModulo(double value, double modulus) => (value % modulus + modulus) % modulus;

    private void DrawShip(DrawingContext dc)
    {
        Ship ship = game.Player;
        if (game.PlayerRespawning) return;
        if (game.Mode is GameMode.Title or GameMode.Controls or GameMode.Paused) return;
        if (game.Lives <= 0 && game.Mode is GameMode.GameOverDelay or GameMode.NameEntry or GameMode.GameOver) return;
        if (game.Mode == GameMode.Playing && ship.Invulnerable > 0 && ship.SpawnShieldTime <= 0 &&
            ((int)(game.TotalTime * 12) & 1) == 0) return;

        dc.PushTransform(new TranslateTransform(ship.Position.X, ship.Position.Y));
        dc.PushTransform(new RotateTransform(ship.Angle * 180 / Math.PI));
        dc.PushTransform(new ScaleTransform(Ship.VisualScale, Ship.VisualScale));

        if (ship.Thrusting)
        {
            var plume = new StreamGeometry();
            using (var c = plume.Open())
            {
                c.BeginFigure(new Point(-14, -7), true, true);
                c.LineTo(new Point(-36 - 8 * Math.Sin(game.TotalTime * 38), 0), true, false);
                c.LineTo(new Point(-14, 7), true, false);
            }
            var plumeBrush = new LinearGradientBrush(Color.FromArgb(245, 245, 255, 255), Color.FromArgb(30, 45, 115, 255), new Point(1, .5), new Point(0, .5));
            dc.DrawGeometry(plumeBrush, new Pen(new SolidColorBrush(Color.FromArgb(120, 80, 220, 255)), 2), plume);
        }

        var hull = new LinearGradientBrush();
        hull.StartPoint = new Point(.2, 0);
        hull.EndPoint = new Point(.8, 1);
        hull.GradientStops.Add(new GradientStop(Color.FromRgb(244, 246, 239), 0));
        hull.GradientStops.Add(new GradientStop(Color.FromRgb(126, 143, 148), .38));
        hull.GradientStops.Add(new GradientStop(Color.FromRgb(40, 53, 62), .72));
        hull.GradientStops.Add(new GradientStop(Color.FromRgb(177, 192, 190), 1));
        dc.DrawGeometry(hull, new Pen(new SolidColorBrush(Color.FromRgb(28, 36, 43)), 1.8), ShipGeometry(0));
        dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(78, 96, 102)), new Pen(new SolidColorBrush(Color.FromRgb(202, 215, 211)), .8),
            Polygon((-13, -10), (-3, -6), (4, 0), (-8, -2)));
        dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(52, 66, 73)), new Pen(new SolidColorBrush(Color.FromRgb(160, 178, 178)), .8),
            Polygon((-13, 10), (-3, 6), (4, 0), (-8, 2)));
        var canopy = new RadialGradientBrush(Color.FromRgb(112, 187, 202), Color.FromRgb(7, 24, 32));
        dc.DrawGeometry(canopy, new Pen(new SolidColorBrush(Color.FromRgb(188, 222, 222)), .8), Polygon((-4, -6), (10, 0), (-4, 6), (1, 0)));
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(27, 31, 34)), new Pen(new SolidColorBrush(Color.FromRgb(145, 152, 149)), .7), new Rect(-16, -13, 8, 5));
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(27, 31, 34)), new Pen(new SolidColorBrush(Color.FromRgb(145, 152, 149)), .7), new Rect(-16, 8, 8, 5));
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(210, 58, 45)), new Pen(new SolidColorBrush(Color.FromRgb(255, 169, 115)), .6), new Point(9, 0), 2.1, 2.1);
        dc.Pop();
        dc.Pop();
        dc.Pop();

        if (ship.Shielding || ship.SpawnShieldTime > 0)
        {
            double pulse = 2 + Math.Sin(game.TotalTime * 12) * 2;
            bool spawnShield = ship.SpawnShieldTime > 0;
            Color shieldColor = spawnShield ? Color.FromRgb(105, 255, 191) : Color.FromRgb(61, 225, 255);
            DrawGlowEllipse(dc, ship.Position, 29 * Ship.VisualScale + pulse, shieldColor, 4, spawnShield ? .72 : .55);
            var shieldPen = new Pen(new SolidColorBrush(Color.FromArgb(220, shieldColor.R, shieldColor.G, shieldColor.B)), 1.9);
            dc.DrawEllipse(null, shieldPen, Pt(ship.Position), 29 * Ship.VisualScale + pulse, 29 * Ship.VisualScale + pulse);
            dc.DrawArc(shieldPen, Pt(ship.Position), 34 * Ship.VisualScale + pulse, game.TotalTime * 95, 122);
        }

        if (game.ShieldImpactTime > 0)
        {
            double intensity = Math.Clamp(game.ShieldImpactTime / .42, 0, 1);
            double expansion = (1 - intensity) * 18;
            Color impactColor = Color.FromRgb(202, 255, 255);
            DrawGlowEllipse(dc, ship.Position, 35 * Ship.VisualScale + expansion, impactColor, 5, .92 * intensity);
            var ringPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(245 * intensity), 181, 255, 255)),
                1.5 + intensity * 2.2)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            double radius = 37 * Ship.VisualScale + expansion;
            dc.DrawArc(ringPen, Pt(ship.Position), radius, game.TotalTime * 180, 146);
            dc.DrawArc(ringPen, Pt(ship.Position), radius, game.TotalTime * 180 + 188, 98);

            double contactRadius = 7 + expansion * .55;
            DrawGlowEllipse(dc, game.ShieldImpactPoint, contactRadius, Color.FromRgb(236, 255, 255), 4, intensity);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(210 * intensity), 228, 255, 255)),
                null, Pt(game.ShieldImpactPoint), 2.5 + intensity * 2.5, 2.5 + intensity * 2.5);
        }
    }

    private void DrawShipDebris(DrawingContext dc)
    {
        foreach (var piece in game.ShipDebrisPieces)
        {
            double life = Math.Clamp(1 - piece.Age / piece.Lifetime, 0, 1);
            byte alpha = (byte)(255 * Math.Min(1, life * 1.8));
            dc.PushTransform(new TranslateTransform(piece.Position.X, piece.Position.Y));
            dc.PushTransform(new RotateTransform(piece.Angle * 180 / Math.PI));
            dc.PushTransform(new ScaleTransform(Ship.VisualScale, Ship.VisualScale));

            Geometry shape = ShipDebrisGeometry(piece.Kind);
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

    private void DrawAsteroids(DrawingContext dc)
    {
        foreach (var rock in game.Asteroids)
        {
            dc.PushTransform(new TranslateTransform(rock.Position.X, rock.Position.Y));
            dc.PushTransform(new RotateTransform(rock.Angle * 180 / Math.PI));
            Geometry shape = AsteroidGeometry(rock);
            if (rock.Steel)
            {
                if (metalAsteroidSprite is not null)
                {
                    double span = rock.Radius * 2.3;
                    dc.DrawImage(metalAsteroidSprite, new Rect(-span / 2, -span / 2, span, span));
                }
                else
                {
                    var metal = new RadialGradientBrush
                    {
                        GradientOrigin = new Point(.28, .22), Center = new Point(.35, .3), RadiusX = .72, RadiusY = .72
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
                BitmapImage? sprite = asteroidSprites[(rock.Seed & int.MaxValue) % asteroidSprites.Length];
                if (sprite is not null)
                {
                    double span = rock.Radius * 2.2;
                    dc.DrawImage(sprite, new Rect(-span / 2, -span / 2, span, span));
                }
                else
                {
                    var stone = new RadialGradientBrush
                    {
                        GradientOrigin = new Point(.28, .22), Center = new Point(.32, .28), RadiusX = .8, RadiusY = .8
                    };
                    stone.GradientStops.Add(new GradientStop(Color.FromRgb(178, 145, 121), 0));
                    stone.GradientStops.Add(new GradientStop(Color.FromRgb(73, 56, 57), .48));
                    stone.GradientStops.Add(new GradientStop(Color.FromRgb(22, 21, 31), 1));
                    dc.DrawGeometry(stone, new Pen(new SolidColorBrush(Color.FromRgb(230, 178, 127)), 1.25), shape);
                    dc.PushClip(shape);
                    DrawRockTexture(dc, rock);
                    dc.Pop();
                    DrawCrater(dc, rock.Radius * -.2, rock.Radius * -.18, rock.Radius * .18);
                    DrawCrater(dc, rock.Radius * .26, rock.Radius * .18, rock.Radius * .11);
                    DrawCrater(dc, rock.Radius * -.12, rock.Radius * .37, rock.Radius * .08);
                }
            }
            dc.Pop();
            dc.Pop();
        }
    }

    private static void DrawCrater(DrawingContext dc, double x, double y, double r)
    {
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(150, 18, 15, 22)), new Pen(new SolidColorBrush(Color.FromArgb(150, 198, 151, 116)), 1), new Point(x, y), r, r * .65);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(55, 255, 214, 174)), null, new Point(x - r * .25, y - r * .22), r * .34, r * .18);
    }

    private static void DrawRockTexture(DrawingContext dc, Asteroid rock)
    {
        int marks = rock.Size == 3 ? 30 : rock.Size == 2 ? 19 : 11;
        for (int i = 0; i < marks; i++)
        {
            double a = Hash(rock.Seed + 417, i) * Math.PI * 2;
            double distance = Math.Sqrt(Hash(rock.Seed - 971, i)) * rock.Radius * .82;
            double size = (.018 + Hash(rock.Seed + 73, i) * .065) * rock.Radius;
            Point p = new(Math.Cos(a) * distance, Math.Sin(a) * distance);
            bool light = Hash(rock.Seed + 11, i) > .58;
            Color color = light ? Color.FromArgb(75, 226, 187, 143) : Color.FromArgb(105, 29, 24, 30);
            dc.DrawEllipse(new SolidColorBrush(color), null, p, size * 1.4, size);
        }

        var vein = new Pen(new SolidColorBrush(Color.FromArgb(100, 35, 27, 31)), Math.Max(.7, rock.Radius * .022));
        for (int i = 0; i < Math.Max(2, rock.Size + 1); i++)
        {
            double a = Hash(rock.Seed + 800, i) * Math.PI * 2;
            Point p0 = new(Math.Cos(a) * rock.Radius * .15, Math.Sin(a) * rock.Radius * .15);
            Point p1 = new(Math.Cos(a + .34) * rock.Radius * .72, Math.Sin(a + .34) * rock.Radius * .72);
            dc.DrawLine(vein, p0, p1);
        }
    }

    private void DrawFighters(DrawingContext dc)
    {
        foreach (var fighter in game.Fighters)
        {
            bool little = fighter.Kind == FighterKind.Interceptor;
            Color glow = little ? Color.FromRgb(63, 228, 255) : Color.FromRgb(255, 57, 112);
            DrawGlowEllipse(dc, fighter.Position, fighter.Radius * .78, glow, 3, .2);
            dc.PushTransform(new TranslateTransform(fighter.Position.X, fighter.Position.Y));
            dc.PushTransform(new RotateTransform(fighter.Angle * 180 / Math.PI));
            BitmapImage? sprite = little ? interceptorSprite : raiderSprite;
            if (sprite is not null)
            {
                double span = little ? 52 : 72;
                dc.DrawImage(sprite, new Rect(-span / 2, -span / 2, span, span));
            }
            else
            {
                Geometry wings = little
                    ? Polygon((22, 0), (1, -12), (-18, -15), (-10, 0), (-18, 15), (1, 12))
                    : Polygon((27, 0), (6, -13), (-25, -21), (-16, -2), (-27, 0), (-16, 2), (-25, 21), (6, 13));
                DrawGlowGeometry(dc, wings, glow, 8);
                var body = new LinearGradientBrush(Lighten(glow, .75), Darken(glow, .72), new Point(1, 0), new Point(0, 1));
                dc.DrawGeometry(body, new Pen(new SolidColorBrush(Lighten(glow, .4)), 1.5), wings);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(5, 12, 24)), new Pen(new SolidColorBrush(glow), 1), new Point(7, 0), 7, 5);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 246, 170)), null, new Point(13, 0), 2, 2);
            }
            dc.Pop();
            dc.Pop();
        }
    }

    private void DrawMines(DrawingContext dc)
    {
        foreach (var mine in game.Mines)
        {
            DrawGlowEllipse(dc, mine.Position, 15, Color.FromRgb(255, 205, 63), 6, .65);
            dc.PushTransform(new TranslateTransform(mine.Position.X, mine.Position.Y));
            dc.PushTransform(new RotateTransform(mine.Angle * 180 / Math.PI));
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(255, 149, 52)), 3),
                    new Point(Math.Cos(a) * 8, Math.Sin(a) * 8), new Point(Math.Cos(a) * 20, Math.Sin(a) * 20));
            }
            var core = new RadialGradientBrush(Color.FromRgb(255, 250, 178), Color.FromRgb(112, 22, 18));
            dc.DrawEllipse(core, new Pen(new SolidColorBrush(Color.FromRgb(255, 217, 76)), 1.5), new Point(), 11, 11);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 55, 45)), null, new Point(), 3 + Math.Sin(game.TotalTime * 14), 3 + Math.Sin(game.TotalTime * 14));
            dc.Pop();
            dc.Pop();
        }
    }

    private void DrawVortices(DrawingContext dc)
    {
        foreach (var vortex in game.Vortices)
        {
            double pulse = Math.Sin(game.TotalTime * 4 + vortex.Position.X) * 3;
            for (int i = 6; i >= 0; i--)
            {
                double r = 26 + i * 10 + pulse;
                byte a = (byte)(22 + (6 - i) * 17);
                var pen = new Pen(new SolidColorBrush(Color.FromArgb(a, (byte)(98 + i * 13), 76, 255)), 3.5 - i * .25);
                dc.DrawArc(pen, Pt(vortex.Position), r, vortex.Angle * 180 / Math.PI + i * 39, 235);
            }
            var disk = new RadialGradientBrush();
            disk.GradientStops.Add(new GradientStop(Color.FromRgb(0, 0, 0), .25));
            disk.GradientStops.Add(new GradientStop(Color.FromRgb(12, 3, 24), .62));
            disk.GradientStops.Add(new GradientStop(Color.FromArgb(0, 115, 70, 255), 1));
            dc.DrawEllipse(disk, new Pen(new SolidColorBrush(Color.FromArgb(180, 145, 96, 255)), 2), Pt(vortex.Position), 36, 36);
            dc.DrawEllipse(Brushes.Black, new Pen(new SolidColorBrush(Color.FromRgb(206, 164, 255)), 1), Pt(vortex.Position), 13, 13);
        }
    }

    private void DrawNovas(DrawingContext dc)
    {
        foreach (var nova in game.Novas)
        {
            double progress = nova.Age / Nova.Fuse;
            double pulse = 1 + Math.Sin(nova.Age * (5 + progress * 22)) * (.08 + progress * .13);
            double radius = (20 + progress * 30) * pulse;
            DrawGlowEllipse(dc, nova.Position, radius, Color.FromRgb(255, 176, 61), 10, .8);
            var star = new RadialGradientBrush();
            star.GradientStops.Add(new GradientStop(Colors.White, 0));
            star.GradientStops.Add(new GradientStop(Color.FromRgb(255, 245, 145), .22));
            star.GradientStops.Add(new GradientStop(Color.FromRgb(255, 72, 35), .63));
            star.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 32, 20), 1));
            dc.DrawEllipse(star, null, Pt(nova.Position), radius, radius);
            for (int i = 0; i < 6; i++)
            {
                double a = i * Math.PI / 3 + nova.Angle;
                var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(80 + progress * 120), 255, 218, 90)), 1.4);
                dc.DrawLine(pen, Pt(nova.Position + V2.FromAngle(a) * radius * .55), Pt(nova.Position + V2.FromAngle(a) * radius * 1.8));
            }
        }
    }

    private void DrawPickups(DrawingContext dc)
    {
        foreach (var pickup in game.Pickups)
        {
            double pulse = 1 + Math.Sin(game.TotalTime * 6 + pickup.Position.X) * .08;
            Color color = pickup.Kind switch
            {
                PickupKind.Canister => Color.FromRgb(80, 234, 255),
                PickupKind.Multiplier => Color.FromRgb(194, 101, 255),
                PickupKind.Bonus => Color.FromRgb(255, 213, 75),
                _ => Color.FromRgb(91, 255, 148)
            };
            DrawGlowEllipse(dc, pickup.Position, pickup.Radius * pulse, color, 7, .55);
            dc.PushTransform(new TranslateTransform(pickup.Position.X, pickup.Position.Y));
            dc.PushTransform(new RotateTransform(pickup.Angle * 180 / Math.PI));
            if (pickup.Kind == PickupKind.Canister)
            {
                if (canisterSprite is not null)
                    dc.DrawImage(canisterSprite, new Rect(-21, -21, 42, 42));
                else
                {
                    var shell = new LinearGradientBrush(Color.FromRgb(223, 252, 255), Darken(color, .68), 45);
                    dc.DrawRoundedRectangle(shell, new Pen(new SolidColorBrush(color), 1.5), new Rect(-11, -16, 22, 32), 5, 5);
                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(8, 31, 47)), null, new Rect(-8, -7, 16, 14));
                    dc.DrawLine(new Pen(new SolidColorBrush(color), 2), new Point(-5, 0), new Point(5, 0));
                    dc.DrawLine(new Pen(new SolidColorBrush(color), 2), new Point(0, -5), new Point(0, 5));
                }
            }
            else if (pickup.Kind == PickupKind.RescueShip)
            {
                dc.PushTransform(new ScaleTransform(.8, .8));
                var rescueHull = new LinearGradientBrush();
                rescueHull.StartPoint = new Point(.2, 0);
                rescueHull.EndPoint = new Point(.8, 1);
                rescueHull.GradientStops.Add(new GradientStop(Color.FromRgb(244, 246, 239), 0));
                rescueHull.GradientStops.Add(new GradientStop(Color.FromRgb(126, 143, 148), .38));
                rescueHull.GradientStops.Add(new GradientStop(Color.FromRgb(40, 53, 62), .72));
                rescueHull.GradientStops.Add(new GradientStop(Color.FromRgb(177, 192, 190), 1));
                dc.DrawGeometry(rescueHull, new Pen(new SolidColorBrush(Color.FromRgb(28, 36, 43)), 1.8), ShipGeometry(0));
                dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(78, 96, 102)), new Pen(new SolidColorBrush(Color.FromRgb(202, 215, 211)), .8),
                    Polygon((-13, -10), (-3, -6), (4, 0), (-8, -2)));
                dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(52, 66, 73)), new Pen(new SolidColorBrush(Color.FromRgb(160, 178, 178)), .8),
                    Polygon((-13, 10), (-3, 6), (4, 0), (-8, 2)));
                var rescueCanopy = new RadialGradientBrush(Color.FromRgb(112, 187, 202), Color.FromRgb(7, 24, 32));
                dc.DrawGeometry(rescueCanopy, new Pen(new SolidColorBrush(Color.FromRgb(188, 222, 222)), .8),
                    Polygon((-4, -6), (10, 0), (-4, 6), (1, 0)));
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(27, 31, 34)), new Pen(new SolidColorBrush(Color.FromRgb(145, 152, 149)), .7), new Rect(-16, -13, 8, 5));
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(27, 31, 34)), new Pen(new SolidColorBrush(Color.FromRgb(145, 152, 149)), .7), new Rect(-16, 8, 8, 5));
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(74, 255, 145)), new Pen(new SolidColorBrush(Color.FromRgb(205, 255, 219)), .7), new Point(9, 0), 2.4, 2.4);
                dc.Pop();
            }
            else
            {
                BitmapImage? sprite = pickup.Kind == PickupKind.Multiplier ? multiplierSprite : dollarSprite;
                if (sprite is not null)
                    dc.DrawImage(sprite, new Rect(-22, -22, 44, 44));
                else
                {
                    Geometry badge = RegularPolygon(6, 15, -Math.PI / 6);
                    dc.DrawGeometry(new SolidColorBrush(Darken(color, .6)), new Pen(new SolidColorBrush(Lighten(color, .5)), 2), badge);
                }

                string value = pickup.Kind == PickupKind.Multiplier ? $"{pickup.Value}x" : $"${pickup.Value / 1000}K";
                DrawCenteredText(dc, value, 1, 7.5, 11, new SolidColorBrush(Color.FromArgb(210, 0, 0, 0)), FontWeights.Black);
                DrawCenteredText(dc, value, 0, 6.5, 11, Brushes.White, FontWeights.Black);
            }
            dc.Pop();
            dc.Pop();
        }
    }

    private void DrawComets(DrawingContext dc)
    {
        foreach (var comet in game.Comets)
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    V2 position = comet.Position + new V2(x * GameEngine.Width, y * GameEngine.Height);
                    if (position.X < -Comet.TrailLength - 30 || position.X > GameEngine.Width + Comet.TrailLength + 30 ||
                        position.Y < -Comet.TrailLength - 30 || position.Y > GameEngine.Height + Comet.TrailLength + 30) continue;
                    DrawComet(dc, comet, position);
                }
            }
        }
    }

    private static void DrawComet(DrawingContext dc, Comet comet, V2 position)
    {
        Color color = FromArgb(comet.Tint);
        V2 back = -comet.Velocity.Normalized;
        V2 side = new(-back.Y, back.X);
        Point head = Pt(position);
        var outerTail = new StreamGeometry();
        using (var c = outerTail.Open())
        {
            c.BeginFigure(Pt(position + side * 14), true, true);
            c.LineTo(Pt(position + back * Comet.TrailLength), true, false);
            c.LineTo(Pt(position - side * 14), true, false);
        }
        var outerBrush = new LinearGradientBrush(Color.FromArgb(0, color.R, color.G, color.B), Color.FromArgb(205, color.R, color.G, color.B),
            Pt(position + back * Comet.TrailLength), head) { MappingMode = BrushMappingMode.Absolute };
        dc.DrawGeometry(outerBrush, null, outerTail);

        var innerTail = new StreamGeometry();
        using (var c = innerTail.Open())
        {
            c.BeginFigure(Pt(position + side * 5), true, true);
            c.LineTo(Pt(position + back * (Comet.TrailLength * .78)), true, false);
            c.LineTo(Pt(position - side * 5), true, false);
        }
        var innerBrush = new LinearGradientBrush(Color.FromArgb(0, 255, 255, 255), Color.FromArgb(225, 255, 255, 255),
            Pt(position + back * (Comet.TrailLength * .78)), head) { MappingMode = BrushMappingMode.Absolute };
        dc.DrawGeometry(innerBrush, null, innerTail);

        DrawGlowEllipse(dc, position, 18, color, 4, .48);
        var core = new RadialGradientBrush();
        core.GradientStops.Add(new GradientStop(Colors.White, 0));
        core.GradientStops.Add(new GradientStop(Lighten(color, .42), .32));
        core.GradientStops.Add(new GradientStop(Darken(color, .48), 1));
        dc.DrawEllipse(core, new Pen(new SolidColorBrush(Color.FromArgb(210, 255, 255, 255)), 1), head, 16, 16);
        DrawCenteredText(dc, comet.Value >= 1000 ? $"${comet.Value / 1000.0:0.#}K" : $"${comet.Value}",
            position.X, position.Y + 4, 8.5, new SolidColorBrush(Color.FromRgb(30, 22, 28)), FontWeights.Black);
    }

    private void DrawFloatingTexts(DrawingContext dc)
    {
        foreach (var text in game.FloatingTexts)
        {
            double life = Math.Clamp(1 - text.Age / text.Lifetime, 0, 1);
            double y = text.Position.Y - text.Age * 34;
            byte alpha = (byte)(255 * Math.Min(1, life * 2.4));
            Color color = FromArgb(text.Color, alpha);
            DrawCenteredText(dc, text.Text, text.Position.X + 1.5, y + 1.5, 18,
                new SolidColorBrush(Color.FromArgb((byte)(alpha * .7), 2, 7, 14)), FontWeights.Black);
            DrawCenteredText(dc, text.Text, text.Position.X, y, 18, new SolidColorBrush(color), FontWeights.Black);
        }
    }

    private void DrawShots(DrawingContext dc)
    {
        foreach (var shot in game.Shots)
        {
            double radius = shot.Enemy ? 5.2 : 4.3;
            Color color = shot.Enemy ? Color.FromRgb(83, 119, 255) : Color.FromRgb(47, 201, 255);
            var glow = new RadialGradientBrush();
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(105, color.R, color.G, color.B), 0));
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(35, color.R, color.G, color.B), .48));
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));
            dc.DrawEllipse(glow, null, Pt(shot.Position), radius * 2.15, radius * 2.15);

            var sphere = new RadialGradientBrush
            {
                GradientOrigin = new Point(.32, .27), Center = new Point(.4, .36), RadiusX = .7, RadiusY = .7
            };
            sphere.GradientStops.Add(new GradientStop(Colors.White, 0));
            sphere.GradientStops.Add(new GradientStop(Color.FromRgb(151, 226, 255), .24));
            sphere.GradientStops.Add(new GradientStop(color, .6));
            sphere.GradientStops.Add(new GradientStop(Color.FromRgb(9, 31, 103), 1));
            dc.DrawEllipse(sphere, new Pen(new SolidColorBrush(Color.FromArgb(210, 174, 230, 255)), .65),
                Pt(shot.Position), radius, radius);
        }
    }

    private void DrawParticles(DrawingContext dc)
    {
        foreach (var p in game.Particles)
        {
            double life = Math.Clamp(1 - p.Age / p.Lifetime, 0, 1);
            Color c = FromArgb(p.Color, (byte)(255 * life));
            double r = p.StartSize * (.35 + life * .8);
            dc.DrawEllipse(new SolidColorBrush(c), null, Pt(p.Position), r, r);
            if (r > 3) dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(45 * life), c.R, c.G, c.B)), null, Pt(p.Position), r * 2.8, r * 2.8);
        }
    }

    private void DrawShockwaves(DrawingContext dc)
    {
        foreach (var ring in game.Shockwaves)
        {
            double p = ring.Age / ring.Lifetime;
            double r = ring.MaxRadius * EaseOut(p);
            Color color = FromArgb(ring.Color, (byte)(220 * (1 - p)));
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(color), Math.Max(.7, 5 * (1 - p))), Pt(ring.Position), r, r);
        }
    }

    private void DrawHud(DrawingContext dc)
    {
        if (game.Mode is GameMode.Title or GameMode.Controls) return;
        var dim = new SolidColorBrush(Color.FromRgb(137, 168, 191));
        DrawText(dc, "SCORE", 30, 29, 12, dim, FontWeights.SemiBold);
        DrawText(dc, Money(game.Score), 88, 31, 20, Brushes.White, FontWeights.Bold);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(90, 92, 149, 177)), 1), new Point(251, 12), new Point(251, 39));
        DrawText(dc, "LEVEL", 274, 29, 12, dim, FontWeights.SemiBold);
        Brush levelBrush = new SolidColorBrush(game.LevelBonusCash > 1_000
            ? Color.FromRgb(255, 221, 113)
            : Color.FromRgb(142, 169, 181));
        DrawText(dc, Money(game.LevelBonusCash), 332, 31, 20, levelBrush, FontWeights.Bold);
        string stageLabel = game.IsBonusStage
            ? $"BONUS STAGE {game.Wave:00}  -  DODGE METAL ASTEROIDS  {game.BonusAsteroidsDodged:00}/{game.BonusAsteroidTotal:00}"
            : $"WAVE {game.Wave:00}";
        DrawCenteredText(dc, stageLabel, GameEngine.Width / 2, 29, game.IsBonusStage ? 15 : 18,
            new SolidColorBrush(game.IsBonusStage ? Color.FromRgb(255, 221, 113) : Color.FromRgb(191, 224, 241)), FontWeights.Bold);
        DrawText(dc, $"SHIPS  {game.Lives}", 1138, 30, 17, Brushes.White, FontWeights.Bold);

        DrawText(dc, "SHIELD", 30, 682, 12, dim, FontWeights.SemiBold);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(125, 5, 17, 29)), new Pen(new SolidColorBrush(Color.FromRgb(55, 91, 113)), 1), new Rect(91, 683, 180, 12), 3, 3);
        double shieldWidth = Math.Max(0, 176 * game.Player.Shield / 100);
        var shieldBrush = new LinearGradientBrush(Color.FromRgb(54, 215, 255), Color.FromRgb(90, 124, 255), 0);
        dc.DrawRoundedRectangle(shieldBrush, null, new Rect(93, 685, shieldWidth, 8), 2, 2);

        if (game.Multiplier > 1)
        {
            DrawText(dc, $"{game.Multiplier}X", 1194, 674, 26, new SolidColorBrush(Color.FromRgb(220, 155, 255)), FontWeights.Bold);
        }

        if (game.IsDemoMode)
        {
            double pulse = .78 + (Math.Sin(game.TotalTime * 3.2) + 1) * .11;
            byte alpha = (byte)(255 * pulse);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(120, 2, 13, 23)),
                new Pen(new SolidColorBrush(Color.FromArgb(150, 87, 217, 247)), 1), new Rect(438, 48, 404, 31));
            DrawCenteredText(dc, "PRESS ANY KEY TO RETURN TO TITLE", GameEngine.Width / 2, 69, 13,
                new SolidColorBrush(Color.FromArgb(alpha, 187, 239, 252)), FontWeights.Bold);
        }

        var active = new List<string>();
        if (game.RapidFireActive) active.Add("RAPID FIRE");
        if (game.AirBrakesActive) active.Add("AIR BRAKES");
        if (game.LuckActive) active.Add("LUCK");
        if (game.TripleFireActive) active.Add("TRIPLE FIRE");
        if (game.LongRangeActive) active.Add("LONG RANGE");
        if (game.FreezeTime > 0) active.Add("TIME FREEZE");
        if (active.Count > 0)
            DrawCenteredText(dc, string.Join("   |   ", active), GameEngine.Width / 2, 694, 12,
                new SolidColorBrush(Color.FromRgb(121, 232, 255)), FontWeights.SemiBold);

        if (game.BannerTime > 0 && game.Mode == GameMode.Playing)
        {
            double alpha = Math.Min(1, game.BannerTime * 2);
            var b = new SolidColorBrush(Color.FromArgb((byte)(230 * alpha), 230, 246, 255));
            DrawCenteredText(dc, game.Banner, GameEngine.Width / 2, 105, 24, b, FontWeights.Bold);
        }
    }

    private void DrawOverlay(DrawingContext dc)
    {
        if (game.Mode is GameMode.Playing or GameMode.WaveOutro or GameMode.WaveIntro or GameMode.GameOverDelay) return;
        double overlayOpacity = game.Mode is GameMode.NameEntry or GameMode.GameOver
            ? game.GameOverOverlayAlpha
            : 1;
        dc.PushOpacity(overlayOpacity);
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(game.Mode is GameMode.Title or GameMode.Controls ? (byte)92 : (byte)145, 0, 2, 9)), null,
            new Rect(0, 0, GameEngine.Width, GameEngine.Height));

        if (game.Mode == GameMode.Title)
        {
            DrawText(dc, "MAELSTROM", 82, 72, 58,
                new SolidColorBrush(Color.FromRgb(225, 247, 255)), FontWeights.Black);
            DrawText(dc, "EVENT HORIZON", 88, 111, 19,
                new SolidColorBrush(Color.FromRgb(82, 221, 255)), FontWeights.SemiBold);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(150, 84, 219, 255)), 1), new Point(88, 132), new Point(551, 132));

            string[] menuItems = ["PLAY", "CONTROLS", "FULL SCREEN", "QUIT"];
            for (int i = 0; i < menuItems.Length; i++)
            {
                double baseline = 188 + i * 48;
                bool selected = game.TitleMenuSelection == i;
                Brush brush = new SolidColorBrush(selected ? Color.FromRgb(117, 230, 255) : Color.FromRgb(174, 194, 207));
                if (selected) DrawText(dc, ">", 101, baseline, 21, brush, FontWeights.Bold);
                DrawText(dc, menuItems[i], 140, baseline, 21, brush, selected ? FontWeights.Bold : FontWeights.SemiBold);
                if (i == 2)
                {
                    var box = new Rect(363, baseline - 18, 22, 22);
                    dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(175, 3, 15, 27)), new Pen(brush, 1.5), box);
                    if (game.FullScreenEnabled)
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
            DrawTitleHighScores(dc);
            DrawTitleTickers(dc);
        }
        else if (game.Mode == GameMode.Controls)
        {
            DrawControlsMenu(dc);
        }
        else if (game.Mode == GameMode.QuitConfirm)
        {
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(235, 4, 12, 22)),
                new Pen(new SolidColorBrush(Color.FromRgb(104, 207, 235)), 1.4),
                new Rect(382, 243, 516, 224), 5, 5);
            DrawCenteredText(dc, "RETURN TO TITLE?", GameEngine.Width / 2, 305, 32,
                new SolidColorBrush(Color.FromRgb(230, 247, 252)), FontWeights.Black);
            DrawCenteredText(dc, "THE CURRENT RUN WILL END", GameEngine.Width / 2, 356, 14,
                new SolidColorBrush(Color.FromRgb(255, 180, 128)), FontWeights.SemiBold);
            DrawCenteredText(dc, "ENTER  CONFIRM", 524, 421, 14,
                new SolidColorBrush(Color.FromRgb(118, 239, 168)), FontWeights.Bold);
            DrawCenteredText(dc, "ESC  CANCEL", 756, 421, 14,
                new SolidColorBrush(Color.FromRgb(128, 213, 241)), FontWeights.Bold);
        }
        else if (game.Mode == GameMode.Paused)
        {
            DrawCenteredText(dc, "PAUSED", GameEngine.Width / 2, 350, 46, Brushes.White, FontWeights.Bold);
        }
        else if (game.Mode is GameMode.WaveSummary or GameMode.WaveSummaryExit)
        {
            DrawCashConfetti(dc);
            DrawWaveSummary(dc);
        }
        else if (game.Mode == GameMode.NameEntry)
        {
            DrawGameOverScene(dc, true);
            DrawCenteredText(dc, "GAME OVER", GameEngine.Width / 2, 105, 50,
                new SolidColorBrush(Color.FromRgb(255, 105, 97)), FontWeights.Black);
            DrawCenteredText(dc, "TOP 10 SCORE", GameEngine.Width / 2, 190, 38,
                new SolidColorBrush(Color.FromRgb(109, 224, 255)), FontWeights.Black);
            DrawCenteredText(dc, $"RANK {game.PendingHighScoreRank:00}  /  {Money(game.Score)}  /  WAVE {game.Wave}",
                GameEngine.Width / 2, 236, 18, Brushes.White, FontWeights.Bold);
            DrawCenteredText(dc, "ENTER PILOT NAME", GameEngine.Width / 2, 342, 15,
                new SolidColorBrush(Color.FromRgb(151, 187, 207)), FontWeights.SemiBold);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(210, 4, 13, 24)),
                new Pen(new SolidColorBrush(Color.FromRgb(66, 190, 228)), 1.4), new Rect(446, 363, 388, 62), 4, 4);
            string cursor = ((int)(game.TotalTime * 2) & 1) == 0 ? "_" : " ";
            DrawCenteredText(dc, game.PendingName + cursor, GameEngine.Width / 2, 404, 25, Brushes.White, FontWeights.Bold);
            DrawCenteredText(dc, "ENTER TO SAVE", GameEngine.Width / 2, 469, 13,
                new SolidColorBrush(Color.FromRgb(122, 215, 239)), FontWeights.SemiBold);
        }
        else
        {
            DrawGameOverScene(dc, false);
            DrawCenteredText(dc, "GAME OVER", GameEngine.Width / 2, 92, 48,
                new SolidColorBrush(Color.FromRgb(255, 105, 97)), FontWeights.Black);
            DrawCenteredText(dc, $"FINAL SCORE  {Money(game.Score)}     WAVE {game.Wave}", GameEngine.Width / 2, 139, 18, Brushes.White, FontWeights.Bold);
            DrawCenteredText(dc, "TOP 10 PILOTS", GameEngine.Width / 2, 190, 20,
                new SolidColorBrush(Color.FromRgb(143, 194, 222)), FontWeights.SemiBold);
            DrawHighScores(dc);
            DrawCenteredText(dc, "PRESS ENTER", GameEngine.Width / 2, 626, 15, Brushes.White, FontWeights.Bold);
        }
        dc.Pop();
    }

    private void DrawGameOverScene(DrawingContext dc, bool highScore)
    {
        double t = game.TotalTime;
        double pulse = .5 + .5 * Math.Sin(t * 3.2);
        var vignette = new RadialGradientBrush
        {
            GradientOrigin = new Point(.5, .5), Center = new Point(.5, .5), RadiusX = .76, RadiusY = .82
        };
        vignette.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), .28));
        vignette.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(105 + pulse * 42), 96, 4, 12), 1));
        dc.DrawRectangle(vignette, null, new Rect(0, 0, GameEngine.Width, GameEngine.Height));

        byte frameAlpha = (byte)(105 + pulse * 80);
        var framePen = new Pen(new SolidColorBrush(Color.FromArgb(frameAlpha, 255, 67, 72)), 1.4);
        dc.DrawRoundedRectangle(null, framePen, new Rect(24, 22, GameEngine.Width - 48, GameEngine.Height - 44), 5, 5);
        dc.DrawLine(framePen, new Point(24, 48), new Point(184, 48));
        dc.DrawLine(framePen, new Point(GameEngine.Width - 184, 48), new Point(GameEngine.Width - 24, 48));
        dc.DrawLine(framePen, new Point(24, GameEngine.Height - 48), new Point(184, GameEngine.Height - 48));
        dc.DrawLine(framePen, new Point(GameEngine.Width - 184, GameEngine.Height - 48), new Point(GameEngine.Width - 24, GameEngine.Height - 48));

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
            double angle = i * Math.PI * 2 / 22 + t * (.025 + Hash(193, i) * .035);
            double radiusX = 445 + Hash(251, i) * 155;
            double radiusY = 230 + Hash(367, i) * 92;
            double x = center.X + Math.Cos(angle) * radiusX;
            double y = center.Y + Math.Sin(angle) * radiusY;
            double size = 3 + Hash(419, i) * 8;
            dc.PushTransform(new TranslateTransform(x, y));
            dc.PushTransform(new RotateTransform(t * (35 + Hash(487, i) * 145) + i * 31));
            Color metal = i % 4 == 0 ? Color.FromRgb(255, 100, 76) : Color.FromRgb(111, 151, 168);
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(170, metal.R, metal.G, metal.B)),
                new Pen(new SolidColorBrush(Color.FromArgb(140, 222, 241, 245)), .7),
                Polygon((-size, -size * .35), (size * .7, -size * .55), (size, size * .2), (-size * .4, size * .65)));
            dc.Pop();
            dc.Pop();
        }

        for (int i = 0; i < 16; i++)
        {
            double phase = (t * (75 + Hash(557, i) * 95) + i * 47) % 360 * Math.PI / 180;
            double radius = 300 + Hash(613, i) * 285;
            Point spark = new(center.X + Math.Cos(phase) * radius, center.Y + Math.Sin(phase) * radius * .48);
            double length = 4 + Hash(677, i) * 13;
            var sparkPen = new Pen(new SolidColorBrush(Color.FromArgb(155, 255, 167, 77)), 1.2);
            dc.DrawLine(sparkPen, spark, new Point(spark.X - Math.Cos(phase) * length, spark.Y - Math.Sin(phase) * length));
        }
    }

    private void DrawWaveSummary(DrawingContext dc)
    {
        string title = game.BonusStageFailed
            ? "BONUS STAGE FAILED"
            : game.IsBonusStage ? "BONUS STAGE COMPLETE" : $"WAVE {game.Wave} COMPLETE";
        Brush titleBrush = game.BonusStageFailed
            ? new SolidColorBrush(Color.FromRgb(255, 119, 105))
            : new SolidColorBrush(Color.FromRgb(210, 244, 255));
        DrawCenteredText(dc, title, GameEngine.Width / 2, 153, 38, titleBrush, FontWeights.Black);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(135, 77, 192, 220)), 1), new Point(390, 181), new Point(890, 181));

        if (game.BonusStageFailed)
            DrawCenteredText(dc, "ASTEROID COLLISION  /  NO BONUS AWARDED", GameEngine.Width / 2, 202, 11,
                new SolidColorBrush(Color.FromRgb(255, 166, 146)), FontWeights.Bold);

        var label = new SolidColorBrush(Color.FromRgb(139, 177, 197));
        var value = new SolidColorBrush(Color.FromRgb(235, 247, 251));
        DrawText(dc, game.IsBonusStage ? "DODGE EARNINGS" : "WAVE EARNINGS", 420, 222, 15, label, FontWeights.SemiBold);
        DrawText(dc, Money(game.SummaryBaseCash), 733, 222, 18, value, FontWeights.Bold);
        DrawText(dc, "LEVEL BONUS", 420, 261, 15, label, FontWeights.SemiBold);
        DrawText(dc, Money(game.SummaryLevelBonusCash), 733, 261, 18,
            new SolidColorBrush(Color.FromRgb(255, 221, 113)), FontWeights.Bold);
        DrawText(dc, "COMET CASH", 420, 300, 15, label, FontWeights.SemiBold);
        DrawText(dc, Money(game.SummaryCometCash), 733, 300, 18, value, FontWeights.Bold);
        DrawText(dc, "MULTIPLIER", 420, 339, 15, label, FontWeights.SemiBold);
        DrawText(dc, $"x {game.SummaryMultiplier}", 733, 339, 18,
            new SolidColorBrush(Color.FromRgb(217, 159, 255)), FontWeights.Bold);
        DrawText(dc, "COMET TOTAL", 420, 378, 15, label, FontWeights.SemiBold);
        DrawText(dc, Money(game.SummaryCometCash * game.SummaryMultiplier), 733, 378, 18,
            new SolidColorBrush(Color.FromRgb(123, 229, 255)), FontWeights.Bold);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(135, 77, 192, 220)), 1), new Point(410, 405), new Point(870, 405));
        DrawText(dc, "WAVE DEPOSIT", 420, 448, 17, Brushes.White, FontWeights.Bold);
        DrawText(dc, Money(game.SummaryDeposited), 733, 448, 23,
            new SolidColorBrush(Color.FromRgb(114, 255, 157)), FontWeights.Black);
        DrawCenteredText(dc, $"BANK TOTAL  {Money(game.Score)}", GameEngine.Width / 2, 516, 19,
            new SolidColorBrush(Color.FromRgb(255, 224, 124)), FontWeights.Bold);

        string prompt = game.SummaryInputReady
            ? "PRESS ANY KEY"
            : game.SummaryComplete ? "STANDBY" : "COUNTING WAVE CASH";
        DrawCenteredText(dc, prompt, GameEngine.Width / 2, 608, 14,
            new SolidColorBrush(Color.FromRgb(139, 206, 226)), FontWeights.SemiBold);
    }

    private void DrawTransitionCurtain(DrawingContext dc)
    {
        if (game.TransitionAlpha <= 0) return;
        byte alpha = (byte)(255 * Math.Clamp(game.TransitionAlpha, 0, 1));
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(alpha, 0, 1, 5)), null,
            new Rect(0, 0, GameEngine.Width, GameEngine.Height));
    }

    private void DrawCashConfetti(DrawingContext dc)
    {
        if (game.CashConfettiTime <= 0) return;
        double elapsed = game.TotalTime;
        for (int i = 0; i < 46; i++)
        {
            double speed = 125 + Hash(701, i) * 210;
            double x = (Hash(177, i) * GameEngine.Width + Math.Sin(elapsed * (1.4 + Hash(911, i)) + i) * 42 + GameEngine.Width) % GameEngine.Width;
            double y = (Hash(337, i) * 690 + elapsed * speed) % 790 - 45;
            double angle = elapsed * (110 + Hash(513, i) * 280) + i * 31;
            Color billColor = i % 3 == 0 ? Color.FromRgb(104, 232, 143) : Color.FromRgb(66, 178, 112);
            dc.PushTransform(new TranslateTransform(x, y));
            dc.PushTransform(new RotateTransform(angle));
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(220, billColor.R, billColor.G, billColor.B)),
                new Pen(new SolidColorBrush(Color.FromRgb(194, 255, 210)), .7), new Rect(-10, -5, 20, 10));
            if (i % 3 == 0) DrawCenteredText(dc, "$", 0, 3, 7,
                new SolidColorBrush(Color.FromRgb(12, 75, 42)), FontWeights.Black);
            dc.Pop();
            dc.Pop();
        }
    }

    private void DrawTitleHighScores(DrawingContext dc)
    {
        DrawText(dc, "TOP 10 PILOTS", 704, 61, 21, new SolidColorBrush(Color.FromRgb(145, 220, 241)), FontWeights.Bold);
        var dim = new SolidColorBrush(Color.FromRgb(106, 140, 159));
        DrawText(dc, "#", 704, 96, 11, dim, FontWeights.SemiBold);
        DrawText(dc, "NAME", 758, 96, 11, dim, FontWeights.SemiBold);
        DrawText(dc, "SCORE", 1015, 96, 11, dim, FontWeights.SemiBold);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(95, 85, 160, 190)), 1), new Point(700, 108), new Point(1194, 108));

        for (int i = 0; i < 10; i++)
        {
            double baseline = 139 + i * 32;
            Color color = i == 0 ? Color.FromRgb(255, 220, 112) : i < 3 ? Color.FromRgb(201, 226, 236) : Color.FromRgb(150, 178, 194);
            Brush brush = new SolidColorBrush(color);
            DrawText(dc, $"{i + 1:00}", 704, baseline, 14, brush, FontWeights.Bold);
            if (i < game.HighScores.Count)
            {
                HighScoreEntry entry = game.HighScores[i];
                DrawText(dc, entry.Name, 758, baseline, 14, brush, FontWeights.SemiBold);
                DrawText(dc, Money(entry.Score), 1015, baseline, 14, brush, FontWeights.Bold);
            }
            else
            {
                DrawText(dc, "---", 758, baseline, 14, new SolidColorBrush(Color.FromRgb(65, 85, 99)), FontWeights.Normal);
            }
        }
    }

    private void DrawTitleTickers(DrawingContext dc)
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

        DrawCenteredText(dc, "HAZARDS", 75, 549, 15,
            new SolidColorBrush(Color.FromRgb(255, 137, 112)), FontWeights.Bold);
        DrawCenteredText(dc, "THREATS + DANGERS", 75, 574, 9,
            new SolidColorBrush(Color.FromRgb(174, 111, 102)), FontWeights.SemiBold);
        DrawCenteredText(dc, "ITEM GUIDE", 730, 549, 15,
            new SolidColorBrush(Color.FromRgb(119, 227, 255)), FontWeights.Bold);
        DrawCenteredText(dc, "PICKUPS + POWERUPS", 730, 574, 9,
            new SolidColorBrush(Color.FromRgb(99, 153, 177)), FontWeights.SemiBold);

        DrawGuideTicker(dc, TitleHazardGuide, new Rect(151, 481, 488, 158), 57, 170);
        DrawGuideTicker(dc, TitleItemGuide, new Rect(821, 481, 458, 158), 62, 0);

        dc.PushClip(new RectangleGeometry(new Rect(0, 641, GameEngine.Width, 76)));
        FormattedText objectiveHeader = Format("MISSION OBJECTIVES", 12.5,
            new SolidColorBrush(Color.FromRgb(255, 215, 112)), FontWeights.Bold);
        FormattedText objectives = Format(TitleObjectives, 12.5,
            new SolidColorBrush(Color.FromRgb(207, 226, 233)), FontWeights.SemiBold);
        const double objectiveGap = 120;
        double objectiveCycleWidth = objectiveHeader.Width + 32 + objectives.Width + objectiveGap;
        double objectiveX = 24 - PositiveModulo(game.TotalTime * 44, objectiveCycleWidth);
        while (objectiveX < GameEngine.Width)
        {
            dc.DrawText(objectiveHeader, new Point(objectiveX, 682 - objectiveHeader.Baseline));
            objectiveX += objectiveHeader.Width + 32;
            dc.DrawText(objectives, new Point(objectiveX, 682 - objectives.Baseline));
            objectiveX += objectives.Width + objectiveGap;
        }
        dc.Pop();
    }

    private void DrawGuideTicker(DrawingContext dc, (TickerIcon Icon, string Name, string Description, uint Tint)[] entries,
        Rect clip, double speed, double phase)
    {
        dc.PushClip(new RectangleGeometry(clip));
        double cycleWidth = MeasureGuideCycle(entries);
        double x = clip.X + 20 - PositiveModulo(game.TotalTime * speed + phase, cycleWidth);
        while (x < clip.Right)
        {
            foreach (var entry in entries)
            {
                Color tint = FromArgb(entry.Tint);
                var iconCenter = new Point(x + 17, 560);
                dc.PushTransform(new ScaleTransform(1.2, 1.2, iconCenter.X, iconCenter.Y));
                DrawTickerIcon(dc, entry.Icon, iconCenter, tint);
                dc.Pop();
                x += 42;
                FormattedText name = Format(entry.Name, 14, new SolidColorBrush(tint), FontWeights.Bold);
                dc.DrawText(name, new Point(x, 565 - name.Baseline));
                x += name.Width + 10;
                FormattedText description = Format(entry.Description, 13,
                    new SolidColorBrush(Color.FromRgb(171, 194, 207)), FontWeights.Normal);
                dc.DrawText(description, new Point(x, 565 - description.Baseline));
                x += description.Width + 50;
            }
        }
        dc.Pop();
    }

    private void DrawTickerIcon(DrawingContext dc, TickerIcon icon, Point center, Color tint)
    {
        BitmapImage? sprite = icon switch
        {
            TickerIcon.Canister => canisterSprite,
            TickerIcon.Cash => dollarSprite,
            TickerIcon.Multiplier => multiplierSprite,
            TickerIcon.Asteroid => asteroidSprites[0],
            TickerIcon.SteelAsteroid or TickerIcon.MetalStorm => metalAsteroidSprite,
            TickerIcon.Raider => raiderSprite,
            TickerIcon.Interceptor => interceptorSprite,
            _ => null
        };
        if (sprite is not null)
        {
            double width = icon is TickerIcon.Raider or TickerIcon.Interceptor ? 30 : 27;
            double height = icon is TickerIcon.Raider or TickerIcon.Interceptor ? 24 : 27;
            dc.DrawImage(sprite, new Rect(center.X - width / 2, center.Y - height / 2, width, height));
            return;
        }

        var brush = new SolidColorBrush(tint);
        var pale = new SolidColorBrush(Lighten(tint, .48));
        var pen = new Pen(pale, 1.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.PushTransform(new TranslateTransform(center.X, center.Y));
        switch (icon)
        {
            case TickerIcon.RapidFire:
                for (int y = -7; y <= 7; y += 7)
                {
                    dc.DrawLine(pen, new Point(-12, y), new Point(5, y));
                    dc.DrawEllipse(brush, null, new Point(9, y), 3.5, 2.4);
                }
                break;
            case TickerIcon.AirBrakes:
                dc.DrawLine(pen, new Point(-12, -9), new Point(-4, 0));
                dc.DrawLine(pen, new Point(-4, 0), new Point(-12, 9));
                dc.DrawLine(pen, new Point(12, -9), new Point(4, 0));
                dc.DrawLine(pen, new Point(4, 0), new Point(12, 9));
                dc.DrawRectangle(brush, null, new Rect(-2, -10, 4, 20));
                break;
            case TickerIcon.Luck:
                dc.DrawEllipse(brush, pen, new Point(-5, -5), 5.5, 5.5);
                dc.DrawEllipse(brush, pen, new Point(5, -5), 5.5, 5.5);
                dc.DrawEllipse(brush, pen, new Point(-5, 5), 5.5, 5.5);
                dc.DrawEllipse(brush, pen, new Point(5, 5), 5.5, 5.5);
                dc.DrawLine(pen, new Point(1, 7), new Point(8, 13));
                break;
            case TickerIcon.TripleFire:
                foreach (double angle in new[] { -.48, 0.0, .48 })
                {
                    Point end = new(12 * Math.Cos(angle), 12 * Math.Sin(angle));
                    dc.DrawLine(pen, new Point(-10, 0), end);
                    dc.DrawEllipse(brush, null, end, 3, 3);
                }
                break;
            case TickerIcon.LongRange:
                dc.DrawLine(pen, new Point(-13, 0), new Point(10, 0));
                dc.DrawLine(pen, new Point(5, -5), new Point(11, 0));
                dc.DrawLine(pen, new Point(5, 5), new Point(11, 0));
                dc.DrawEllipse(brush, null, new Point(-10, 0), 3, 3);
                break;
            case TickerIcon.Shield:
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(55, tint.R, tint.G, tint.B)), pen,
                    new Point(0, 0), 12, 12);
                dc.DrawArc(new Pen(pale, 2.4), new Point(0, 0), 9, -60, 150);
                dc.DrawArc(new Pen(pale, 2.4), new Point(0, 0), 9, 130, 95);
                break;
            case TickerIcon.Freeze:
                for (int i = 0; i < 3; i++)
                {
                    double angle = i * Math.PI / 3;
                    V2 axis = V2.FromAngle(angle) * 12;
                    dc.DrawLine(pen, new Point(-axis.X, -axis.Y), new Point(axis.X, axis.Y));
                }
                dc.DrawEllipse(pale, null, new Point(0, 0), 3, 3);
                break;
            case TickerIcon.SmartBomb:
                var bomb = new RadialGradientBrush(Lighten(tint, .6), Darken(tint, .52));
                dc.DrawEllipse(bomb, pen, new Point(0, 2), 10, 10);
                dc.DrawLine(pen, new Point(5, -7), new Point(10, -13));
                dc.DrawLine(new Pen(Brushes.White, 1.8), new Point(9, -13), new Point(13, -10));
                break;
            case TickerIcon.Comet:
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(65, tint.R, tint.G, tint.B)), 5),
                    new Point(-14, 5), new Point(5, -2));
                dc.DrawLine(pen, new Point(-14, 9), new Point(6, 0));
                dc.DrawEllipse(new RadialGradientBrush(Colors.White, tint), pen, new Point(7, -1), 7, 7);
                break;
            case TickerIcon.RescueShip:
                dc.PushTransform(new ScaleTransform(.55, .55));
                dc.DrawGeometry(brush, pen, ShipGeometry(0));
                dc.Pop();
                break;
            case TickerIcon.Mine:
                for (int i = 0; i < 8; i++)
                {
                    V2 direction = V2.FromAngle(i * Math.PI / 4);
                    dc.DrawLine(pen, new Point(direction.X * 7, direction.Y * 7),
                        new Point(direction.X * 14, direction.Y * 14));
                }
                dc.DrawEllipse(new RadialGradientBrush(Lighten(tint, .5), Darken(tint, .5)), pen,
                    new Point(0, 0), 8, 8);
                break;
            case TickerIcon.BlackHole:
                dc.DrawEllipse(Brushes.Black, new Pen(brush, 2.6), new Point(0, 0), 7, 7);
                dc.DrawArc(pen, new Point(0, 0), 12, game.TotalTime * 150, 225);
                dc.DrawArc(new Pen(brush, 1.2), new Point(0, 0), 9, -game.TotalTime * 190, 150);
                break;
            case TickerIcon.Supernova:
                for (int i = 0; i < 8; i++)
                {
                    V2 direction = V2.FromAngle(i * Math.PI / 4);
                    dc.DrawLine(pen, new Point(direction.X * 4, direction.Y * 4),
                        new Point(direction.X * 14, direction.Y * 14));
                }
                dc.DrawEllipse(new RadialGradientBrush(Colors.White, tint), null, new Point(0, 0), 7, 7);
                break;
        }
        dc.Pop();
    }

    private static double MeasureGuideCycle((TickerIcon Icon, string Name, string Description, uint Tint)[] entries)
    {
        double width = 0;
        foreach (var entry in entries)
        {
            width += 42;
            width += Format(entry.Name, 14, Brushes.White, FontWeights.Bold).Width + 10;
            width += Format(entry.Description, 13, Brushes.White, FontWeights.Normal).Width + 50;
        }
        return width;
    }

    private void DrawControlsMenu(DrawingContext dc)
    {
        DrawCenteredText(dc, "CONTROLS", GameEngine.Width / 2, 91, 40, Brushes.White, FontWeights.Black);
        DrawCenteredText(dc, "KEYBOARD", GameEngine.Width / 2, 127, 14,
            new SolidColorBrush(Color.FromRgb(95, 209, 238)), FontWeights.SemiBold);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(110, 80, 166, 197)), 1), new Point(344, 151), new Point(936, 151));

        for (int i = 0; i < ControlBindings.Actions.Length; i++)
        {
            GameAction action = ControlBindings.Actions[i];
            bool selected = game.ControlSelection == i;
            double baseline = 203 + i * 49;
            Brush labelBrush = new SolidColorBrush(selected ? Color.FromRgb(122, 229, 255) : Color.FromRgb(177, 199, 211));
            if (selected) DrawText(dc, ">", 354, baseline, 17, labelBrush, FontWeights.Bold);
            DrawText(dc, ControlBindings.ActionName(action), 390, baseline, 17, labelBrush, selected ? FontWeights.Bold : FontWeights.SemiBold);

            string keyName = selected && game.WaitingForBinding
                ? (((int)(game.TotalTime * 3) & 1) == 0 ? "PRESS A KEY" : "")
                : ControlBindings.KeyName(game.Bindings[action]);
            Brush keyBrush = new SolidColorBrush(selected ? Color.FromRgb(240, 249, 252) : Color.FromRgb(120, 158, 178));
            DrawText(dc, keyName, 730, baseline, 17, keyBrush, FontWeights.Bold);
        }

        DrawCenteredText(dc, "ENTER  CHANGE       R  DEFAULTS       ESC  BACK", GameEngine.Width / 2, 642, 12,
            new SolidColorBrush(Color.FromRgb(103, 175, 199)), FontWeights.SemiBold);
    }

    private void DrawHighScores(DrawingContext dc)
    {
        var label = new SolidColorBrush(Color.FromRgb(123, 163, 187));
        DrawText(dc, "RANK", 393, 222, 11, label, FontWeights.SemiBold);
        DrawText(dc, "PILOT", 475, 222, 11, label, FontWeights.SemiBold);
        DrawText(dc, "SCORE", 687, 222, 11, label, FontWeights.SemiBold);
        DrawText(dc, "WAVE", 833, 222, 11, label, FontWeights.SemiBold);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(110, 79, 153, 184)), 1), new Point(388, 232), new Point(892, 232));

        for (int i = 0; i < 10; i++)
        {
            double baseline = 263 + i * 30;
            Brush rowBrush = i < 3
                ? new SolidColorBrush(i == 0 ? Color.FromRgb(255, 219, 104) : Color.FromRgb(205, 230, 238))
                : new SolidColorBrush(Color.FromRgb(164, 190, 205));
            if (i < game.HighScores.Count)
            {
                HighScoreEntry entry = game.HighScores[i];
                DrawText(dc, $"{i + 1:00}", 400, baseline, 14, rowBrush, FontWeights.Bold);
                DrawText(dc, entry.Name, 475, baseline, 14, rowBrush, FontWeights.SemiBold);
                DrawText(dc, Money(entry.Score), 687, baseline, 14, rowBrush, FontWeights.Bold);
                DrawText(dc, entry.Wave.ToString("00"), 846, baseline, 14, rowBrush, FontWeights.SemiBold);
            }
            else
            {
                DrawText(dc, $"{i + 1:00}", 400, baseline, 14, new SolidColorBrush(Color.FromRgb(70, 92, 107)), FontWeights.SemiBold);
                DrawText(dc, "---", 475, baseline, 14, new SolidColorBrush(Color.FromRgb(70, 92, 107)), FontWeights.Normal);
            }
        }
    }

    private static Geometry ShipGeometry(double expand)
        => Polygon((27 + expand, 0), (5, -8), (-14 - expand, -16 - expand), (-18 - expand, -8),
            (-9, 0), (-18 - expand, 8), (-14 - expand, 16 + expand), (5, 8));

    private static Geometry ShipDebrisGeometry(int kind) => kind switch
    {
        0 => Polygon((27, 0), (5, -8), (0, 0), (5, 8)),
        1 => Polygon((4, -7), (-14, -16), (-18, -8), (-9, 0), (0, 0)),
        2 => Polygon((0, 0), (-9, 0), (-18, 8), (-14, 16), (4, 7)),
        3 => Polygon((-17, -7), (-8, -4), (-8, 4), (-17, 7)),
        _ => Polygon((-5, -6), (10, 0), (-5, 6), (0, 0))
    };

    private static Geometry AsteroidGeometry(Asteroid rock)
    {
        int count = rock.Size == 3 ? 13 : rock.Size == 2 ? 11 : 9;
        var points = new (double x, double y)[count];
        for (int i = 0; i < count; i++)
        {
            double a = i * Math.PI * 2 / count;
            double noise = .78 + .25 * Hash(rock.Seed, i);
            points[i] = (Math.Cos(a) * rock.Radius * noise, Math.Sin(a) * rock.Radius * noise);
        }
        return Polygon(points);
    }

    private static double Hash(int seed, int index)
    {
        double x = Math.Sin(seed * .000013 + index * 78.233) * 43758.5453;
        return x - Math.Floor(x);
    }

    private static Geometry RegularPolygon(int sides, double radius, double offset)
    {
        var points = new (double x, double y)[sides];
        for (int i = 0; i < sides; i++)
        {
            double a = offset + i * Math.PI * 2 / sides;
            points[i] = (Math.Cos(a) * radius, Math.Sin(a) * radius);
        }
        return Polygon(points);
    }

    private static StreamGeometry Polygon(params (double x, double y)[] points)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(points[0].x, points[0].y), true, true);
        for (int i = 1; i < points.Length; i++) context.LineTo(new Point(points[i].x, points[i].y), true, false);
        geometry.Freeze();
        return geometry;
    }

    private static void DrawGlowGeometry(DrawingContext dc, Geometry geometry, Color color, double width)
    {
        for (int i = 3; i >= 1; i--)
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb((byte)(20 + i * 8), color.R, color.G, color.B)), width * i), geometry);
    }

    private static void DrawGlowEllipse(DrawingContext dc, V2 center, double radius, Color color, int layers, double intensity)
    {
        for (int i = layers; i >= 1; i--)
        {
            double r = radius + i * 4;
            byte alpha = (byte)(Math.Clamp(intensity, 0, 1) * 55 / i);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)), null, Pt(center), r, r);
        }
    }

    private static void DrawText(DrawingContext dc, string text, double x, double baseline, double size, Brush brush, FontWeight weight)
    {
        var ft = Format(text, size, brush, weight);
        dc.DrawText(ft, new Point(x, baseline - ft.Baseline));
    }

    private static void DrawCenteredText(DrawingContext dc, string text, double centerX, double baseline, double size, Brush brush, FontWeight weight)
    {
        var ft = Format(text, size, brush, weight);
        dc.DrawText(ft, new Point(centerX - ft.Width / 2, baseline - ft.Baseline));
    }

    private static FormattedText Format(string text, double size, Brush brush, FontWeight weight)
        => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal), size, brush, 1.0)
        { TextAlignment = TextAlignment.Left };

    private static Point Pt(V2 v) => new(v.X, v.Y);
    private static string Money(int value) => value.ToString("$#,0", CultureInfo.InvariantCulture);
    private static double EaseOut(double x) => 1 - Math.Pow(1 - Math.Clamp(x, 0, 1), 3);
    private static Color FromArgb(uint argb, byte? alpha = null)
        => Color.FromArgb(alpha ?? (byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
    private static Color Lighten(Color c, double amount)
        => Color.FromRgb((byte)(c.R + (255 - c.R) * amount), (byte)(c.G + (255 - c.G) * amount), (byte)(c.B + (255 - c.B) * amount));
    private static Color Darken(Color c, double amount)
        => Color.FromRgb((byte)(c.R * (1 - amount)), (byte)(c.G * (1 - amount)), (byte)(c.B * (1 - amount)));
}

internal static class DrawingContextArcExtensions
{
    public static void DrawArc(this DrawingContext dc, Pen pen, Point center, double radius, double startDegrees, double sweepDegrees)
    {
        double start = startDegrees * Math.PI / 180;
        double end = (startDegrees + sweepDegrees) * Math.PI / 180;
        Point p0 = new(center.X + Math.Cos(start) * radius, center.Y + Math.Sin(start) * radius);
        Point p1 = new(center.X + Math.Cos(end) * radius, center.Y + Math.Sin(end) * radius);
        var geometry = new StreamGeometry();
        using (var c = geometry.Open())
        {
            c.BeginFigure(p0, false, false);
            c.ArcTo(p1, new Size(radius, radius), 0, Math.Abs(sweepDegrees) > 180,
                sweepDegrees >= 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise, true, false);
        }
        dc.DrawGeometry(null, pen, geometry);
    }
}
