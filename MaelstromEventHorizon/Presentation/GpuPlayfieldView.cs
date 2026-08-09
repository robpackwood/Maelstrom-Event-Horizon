using OpenTK.Graphics.OpenGL4;
using OpenTK.Wpf;
using MaelstromEventHorizon.Application;
using MaelstromEventHorizon.Domain.Effects;
using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Presentation;

/// <summary>GPU-backed playfield. WPF continues to draw menus and the HUD above this control.</summary>
internal sealed class GpuPlayfieldView : GLWpfControl
{
    private const int FloatsPerVertex = 6;
    private readonly GameEngine game;
    private float[] vertices = new float[16_384];
    private int floatCount;
    private int program;
    private int vertexArray;
    private int vertexBuffer;
    private int gpuBufferCapacity;
    private int resolutionUniform;
    private bool initialized;
    private bool unavailable;

    internal event Action<Exception>? Failed;

    public GpuPlayfieldView(GameEngine game)
    {
        this.game = game;
        Focusable = false;
        IsHitTestVisible = false;
        Ready += InitializeRenderer;
        Render += RenderFrame;
        Loaded += StartRenderer;
    }

    private void StartRenderer(object? sender, EventArgs e)
    {
        try
        {
            Start(new GLWpfControlSettings { MajorVersion = 3, MinorVersion = 3 });
            RenderContinuously = true;
        }
        catch (Exception error)
        {
            ReportFailure(error);
        }
    }

