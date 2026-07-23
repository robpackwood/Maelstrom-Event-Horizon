using MaelstromEventHorizon.Application;
using MaelstromEventHorizon.Application.Services.Contracts;
using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;
using MaelstromEventHorizon.Presentation.Rendering;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MaelstromEventHorizon.Presentation;

internal sealed class GameView : FrameworkElement
{
    internal enum TickerIcon
    {
        Canister,
        RapidFire,
        AirBrakes,
        Luck,
        TripleFire,
        LongRange,
        Shield,
        Freeze,
        SmartBomb,
        RetroVision,
        RicochetArena,
        GiantShip,
        Cash,
        Multiplier,
        Comet,
        RescueShip,
        Asteroid,
        SteelAsteroid,
        Raider,
        Interceptor,
        Mine,
        BlackHole,
        Supernova,
        MetalStorm,
        AlienBoss
    }

    internal readonly GameEngine Game;
    internal readonly GameRenderServices Renderers;
    internal readonly IAssetProvider Assets;
    internal readonly Stopwatch Clock = Stopwatch.StartNew();
    internal readonly BitmapSource? Background;
    internal readonly BitmapSource?[] WaveBackgrounds = new BitmapSource?[8];
    internal readonly ImageBrush?[] BonusBackgroundBrushes = new ImageBrush?[8];
    internal readonly BitmapImage?[] AsteroidSprites;
    internal readonly BitmapImage? MetalAsteroidSprite;
    internal readonly BitmapImage? RaiderSprite;
    internal readonly BitmapImage? InterceptorSprite;
    internal readonly BitmapImage? CanisterSprite;
    internal readonly BitmapImage? DollarSprite;
    internal readonly BitmapImage? MultiplierSprite;
    internal readonly BitmapSource PlayerShipSprite;
    internal readonly BitmapSource GiantPlayerShipSprite;
    internal readonly BitmapSource RescueShipSprite;
    internal readonly BitmapSource MineBodySprite;
    internal readonly BitmapSource VortexCoreSprite;
    internal readonly BitmapSource NovaCoreSprite;
    internal readonly Dictionary<uint, BitmapSource> CometHeadSprites;
    internal readonly BitmapSource[][] BossSpriteFrames;
    internal double PreviousTime;
    internal bool FastBonusSampling;
    internal const int BossSpriteFrameCount = 12;
    internal const double BossSpriteFrameRate = 12;
    internal static readonly Color[] WaveGrades = [Color.FromRgb(8, 26, 50), Color.FromRgb(45, 7, 13), Color.FromRgb(4, 38, 27), Color.FromRgb(24, 8, 43), Color.FromRgb(48, 26, 3), Color.FromRgb(5, 31, 42), Color.FromRgb(37, 7, 34), Color.FromRgb(35, 19, 8)];
    internal static readonly Brush VignetteBrush = CreateVignetteBrush();
    internal static readonly (TickerIcon Icon, string Name, string Description, uint Tint)[] TitleItemGuide = [(TickerIcon.Canister, "ITEM CANISTER", "contains one random ship power-up", 0xff50eaff), (TickerIcon.RapidFire, "RAPID FIRE", "hold fire for 15-round bursts", 0xffff6e72), (TickerIcon.AirBrakes, "AIR BRAKES", "adds strong momentum control", 0xff73d8ff), (TickerIcon.Luck, "LUCK OF THE IRISH", "triggers every special event once this wave", 0xff72f09a), (TickerIcon.TripleFire, "TRIPLE FIRE", "launches a three-way shot", 0xffffb85d), (TickerIcon.LongRange, "LONG RANGE", "keeps shots active farther", 0xff8fd5ff), (TickerIcon.Shield, "SHIELD ENERGY", "restores shield charge", 0xff64edff), (TickerIcon.Freeze, "TIME FREEZE", "stops hostile motion for eight seconds", 0xffb18cff), (TickerIcon.SmartBomb, "SMART BOMB", "fractures asteroids and clears nearby threats", 0xffff7b68), (TickerIcon.RetroVision, "16-BIT VISION", "pixelates every graphic until the ship is lost", 0xffffd45d), (TickerIcon.RicochetArena, "RICOCHET ARENA", "seals the screen and makes every moving object bounce", 0xff74f3c5), (TickerIcon.GiantShip, "GIANT SHIP", "doubles ship size and absorbs one lethal hit", 0xffffd85a), (TickerIcon.Cash, "CASH BONUS", "adds dollars to the current wave", 0xffffd66b), (TickerIcon.Multiplier, "MULTIPLIER", "multiplies banked comet cash", 0xffd08aff), (TickerIcon.Comet, "COMET", "shoot any part for $500 to $5,000", 0xff73e7ff), (TickerIcon.RescueShip, "RESCUE SHIP", "touch it to gain one extra life", 0xff79f3b2)];
    internal static readonly (TickerIcon Icon, string Name, string Description, uint Tint)[] TitleHazardGuide = [(TickerIcon.Asteroid, "ASTEROID", "fractures into smaller rocks when shot", 0xffd5c5aa), (TickerIcon.SteelAsteroid, "STEEL ASTEROID", "resists gunfire and hits hard", 0xffd4edf5), (TickerIcon.Raider, "RAIDER", "pursues the ship and fires scattered shots", 0xffff6984), (TickerIcon.Interceptor, "INTERCEPTOR", "moves faster and leads its fire", 0xff69e8ff), (TickerIcon.Mine, "HOMING MINE", "tracks the ship and takes five shots to destroy", 0xffffdf58), (TickerIcon.BlackHole, "BLACK HOLE", "pulls the ship inward; contact is fatal", 0xffc187ff), (TickerIcon.Supernova, "SUPERNOVA", "shoot it before it detonates", 0xffff8c50), (TickerIcon.MetalStorm, "DODGE TRIAL", "six intense patterns; weapons and shields disabled", 0xffb9e8f7), (TickerIcon.AlienBoss, "ALIEN BOSS", "appears after every dodge trial; learn its attack pattern", 0xff91ef62)];
    internal const string TitleObjectives = "BREAK ASTEROIDS  /  SURVIVE EACH WAVE  /  DESTROY ENEMY SHIPS  /  SHOOT COMETS FOR CASH  /  " + "COLLECT POWER-UPS  /  BANK WAVE EARNINGS  /  SURVIVE A NEW DODGE TRIAL EVERY 5 WAVES  /  DEFEAT THE ALIEN BOSS THAT FOLLOWS  /  " + "EARN AN EXTRA SHIP EVERY $50,000  /  CHASE THE HIGHEST SCORE";
    public GameView(GameEngine game, IAssetProvider assets, GameRenderServices renderers)
    {
        this.Game = game;
        this.Renderers = renderers;
        this.Assets = assets;
        Focusable = true;
        SnapsToDevicePixels = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.Fant);
        AsteroidSprites = [LoadSprite("asteroid-basalt.png"), LoadSprite("asteroid-fractured.png"), LoadSprite("asteroid-iron.png")];
        MetalAsteroidSprite = LoadSprite("asteroid-metal.png");
        RaiderSprite = LoadSprite("enemy-raider.png");
        InterceptorSprite = LoadSprite("enemy-interceptor.png");
        CanisterSprite = LoadSprite("item-canister.png");
        DollarSprite = LoadSprite("bonus-dollar.png");
        MultiplierSprite = LoadSprite("bonus-multiplier.png");
        PlayerShipSprite = LoadOrPrerenderSprite("player-ship.png", 192, 72, dc => DrawDetailedShipHull(dc, 0, Color.FromRgb(72, 220, 255), false));
        GiantPlayerShipSprite = LoadOrPrerenderSprite("player-ship-giant.png", 192, 80, dc => DrawDetailedShipHull(dc, 0, Color.FromRgb(72, 220, 255), true));
        RescueShipSprite = LoadOrPrerenderSprite("rescue-ship.png", 192, 72, dc => DrawDetailedShipHull(dc, 0, Color.FromRgb(74, 255, 145), false));
        MineBodySprite = LoadOrPrerenderSprite("hazard-mine.png", 160, 54, DrawMineBody);
        VortexCoreSprite = LoadOrPrerenderSprite("hazard-black-hole-core.png", 192, 84, DrawVortexCore);
        NovaCoreSprite = LoadOrPrerenderSprite("hazard-supernova-core.png", 192, 104, DrawNovaCore);
        CometHeadSprites = BuildCometHeadSprites();
        BossSpriteFrames = BuildBossSpriteFrames();
        Background = LoadBitmapAsset("deep-space.png");
        LoadBackgroundAtlas("space-atlas-a.png", 0);
        LoadBackgroundAtlas("space-atlas-b.png", 4);
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
            if (game.Mode is not GameMode.Title and not GameMode.NameEntry)
                return;
            game.HandleTextInput(e.Text);
            e.Handled = true;
        };
        KeyDown += (_, e) =>
        {
            if (game.Mode == GameMode.NameEntry)
            {
                if (game.HandleNameEntryKey(e.Key))
                    e.Handled = true;
                return;
            }

            if (game.HandleCommandKey(e.Key, e.IsRepeat))
                e.Handled = true;
        };
    }

    private BitmapImage? LoadSprite(string filename)
    {
        try
        {
            string path = Assets.PathFor("Sprites", filename);
            var image = new BitmapImage();
            image.BeginInit();
            image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.Fant);
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    internal static BitmapSource PrerenderSprite(int pixelSize, double logicalSize, Action<DrawingContext> draw)
    {
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(pixelSize / 2.0, pixelSize / 2.0));
            double scale = pixelSize / logicalSize;
            dc.PushTransform(new ScaleTransform(scale, scale));
            draw(dc);
            dc.Pop();
            dc.Pop();
        }

        var bitmap = new RenderTargetBitmap(pixelSize, pixelSize, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        RenderOptions.SetBitmapScalingMode(bitmap, BitmapScalingMode.Fant);
        bitmap.Freeze();
        return bitmap;
    }

    private BitmapSource LoadOrPrerenderSprite(string filename, int pixelSize, double logicalSize, Action<DrawingContext> draw)
    {
        string? outputDirectory = Environment.GetEnvironmentVariable("MAELSTROM_SPRITE_OUTPUT");
        string? forceScope = Environment.GetEnvironmentVariable("MAELSTROM_SPRITE_FORCE");
        bool forceBake = !string.IsNullOrWhiteSpace(outputDirectory) && (forceScope == "1" || forceScope == "boss" && filename.StartsWith("boss-", StringComparison.Ordinal));
        BitmapImage? loaded = forceBake ? null : LoadSprite(filename);
        if (loaded is not null)
            return loaded;
        BitmapSource rendered = PrerenderSprite(pixelSize, logicalSize, draw);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
            string path = Path.Combine(outputDirectory, filename);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rendered));
            using FileStream stream = File.Create(path);
            encoder.Save(stream);
        }

        return rendered;
    }

    private Dictionary<uint, BitmapSource> BuildCometHeadSprites()
    {
        uint[] tints = [0xffff6e9e, 0xffc977ff, 0xff62dcff, 0xff63f0ca, 0xffffc65b];
        var sprites = new Dictionary<uint, BitmapSource>(tints.Length);
        foreach (uint tint in tints)
        {
            Color color = FromArgb(tint);
            sprites[tint] = LoadOrPrerenderSprite($"comet-head-{tint:x8}.png", 160, 80, dc => DrawCometHead(dc, color));
        }

        return sprites;
    }

    private BitmapSource[][] BuildBossSpriteFrames()
    {
        AlienBossKind[] kinds = Enum.GetValues<AlienBossKind>();
        var sprites = new BitmapSource[kinds.Length][];
        foreach (AlienBossKind kind in kinds)
        {
            var frames = new BitmapSource[BossSpriteFrameCount];
            var sample = new AlienBoss(V2.Zero, kind, 1);
            for (int frame = 0; frame < frames.Length; frame++)
            {
                sample.Age = frame / BossSpriteFrameRate;
                string filename = $"boss-{kind.ToString().ToLowerInvariant()}-{frame:00}.png";
                frames[frame] = LoadOrPrerenderSprite(filename, 320, 200, dc => DrawBossBody(dc, sample));
            }

            sprites[(int)kind] = frames;
        }

        return sprites;
    }

    private BitmapImage? LoadBitmapAsset(string filename)
    {
        try
        {
            string path = Assets.PathFor(filename);
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

    private void LoadBackgroundAtlas(string filename, int startIndex)
    {
        BitmapSource? atlas = LoadBitmapAsset(filename);
        if (atlas is null)
            return;
        int halfWidth = atlas.PixelWidth / 2;
        int halfHeight = atlas.PixelHeight / 2;
        for (int row = 0; row < 2; row++)
        {
            for (int column = 0; column < 2; column++)
            {
                int x = column * halfWidth;
                int y = row * halfHeight;
                int width = column == 0 ? halfWidth : atlas.PixelWidth - halfWidth;
                int height = row == 0 ? halfHeight : atlas.PixelHeight - halfHeight;
                var crop = new CroppedBitmap(atlas, new Int32Rect(x, y, width, height));
                crop.Freeze();
                int index = startIndex + row * 2 + column;
                WaveBackgrounds[index] = crop;
                BonusBackgroundBrushes[index] = CreateBonusBackgroundBrush(crop);
            }
        }
    }

    internal static ImageBrush CreateBonusBackgroundBrush(BitmapSource source)
    {
        var brush = new ImageBrush(source)
        {
            Stretch = Stretch.UniformToFill,
            TileMode = TileMode.FlipXY,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, GameEngine.Width, GameEngine.Height),
            ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
            Viewbox = new Rect(0, 0, 1, 1),
            Transform = new TranslateTransform()
        };
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.Linear);
        return brush;
    }

    internal static Brush CreateVignetteBrush()
    {
        var vignette = new RadialGradientBrush
        {
            GradientOrigin = new Point(.5, .48),
            Center = new Point(.5, .48),
            RadiusX = .72,
            RadiusY = .78
        };
        vignette.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), .35));
        vignette.GradientStops.Add(new GradientStop(Color.FromArgb(155, 0, 0, 5), 1));
        vignette.Freeze();
        return vignette;
    }

    private void RenderFrame(object? sender, EventArgs e)
    {
        double now = Clock.Elapsed.TotalSeconds;
        double dt = PreviousTime == 0 ? 1.0 / 60 : now - PreviousTime;
        PreviousTime = now;
        Game.Update(dt);
        bool useFastSampling = Game is { IsBonusStage: true, Mode: GameMode.Playing };
        if (useFastSampling != FastBonusSampling)
        {
            FastBonusSampling = useFastSampling;
            RenderOptions.SetBitmapScalingMode(this, FastBonusSampling ? BitmapScalingMode.Linear : BitmapScalingMode.Fant);
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (ActualWidth < 1 || ActualHeight < 1)
            return;
        double scale = Math.Min(ActualWidth / GameEngine.Width, ActualHeight / GameEngine.Height);
        double x = (ActualWidth - GameEngine.Width * scale) / 2;
        double y = (ActualHeight - GameEngine.Height * scale) / 2;
        dc.PushClip(new RectangleGeometry(new Rect(x, y, GameEngine.Width * scale, GameEngine.Height * scale)));
        dc.PushTransform(new TranslateTransform(x, y));
        dc.PushTransform(new ScaleTransform(scale, scale));
        if (Game.RetroVisionActive)
            DrawRetroFrame(dc);
        else
            DrawGameCanvas(dc);
        dc.Pop();
        dc.Pop();
        dc.Pop();
    }

    internal void DrawGameCanvas(DrawingContext dc) => Renderers.SceneRenderer.DrawGameCanvas(this, dc);
    internal void DrawRetroFrame(DrawingContext dc) => Renderers.SceneRenderer.DrawRetroFrame(this, dc);
    internal double PositiveModulo(double value, double modulus) => Renderers.SceneRenderer.PositiveModulo(value, modulus);
    internal void DrawShip(DrawingContext dc) => Renderers.PlayerAsteroidRenderer.DrawShip(this, dc);
    internal void DrawDetailedShipHull(DrawingContext dc, double time, Color accent, bool armored) => Renderers.PlayerAsteroidRenderer.DrawDetailedShipHull(this, dc, time, accent, armored);
    internal void DrawShipDebris(DrawingContext dc) => Renderers.PlayerAsteroidRenderer.DrawShipDebris(this, dc);
    internal void DrawAsteroids(DrawingContext dc) => Renderers.PlayerAsteroidRenderer.DrawAsteroids(this, dc);
    internal void DrawFighters(DrawingContext dc) => Renderers.CombatActorRenderer.DrawFighters(this, dc);
    internal void DrawBosses(DrawingContext dc) => Renderers.CombatActorRenderer.DrawBosses(this, dc);
    internal void DrawBossBody(DrawingContext dc, AlienBoss boss) => Renderers.CombatActorRenderer.DrawBossBody(this, dc, boss);
    internal Color BossColor(AlienBossKind kind) => Renderers.CombatActorRenderer.BossColor(kind);
    internal void DrawMines(DrawingContext dc) => Renderers.HazardPickupRenderer.DrawMines(this, dc);
    internal void DrawMineBody(DrawingContext dc) => Renderers.HazardPickupRenderer.DrawMineBody(this, dc);
    internal void DrawVortices(DrawingContext dc) => Renderers.HazardPickupRenderer.DrawVortices(this, dc);
    internal void DrawVortexCore(DrawingContext dc) => Renderers.HazardPickupRenderer.DrawVortexCore(dc);
    internal void DrawNovas(DrawingContext dc) => Renderers.HazardPickupRenderer.DrawNovas(this, dc);
    internal void DrawNovaCore(DrawingContext dc) => Renderers.HazardPickupRenderer.DrawNovaCore(dc);
    internal void DrawPickups(DrawingContext dc) => Renderers.HazardPickupRenderer.DrawPickups(this, dc);
    internal void DrawComets(DrawingContext dc) => Renderers.HazardPickupRenderer.DrawComets(this, dc);
    internal void DrawCometHead(DrawingContext dc, Color color) => Renderers.HazardPickupRenderer.DrawCometHead(this, dc, color);
    internal void DrawFloatingTexts(DrawingContext dc) => Renderers.EffectsHudRenderer.DrawFloatingTexts(this, dc);
    internal void DrawShots(DrawingContext dc) => Renderers.EffectsHudRenderer.DrawShots(this, dc);
    internal void DrawArenaFrame(DrawingContext dc) => Renderers.EffectsHudRenderer.DrawArenaFrame(dc);
    internal void DrawParticles(DrawingContext dc) => Renderers.EffectsHudRenderer.DrawParticles(this, dc);
    internal void DrawShockwaves(DrawingContext dc) => Renderers.EffectsHudRenderer.DrawShockwaves(this, dc);
    internal void DrawHud(DrawingContext dc) => Renderers.EffectsHudRenderer.DrawHud(this, dc);
    internal void DrawOverlay(DrawingContext dc) => Renderers.OverlayRenderer.DrawOverlay(this, dc);
    internal void DrawTransitionCurtain(DrawingContext dc) => Renderers.OverlayRenderer.DrawTransitionCurtain(this, dc);
    internal void DrawTitleTickers(DrawingContext dc) => Renderers.TitleScreenRenderer.DrawTitleTickers(this, dc);
    internal void DrawControlsMenu(DrawingContext dc) => Renderers.TitleScreenRenderer.DrawControlsMenu(this, dc);
    internal void DrawHighScores(DrawingContext dc) => Renderers.DrawingPrimitiveService.DrawHighScores(this, dc);
    internal Geometry ShipGeometry(double expand) => Renderers.DrawingPrimitiveService.ShipGeometry(expand);
    internal Geometry ShipDebrisGeometry(int kind) => Renderers.DrawingPrimitiveService.ShipDebrisGeometry(kind);
    internal Geometry AsteroidGeometry(Asteroid rock) => Renderers.DrawingPrimitiveService.AsteroidGeometry(rock);
    internal double Hash(int seed, int index) => Renderers.DrawingPrimitiveService.Hash(seed, index);
    internal Geometry RegularPolygon(int sides, double radius, double offset) => Renderers.DrawingPrimitiveService.RegularPolygon(sides, radius, offset);
    internal void DrawGlowGeometry(DrawingContext dc, Geometry geometry, Color color, double width) => Renderers.DrawingPrimitiveService.DrawGlowGeometry(dc, geometry, color, width);
    internal void DrawGlowEllipse(DrawingContext dc, V2 center, double radius, Color color, int layers, double intensity) => Renderers.DrawingPrimitiveService.DrawGlowEllipse(dc, center, radius, color, layers, intensity);
    internal void DrawText(DrawingContext dc, string text, double x, double baseline, double size, Brush brush, FontWeight weight) => Renderers.DrawingPrimitiveService.DrawText(dc, text, x, baseline, size, brush, weight);
    internal void DrawCenteredText(DrawingContext dc, string text, double centerX, double baseline, double size, Brush brush, FontWeight weight) => Renderers.DrawingPrimitiveService.DrawCenteredText(dc, text, centerX, baseline, size, brush, weight);
    internal FormattedText Format(string text, double size, Brush brush, FontWeight weight) => Renderers.DrawingPrimitiveService.Format(text, size, brush, weight);
    internal Point Pt(V2 v) => Renderers.DrawingPrimitiveService.Pt(v);
    internal string Money(int value) => Renderers.DrawingPrimitiveService.Money(value);
    internal double EaseOut(double x) => Renderers.DrawingPrimitiveService.EaseOut(x);
    internal Color FromArgb(uint argb, byte? alpha = null) => Renderers.DrawingPrimitiveService.FromArgb(argb, alpha);
    internal Color Lighten(Color c, double amount) => Renderers.DrawingPrimitiveService.Lighten(c, amount);
    internal Color Darken(Color c, double amount) => Renderers.DrawingPrimitiveService.Darken(c, amount);
    internal StreamGeometry Polygon(params (double x, double y)[] points) => Renderers.DrawingPrimitiveService.Polygon(points);
}
