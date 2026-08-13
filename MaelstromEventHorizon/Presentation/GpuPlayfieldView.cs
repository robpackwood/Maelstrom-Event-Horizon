using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Diagnostics;
using MaelstromEventHorizon.Application;
using MaelstromEventHorizon.Domain.Effects;
using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;
using Vortice.Direct3D9;

namespace MaelstromEventHorizon.Presentation;

internal sealed class GpuPlayfieldView : Image
{
    private const int FloatsPerVertex = 6;
    private const int MaximumCircleSegments = 20;
    private const int SurfaceWidth = 1280;
    private const int SurfaceHeight = 720;
    private static readonly V2[][] unitCirclePoints = CreateUnitCirclePoints();
    private readonly GameEngine game;
    private readonly FrameTimingProfiler frameTimings;
    private readonly TransparentEffectBudget transparentEffects = new();
    private float[] vertices = new float[16_384];
    private readonly DirectTriangleBatch directBatch = new();
    private int floatCount;
    private long lastPresentedFrame = -1;
    private bool initialized;
    private bool unavailable;
    private D3DImage? image;
    private IDirect3D9Ex? direct3D;
    private IDirect3DDevice9Ex? device;
    private IDirect3DSurface9? surface;
    private CachedBackdropPass? backdrop;
    private GpuFrameTimer? gpuFrameTimer;
    private InstancedSpriteBatch? spriteBatch;

    internal event Action<Exception>? Failed;

    public GpuPlayfieldView(GameEngine game, FrameTimingProfiler frameTimings)
    {
        this.game = game;
        this.frameTimings = frameTimings;
        Focusable = false;
        IsHitTestVisible = false;
        Stretch = Stretch.Uniform;
        Loaded += InitializeRenderer;
        Unloaded += DisposeRenderer;
    }