    private void InitializeRenderer()
    {
        try
        {
            program = CreateProgram();
            resolutionUniform = GL.GetUniformLocation(program, "uResolution");
            vertexArray = GL.GenVertexArray();
            vertexBuffer = GL.GenBuffer();
            GL.BindVertexArray(vertexArray);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBuffer);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), IntPtr.Zero, BufferUsageHint.DynamicDraw);
            gpuBufferCapacity = vertices.Length;
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, FloatsPerVertex * sizeof(float), 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, FloatsPerVertex * sizeof(float),
                2 * sizeof(float));
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            initialized = true;
        }
        catch (Exception error)
        {
            ReportFailure(error);
        }
    }

    private void RenderFrame(TimeSpan _)
    {
        if (!initialized || unavailable)
        {
            return;
        }

        try
        {
            int surfaceWidth = Math.Max(1, FrameBufferWidth);
            int surfaceHeight = Math.Max(1, FrameBufferHeight);
            GL.Viewport(0, 0, surfaceWidth, surfaceHeight);
            GL.ClearColor(0.001f, 0.005f, 0.015f, 1);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            double scale = Math.Min(surfaceWidth / GameEngine.Width, surfaceHeight / GameEngine.Height);
            int playfieldWidth = Math.Max(1, (int)Math.Round(GameEngine.Width * scale));
            int playfieldHeight = Math.Max(1, (int)Math.Round(GameEngine.Height * scale));
            GL.Viewport((surfaceWidth - playfieldWidth) / 2, (surfaceHeight - playfieldHeight) / 2, playfieldWidth,
                playfieldHeight);

            floatCount = 0;
            DrawPlayfield();
            GL.UseProgram(program);
            GL.Uniform2(resolutionUniform, (float)GameEngine.Width, (float)GameEngine.Height);
            GL.BindVertexArray(vertexArray);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBuffer);

            if (floatCount > gpuBufferCapacity)
            {
                gpuBufferCapacity = vertices.Length;
                GL.BufferData(BufferTarget.ArrayBuffer, gpuBufferCapacity * sizeof(float), IntPtr.Zero,
                    BufferUsageHint.DynamicDraw);
            }

            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, floatCount * sizeof(float), vertices);
            GL.DrawArrays(PrimitiveType.Triangles, 0, floatCount / FloatsPerVertex);
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

        try
        {
            RenderContinuously = false;
        }
        catch (Exception)
        {
            // Starting the graphics host can fail before it creates its render loop.
        }

        Failed?.Invoke(error);
    }

    private void DrawPlayfield()
    {
        uint grade = game.Mode is GameMode.Title or GameMode.Controls ? 0xff081a32 : WaveGrade();
        AddRect(0, 0, GameEngine.Width, GameEngine.Height, grade);
        DrawStars();

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

    private void DrawStars()
    {
        foreach (Star star in game.Stars)
        {
            double twinkle = .55 + .45 * Math.Sin(game.TotalTime * (1.2 + star.Depth * 2.5) + star.Phase);
            double size = .7 + star.Depth * 1.6 + twinkle * .45;
            AddCircle(star.Position.X, star.Position.Y, size, 0xffb9ddff, .38 + twinkle * .58, 6);
        }
    }

    private void DrawAsteroids(V2 shake)
    {
        foreach (Asteroid asteroid in game.Asteroids)
        {
            uint color = asteroid.Steel ? 0xff8faec2 : asteroid.Mega ? 0xffff704d : 0xff98796b;
            AddCircle(asteroid.Position.X + shake.X, asteroid.Position.Y + shake.Y, asteroid.Radius, color, .96, 12);
            AddRing(asteroid.Position.X + shake.X, asteroid.Position.Y + shake.Y, asteroid.Radius * .88,
                asteroid.Steel ? 0xffd8f5ff : 0xffd6ad8c, .7, 12, 1.2);
        }
    }

    private void DrawFighters(V2 shake)
    {
        foreach (Fighter fighter in game.Fighters)
        {
            uint color = fighter.Kind == FighterKind.Interceptor ? 0xff5eeaff : 0xffff4f85;
            AddShip(fighter.Position + shake, fighter.Angle, fighter.Radius * 1.1, color, .95);
        }
    }

    private void DrawBosses(V2 shake)
    {
        foreach (AlienBoss boss in game.Bosses)
        {
            uint color = BossColor(boss.Kind);
            V2 position = boss.Position + shake;
            AddCircle(position.X, position.Y, boss.Radius, color, .94, 18);
            AddRing(position.X, position.Y, boss.Radius * .88, 0xffefffff, .58, 18, 1.5);
            AddRing(position.X, position.Y, boss.Radius * 1.08, color, .22, 18, 3);
        }
    }

    private void DrawMines(V2 shake)
    {
        foreach (HomingMine mine in game.Mines)
        {
            V2 position = mine.Position + shake;
            AddCircle(position.X, position.Y, mine.Radius, 0xffffd84e, .95, 10);
            AddRing(position.X, position.Y, mine.Radius * 1.24, 0xffff643d, .62, 10, 1.4);
        }
    }

    private void DrawVortices(V2 shake)
    {
        foreach (GravityVortex vortex in game.Vortices)
        {
            V2 position = vortex.Position + shake;
            AddCircle(position.X, position.Y, vortex.Radius, 0xff5b1e80, .7, 16);
            AddRing(position.X, position.Y, vortex.Radius * 1.15, 0xffcc8cff, .7, 16, 2);
        }
    }

    private void DrawNovas(V2 shake)
    {
        foreach (Nova nova in game.Novas)
        {
            V2 position = nova.Position + shake;
            AddCircle(position.X, position.Y, nova.Radius * 1.45, 0xffff613c, .85, 12);
            AddCircle(position.X, position.Y, nova.Radius * .55, 0xffffefa7, 1, 10);
        }
    }

    private void DrawComets(V2 shake)
    {
        foreach (Comet comet in game.Comets)
        {
            V2 position = comet.Position + shake;
            V2 tail = position - comet.Velocity.Normalized * Comet.TrailLength;
            AddLine(tail.X, tail.Y, position.X, position.Y, 0xffa7e8ff, .42, 7);
            AddCircle(position.X, position.Y, comet.Radius, comet.Tint, .98, 12);
        }
    }

    private void DrawPickups(V2 shake)
    {
        foreach (Pickup pickup in game.Pickups)
        {
            V2 position = pickup.Position + shake;
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
            AddRing(position.X, position.Y, pickup.Radius * 1.5, color, .36, 8, 1);
        }
    }

    private void DrawShots(V2 shake)
    {
        foreach (Shot shot in game.Shots)
        {
            V2 position = shot.Position + shake;
            uint color = shot.BossShot ? shot.Tint : shot.Enemy ? 0xff5877ff : PlayerShotColor(shot.PowerLevel);

            if (shot.Laser)
            {
                V2 tail = position - shot.Velocity.Normalized * 30;
                AddLine(tail.X, tail.Y, position.X, position.Y, color, .62, 5);
            }

            AddCircle(position.X, position.Y, shot.BossShot ? Math.Max(6, shot.Radius) : Math.Max(3.8, shot.Radius),
                color, .96, 8);
        }
    }

    private void DrawShip(V2 shake)
    {
        Ship ship = game.Player;

        if (game.PlayerRespawning || game.Mode is GameMode.Paused or GameMode.WaveSummary or GameMode.WaveSummaryExit)
        {
            return;
        }

        V2 position = ship.Position + shake;

        if (ship.Thrusting)
        {
            V2 tail = position - V2.FromAngle(ship.Angle) * 45 * ship.VisualScale;
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
            AddCircle(particle.Position.X + shake.X, particle.Position.Y + shake.Y,
                Math.Max(1, particle.StartSize * (.3 + life * .7)), particle.Color, life, 6);
        }
    }

    private void DrawShockwaves(V2 shake)
    {
        foreach (Shockwave ring in game.Shockwaves)
        {
            double progress = ring.Age / ring.Lifetime;
            AddRing(ring.Position.X + shake.X, ring.Position.Y + shake.Y, ring.MaxRadius * (1 - Math.Pow(1 - progress, 3)),
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
        for (int i = 0; i < sides; i++)
        {
            double first = i * Math.PI * 2 / sides;
            double second = (i + 1) * Math.PI * 2 / sides;
            AddTriangle(new V2(x, y), new V2(x + Math.Cos(first) * radius, y + Math.Sin(first) * radius),
                new V2(x + Math.Cos(second) * radius, y + Math.Sin(second) * radius), color, alpha);
        }
    }

    private void AddRing(double x, double y, double radius, uint color, double alpha, int sides, double thickness)
    {
        for (int i = 0; i < sides; i++)
        {
            double first = i * Math.PI * 2 / sides;
            double second = (i + 1) * Math.PI * 2 / sides;
            AddLine(x + Math.Cos(first) * radius, y + Math.Sin(first) * radius,
                x + Math.Cos(second) * radius, y + Math.Sin(second) * radius, color, alpha, thickness);
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

    private void AddRect(double x, double y, double width, double height, uint color)
    {
        AddQuad(new V2(x, y), new V2(x + width, y), new V2(x + width, y + height), new V2(x, y + height), color, 1);
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

    private static int CreateProgram()
    {
        const string vertexSource = "#version 330 core\nlayout(location=0) in vec2 aPosition;\nlayout(location=1) in vec4 aColor;\nuniform vec2 uResolution;\nout vec4 vColor;\nvoid main(){ vec2 n=(aPosition/uResolution)*2.0-1.0; gl_Position=vec4(n.x,-n.y,0,1); vColor=aColor; }";
        const string fragmentSource = "#version 330 core\nin vec4 vColor;\nout vec4 outputColor;\nvoid main(){ outputColor=vColor; }";
        int vertex = CompileShader(ShaderType.VertexShader, vertexSource);
        int fragment = CompileShader(ShaderType.FragmentShader, fragmentSource);
        int result = GL.CreateProgram();
        GL.AttachShader(result, vertex);
        GL.AttachShader(result, fragment);
        GL.LinkProgram(result);
        GL.GetProgram(result, GetProgramParameterName.LinkStatus, out int linked);

        if (linked == 0)
        {
            throw new InvalidOperationException(GL.GetProgramInfoLog(result));
        }

        GL.DeleteShader(vertex);
        GL.DeleteShader(fragment);
        return result;
    }

    private static int CompileShader(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int compiled);

        if (compiled == 0)
        {
            throw new InvalidOperationException(GL.GetShaderInfoLog(shader));
        }

        return shader;
    }
}