    private void InitializeRenderer(object? sender, RoutedEventArgs e)
    {
        try
        {
            IntPtr windowHandle = new WindowInteropHelper(Window.GetWindow(this)!).Handle;
            D3D9.Direct3DCreate9Ex(out direct3D).CheckError();

            PresentParameters presentation = new()
            {
                Windowed = true,
                SwapEffect = SwapEffect.Discard,
                DeviceWindowHandle = windowHandle,
                BackBufferFormat = Format.A8R8G8B8,
                BackBufferWidth = SurfaceWidth,
                BackBufferHeight = SurfaceHeight,
                PresentationInterval = PresentInterval.Immediate
            };

            device = direct3D.CreateDeviceEx(0, DeviceType.Hardware, windowHandle,
                CreateFlags.HardwareVertexProcessing | CreateFlags.Multithreaded | CreateFlags.FpuPreserve,
                presentation, new DisplayModeEx());

            IntPtr sharedHandle = IntPtr.Zero;

            surface = device.CreateRenderTarget(
                SurfaceWidth, SurfaceHeight, Format.A8R8G8B8, MultisampleType.None, 0, false, ref sharedHandle);

            image = new D3DImage();
            image.Lock();
            image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, surface.NativePointer);
            image.Unlock();
            Source = image;
            device.SetRenderState(RenderState.AlphaBlendEnable, true);
            device.SetRenderState(RenderState.SourceBlend, Blend.SourceAlpha);
            device.SetRenderState(RenderState.DestinationBlend, Blend.InverseSourceAlpha);
            device.SetRenderState(RenderState.ZEnable, false);
            device.SetRenderState(RenderState.CullMode, Cull.None);
            device.SetRenderState(RenderState.Lighting, false);
            device.VertexFormat = VertexFormat.PositionRhw | VertexFormat.Diffuse;
            backdrop = new CachedBackdropPass(device, game);
            spriteBatch = new InstancedSpriteBatch(device);
            gpuFrameTimer = GpuFrameTimer.TryCreate(device);
            initialized = true;
            CompositionTarget.Rendering += RenderFrame;
        }
        catch (Exception error)
        {
            ReportFailure(error);
        }
    }

    private void RenderFrame(object? sender, EventArgs e)
    {
        if (!initialized || unavailable || lastPresentedFrame == game.PresentedFrame)
        {
            return;
        }

        lastPresentedFrame = game.PresentedFrame;

        try
        {
            long submissionStartedAt = Stopwatch.GetTimestamp();
            floatCount = 0;
            spriteBatch!.Clear();
            transparentEffects.Reset(game.VisualQuality);
            DrawPlayfield();
            directBatch.Upload(vertices, floatCount);

            image!.Lock();
            device!.SetRenderTarget(0, surface!);
            device.BeginScene();
            gpuFrameTimer?.Begin();
            int directVertexCount = floatCount / FloatsPerVertex;
            backdrop!.Draw(device, BackdropColor());
            spriteBatch.Draw(device);
            directBatch.Draw(device, directVertexCount, 0, directVertexCount);
            device.EndScene();
            gpuFrameTimer?.End();
            image.AddDirtyRect(new Int32Rect(0, 0, SurfaceWidth, SurfaceHeight));
            image.Unlock();
            double gpuMilliseconds = 0;
            bool hardwareTiming = gpuFrameTimer is not null && gpuFrameTimer.TryGetLatest(out gpuMilliseconds);

            frameTimings.RecordGpuPlayfield(hardwareTiming
                    ? gpuMilliseconds : FrameTimingProfiler.ElapsedMilliseconds(submissionStartedAt),
                hardwareTiming);
        }
        catch (Exception error)
        {
            ReportFailure(error);
        }
    }

    private void ReportFailure(Exception error)
    {
        if (unavailable)
        {
            return;
        }

        unavailable = true;
        initialized = false;
        CompositionTarget.Rendering -= RenderFrame;
        gpuFrameTimer?.Dispose();
        Failed?.Invoke(error);
    }

    private V2 RenderPosition(Body body) => body.Position + body.Velocity * (game.RenderInterpolation / 120);

    private static bool IsWithinPlayfield(V2 position, double extent) =>
        position.X + extent >= 0 && position.X - extent <= GameEngine.Width &&
        position.Y + extent >= 0 && position.Y - extent <= GameEngine.Height;

    private double TrailDetailScale => game.VisualQuality switch { 0 => .45, 1 => .7, _ => 1 };

    private int ScaleSegmentCount(int sides)
    {
        double scale = game.VisualQuality switch { 0 => .55, 1 => .75, _ => 1 };
        return Math.Clamp(Math.Max(6, (int)Math.Ceiling(sides * scale)), 3, MaximumCircleSegments);
    }

    private void DisposeRenderer(object? sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering -= RenderFrame;
        backdrop?.Dispose();
        spriteBatch?.Dispose();
        directBatch.Dispose();
        surface?.Dispose();
        device?.Dispose();
        direct3D?.Dispose();
    }

    private void DrawPlayfield()
    {
        if (game.Mode is GameMode.Title or GameMode.Controls)
        {
            return;
        }

        V2 shake = game.ScreenShakeOffset;
        DrawVortices(shake);
        DrawNovas(shake);
        DrawComets(shake);
        DrawPickups(shake);
        DrawAsteroids(shake);
        DrawFighters(shake);
        DrawBosses(shake);
        DrawMines(shake);
        DrawShots(shake);
        DrawShip(shake);
        DrawParticles(shake);
        DrawShockwaves(shake);
    }

    private void DrawAsteroids(V2 shake)
    {
        foreach (Asteroid asteroid in game.Asteroids)
        {
            uint color = asteroid.Steel
                ? 0xff8faec2
                : asteroid.Colossal
                    ? 0xffff3c7d
                    : asteroid.Mega
                        ? 0xffff704d
                        : 0xff98796b;

            V2 position = RenderPosition(asteroid) + shake;
            if (!IsWithinPlayfield(position, asteroid.Radius * 1.1))
            {
                continue;
            }

            AddCircle(position.X, position.Y, asteroid.Radius, color, .96, 12);

            AddEffectRing(position.X, position.Y, asteroid.Radius * .88,
                asteroid.Steel ? 0xffd8f5ff : asteroid.Colossal ? 0xffffd8e5 : 0xffd6ad8c, .7, 12, 1.2);
        }
    }

    private void DrawFighters(V2 shake)
    {
        foreach (Fighter fighter in game.Fighters)
        {
            uint color = fighter.Kind == FighterKind.Interceptor ? 0xff5eeaff : 0xffff4f85;
            V2 position = RenderPosition(fighter) + shake;

            if (IsWithinPlayfield(position, fighter.Radius * 1.1))
            {
                AddShip(position, fighter.Angle, fighter.Radius * 1.1, color, .95);
            }
        }
    }

    private void DrawBosses(V2 shake)
    {
        foreach (AlienBoss boss in game.Bosses)
        {
            uint color = BossColor(boss.Kind);
            V2 position = RenderPosition(boss) + shake;
            if (!IsWithinPlayfield(position, boss.Radius * 1.2))
            {
                continue;
            }

            AddCircle(position.X, position.Y, boss.Radius, color, .94, 18);
            AddEffectRing(position.X, position.Y, boss.Radius * .88, 0xffefffff, .58, 18, 1.5);

            if (game.VisualQuality > 0)
            {
                AddEffectRing(position.X, position.Y, boss.Radius * 1.08, color,
                    game.VisualQuality == 1 ? .14 : .22, 18, 3);
            }
        }
    }

    private void DrawMines(V2 shake)
    {
        foreach (HomingMine mine in game.Mines)
        {
            V2 position = RenderPosition(mine) + shake;
            if (!IsWithinPlayfield(position, mine.Radius * 1.4))
            {
                continue;
            }

            AddCircle(position.X, position.Y, mine.Radius, 0xffffd84e, .95, 10);
            AddEffectRing(position.X, position.Y, mine.Radius * 1.24, 0xffff643d, .62, 10, 1.4);
        }
    }

    private void DrawVortices(V2 shake)
    {
        foreach (GravityVortex vortex in game.Vortices)
        {
            V2 position = RenderPosition(vortex) + shake;
            if (!IsWithinPlayfield(position, vortex.Radius * 1.25))
            {
                continue;
            }

            AddEffectCircle(position.X, position.Y, vortex.Radius, 0xff5b1e80, .7, 16);

            if (game.VisualQuality > 0)
            {
                AddEffectRing(position.X, position.Y, vortex.Radius * 1.15, 0xffcc8cff, .7, 16, 2);
            }
        }
    }

    private void DrawNovas(V2 shake)
    {
        foreach (Nova nova in game.Novas)
        {
            V2 position = RenderPosition(nova) + shake;
            if (!IsWithinPlayfield(position, nova.Radius * 1.5))
            {
                continue;
            }

            AddEffectCircle(position.X, position.Y, nova.Radius * 1.45, 0xffff613c, .85, 12);
            AddCircle(position.X, position.Y, nova.Radius * .55, 0xffffefa7, 1, 10);
        }
    }

    private void DrawComets(V2 shake)
    {
        foreach (Comet comet in game.Comets)
        {
            V2 position = RenderPosition(comet) + shake;
            double trailLength = Comet.TrailLength * TrailDetailScale;
            if (!IsWithinPlayfield(position, trailLength + comet.Radius))
            {
                continue;
            }

            V2 tail = position - comet.Velocity.Normalized * trailLength;
            AddLine(tail.X, tail.Y, position.X, position.Y, 0xffa7e8ff, .42, 7);
            AddCircle(position.X, position.Y, comet.Radius, comet.Tint, .98, 12);
        }
    }

    private void DrawPickups(V2 shake)
    {
        foreach (Pickup pickup in game.Pickups)
        {
            V2 position = RenderPosition(pickup) + shake;
            if (!IsWithinPlayfield(position, pickup.Radius * 1.6))
            {
                continue;
            }

            uint color = pickup.Kind switch
            {
                PickupKind.RescueShip => 0xff76ffbe,
                PickupKind.Bonus => 0xffffd65d,
                PickupKind.Multiplier => 0xffdf97ff,
                PickupKind.TimeFreeze => 0xff9eb9ff,
                PickupKind.SmartBomb => 0xffff7b70,
                PickupKind.RicochetArena => 0xff76f2ce,
                _ => 0xff5eeaff
            };

            AddDiamond(position.X, position.Y, pickup.Radius * 1.25, color, .96);
            if (game.VisualQuality > 0)
            {
                AddEffectRing(position.X, position.Y, pickup.Radius * 1.5, color, .36, 8, 1);
            }
        }
    }

    private void DrawShots(V2 shake)
    {
        foreach (Shot shot in game.Shots)
        {
            V2 position = RenderPosition(shot) + shake;
            double radius = shot.BossShot ? Math.Max(6, shot.Radius) : Math.Max(3.8, shot.Radius);
            double trailLength = shot.Laser ? 30 * TrailDetailScale : 0;
            if (!IsWithinPlayfield(position, trailLength + radius))
            {
                continue;
            }

            uint color = shot.BossShot ? shot.Tint : shot.Enemy ? 0xff5877ff : PlayerShotColor(shot.PowerLevel);

            if (shot.Laser)
            {
                V2 tail = position - shot.Velocity.Normalized * trailLength;
                AddLine(tail.X, tail.Y, position.X, position.Y, color, .62, 5);
            }

            AddCircle(position.X, position.Y, radius, color, .96, 8);
        }
    }

    private void DrawShip(V2 shake)
    {
        Ship ship = game.Player;

        if (game.PlayerRespawning || game.Mode is GameMode.Paused or GameMode.WaveSummary or GameMode.WaveSummaryExit)
        {
            return;
        }

        V2 position = RenderPosition(ship) + shake;
        if (!IsWithinPlayfield(position, Math.Max(45 * ship.VisualScale, 40)))
        {
            return;
        }

        if (ship.Thrusting)
        {
            V2 tail = position - V2.FromAngle(ship.Angle) * 45 * ship.VisualScale * TrailDetailScale;
            AddLine(tail.X, tail.Y, position.X, position.Y, 0xff61dfff, .8, 10);
        }

        AddShip(position, ship.Angle, ship.Radius * 1.18, 0xffdceef4, 1);

        if (ship.Shielding || ship.SpawnShieldTime > 0)
        {
            AddRing(position.X, position.Y, 32 * ship.VisualScale, 0xff66edff, .82, 20, 2);
        }

        if (game.ReflectionShieldActive)
        {
            AddRing(position.X, position.Y, 37 * ship.VisualScale, 0xffffc252, .82, 20, 2.4);
        }
    }

    private void DrawParticles(V2 shake)
    {
        foreach (Particle particle in game.Particles)
        {
            double life = Math.Clamp(1 - particle.Age / particle.Lifetime, 0, 1);
            V2 position = RenderPosition(particle) + shake;
            double radius = Math.Max(1, particle.StartSize * (.3 + life * .7));
            if (!IsWithinPlayfield(position, radius))
            {
                continue;
            }

            AddEffectCircle(position.X, position.Y, radius, particle.Color, life, 6);
        }
    }

    private void DrawShockwaves(V2 shake)
    {
        foreach (Shockwave ring in game.Shockwaves)
        {
            double progress = ring.Age / ring.Lifetime;
            double radius = ring.MaxRadius * (1 - Math.Pow(1 - progress, 3));
            V2 position = ring.Position + shake;
            if (!IsWithinPlayfield(position, radius + 4))
            {
                continue;
            }

            AddEffectRing(position.X, position.Y, radius,
                ring.Color, .82 * (1 - progress), 20, Math.Max(1, 4 * (1 - progress)));
        }
    }

    private void AddShip(V2 position, double angle, double radius, uint color, double alpha)
    {
        V2 forward = V2.FromAngle(angle);
        V2 side = new(-forward.Y, forward.X);

        AddTriangle(position + forward * radius, position - forward * radius * .78 + side * radius * .66,
            position - forward * radius * .78 - side * radius * .66, color, alpha);
    }

    private void AddDiamond(double x, double y, double radius, uint color, double alpha)
    {
        AddTriangle(new V2(x, y - radius), new V2(x + radius, y), new V2(x, y + radius), color, alpha);
        AddTriangle(new V2(x, y - radius), new V2(x, y + radius), new V2(x - radius, y), color, alpha);
    }

    private void AddCircle(double x, double y, double radius, uint color, double alpha, int sides)
    {
        sides = ScaleSegmentCount(sides);

        if (sides <= 12)
        {
            spriteBatch!.Add(x, y, radius, color, alpha);
            return;
        }

        V2[] points = unitCirclePoints[sides];

        for (int i = 0; i < sides; i++)
        {
            V2 first = points[i];
            V2 second = points[(i + 1) % sides];

            AddTriangle(new V2(x, y), new V2(x + first.X * radius, y + first.Y * radius),
                new V2(x + second.X * radius, y + second.Y * radius), color, alpha);
        }
    }

    private void AddEffectCircle(double x, double y, double radius, uint color, double alpha, int sides)
    {
        double scale = transparentEffects.ReserveDisk(radius);

        if (scale > 0)
        {
            AddCircle(x, y, radius * scale, color, alpha, sides);
        }
    }

    private void AddRing(double x, double y, double radius, uint color, double alpha, int sides, double thickness)
    {
        sides = ScaleSegmentCount(sides);
        V2[] points = unitCirclePoints[sides];

        for (int i = 0; i < sides; i++)
        {
            V2 first = points[i];
            V2 second = points[(i + 1) % sides];

            AddLine(x + first.X * radius, y + first.Y * radius,
                x + second.X * radius, y + second.Y * radius, color, alpha, thickness);
        }
    }

    private static V2[][] CreateUnitCirclePoints()
    {
        V2[][] pointsBySegmentCount = new V2[MaximumCircleSegments + 1][];

        for (int sides = 3; sides <= MaximumCircleSegments; sides++)
        {
            V2[] points = new V2[sides];

            for (int i = 0; i < sides; i++)
            {
                double angle = i * Math.PI * 2 / sides;
                points[i] = new V2(Math.Cos(angle), Math.Sin(angle));
            }

            pointsBySegmentCount[sides] = points;
        }

        return pointsBySegmentCount;
    }

    private void AddEffectRing(double x, double y, double radius, uint color, double alpha, int sides, double thickness)
    {
        double scale = transparentEffects.ReserveRing(radius, thickness);

        if (scale > 0)
        {
            AddRing(x, y, radius, color, alpha, Math.Max(6, (int)Math.Round(sides * scale)), thickness * scale);
        }
    }

    private void AddLine(double x0, double y0, double x1, double y1, uint color, double alpha, double thickness)
    {
        double dx = x1 - x0;
        double dy = y1 - y0;
        double length = Math.Sqrt(dx * dx + dy * dy);

        if (length < .01)
        {
            return;
        }

        double nx = -dy / length * thickness / 2;
        double ny = dx / length * thickness / 2;

        AddQuad(new V2(x0 + nx, y0 + ny), new V2(x1 + nx, y1 + ny), new V2(x1 - nx, y1 - ny),
            new V2(x0 - nx, y0 - ny), color, alpha);
    }

    private void AddQuad(V2 a, V2 b, V2 c, V2 d, uint color, double alpha)
    {
        AddTriangle(a, b, c, color, alpha);
        AddTriangle(a, c, d, color, alpha);
    }

    private void AddTriangle(V2 a, V2 b, V2 c, uint color, double alpha)
    {
        AddVertex(a, color, alpha);
        AddVertex(b, color, alpha);
        AddVertex(c, color, alpha);
    }

    private void AddVertex(V2 point, uint color, double alpha)
    {
        if (floatCount + FloatsPerVertex > vertices.Length)
        {
            Array.Resize(ref vertices, vertices.Length * 2);
        }

        vertices[floatCount++] = (float)point.X;
        vertices[floatCount++] = (float)point.Y;
        vertices[floatCount++] = (byte)(color >> 16) / 255f;
        vertices[floatCount++] = (byte)(color >> 8) / 255f;
        vertices[floatCount++] = (byte)color / 255f;
        vertices[floatCount++] = (byte)(color >> 24) / 255f * (float)alpha;
    }

    private uint WaveGrade() => GameView.WaveGrades[Math.Max(0, game.Wave - 1) % GameView.WaveGrades.Length] switch
    {
        var color => 0xff000000u | (uint)color.R << 16 | (uint)color.G << 8 | color.B
    };

    private uint BackdropColor() => game.Mode is GameMode.Title or GameMode.Controls ? 0xff081a32 : WaveGrade();

    private static uint PlayerShotColor(int power) => power switch
    {
        1 => 0xff35eed2,
        2 => 0xffa774ff,
        3 => 0xffff68a8,
        _ => 0xff2fc9ff
    };

    private static uint BossColor(AlienBossKind kind) => kind switch
    {
        AlienBossKind.SludgeMaw => 0xff8fe84f,
        AlienBossKind.EyeTyrant => 0xffd976ff,
        AlienBossKind.BoneBroodmother => 0xffff8c4d,
        AlienBossKind.DreadHarvester => 0xffd5d94a,
        AlienBossKind.SolarWarden => 0xffffcf54,
        _ => 0xff56f1d2
    };

}
