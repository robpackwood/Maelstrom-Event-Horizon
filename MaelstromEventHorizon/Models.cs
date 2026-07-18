namespace MaelstromEventHorizon;

internal readonly record struct V2(double X, double Y)
{
    public static readonly V2 Zero = new(0, 0);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public double LengthSquared => X * X + Y * Y;
    public V2 Normalized => Length > .0001 ? this / Length : Zero;
    public static V2 FromAngle(double angle) => new(Math.Cos(angle), Math.Sin(angle));
    public static double Distance(V2 a, V2 b) => (a - b).Length;
    public static V2 operator +(V2 a, V2 b) => new(a.X + b.X, a.Y + b.Y);
    public static V2 operator -(V2 a, V2 b) => new(a.X - b.X, a.Y - b.Y);
    public static V2 operator -(V2 a) => new(-a.X, -a.Y);
    public static V2 operator *(V2 a, double n) => new(a.X * n, a.Y * n);
    public static V2 operator /(V2 a, double n) => new(a.X / n, a.Y / n);
}

internal abstract class Body(V2 position, V2 velocity, double radius)
{
    public V2 Position = position;
    public V2 Velocity = velocity;
    public double Radius = radius;
    public double Angle;
    public double Spin;
    public double Age;
    public bool Alive = true;
}

internal sealed class Ship : Body
{
    public const double VisualScale = 1.3;
    public Ship(V2 position) : base(position, V2.Zero, 22.1) { }
    public double Shield = 67;
    public double Invulnerable = 2.5;
    public double SpawnShieldTime;
    public bool Shielding;
    public bool Thrusting;
}

internal sealed class ShipDebris(V2 position, V2 velocity, int kind, double angle, double spin)
    : Body(position, velocity, 8)
{
    public int Kind = kind;
    public double Lifetime = 1.9;

    public ShipDebris Initialize()
    {
        Angle = angle;
        Spin = spin;
        return this;
    }
}

internal sealed class Asteroid : Body
{
    public Asteroid(V2 position, V2 velocity, int size, bool steel, int seed, bool exitsArena = false)
        : base(position, velocity, size switch { 3 => 35, 2 => 21, _ => 11 })
    {
        Size = size;
        Steel = steel;
        Seed = seed;
        ExitsArena = exitsArena;
        Spin = (seed % 2 == 0 ? 1 : -1) * (.18 + seed % 11 * .025);
        HitPoints = steel ? 7 : 1;
    }

    public int Size;
    public bool Steel;
    public int Seed;
    public int HitPoints;
    public bool ExitsArena;
}

internal enum FighterKind { Raider, Interceptor }

internal sealed class Fighter : Body
{
    public Fighter(V2 position, V2 velocity, FighterKind kind)
        : base(position, velocity, kind == FighterKind.Raider ? 27 : 18)
    {
        Kind = kind;
        HitPoints = kind == FighterKind.Raider ? 4 : 2;
        FireDelay = kind == FighterKind.Raider ? 2.1 : 1.45;
    }

    public FighterKind Kind;
    public int HitPoints;
    public double FireDelay;
}

internal sealed class HomingMine(V2 position, V2 velocity) : Body(position, velocity, 15)
{
    public int HitPoints = 2;
}

internal sealed class GravityVortex(V2 position) : Body(position, V2.Zero, 43)
{
    public double Lifetime = 14;
}

internal sealed class Nova(V2 position) : Body(position, V2.Zero, 22)
{
    public const double Fuse = 6.2;
    public bool Detonated;
}

internal enum PickupKind { Canister, Multiplier, Bonus, RescueShip }

internal sealed class Pickup(V2 position, V2 velocity, PickupKind kind, int value = 0)
    : Body(position, velocity, kind == PickupKind.RescueShip ? 22 : 15)
{
    public PickupKind Kind = kind;
    public int Value = value;
    public double Lifetime = kind == PickupKind.Canister ? 16 : kind == PickupKind.RescueShip ? 18 : 10;
}

internal sealed class Comet(V2 position, V2 velocity, int value, uint tint)
    : Body(position, velocity, 22)
{
    public const double TrailLength = 180;
    public int Value = value;
    public uint Tint = tint;
    public double Lifetime = 15;
}

internal sealed class FloatingText(V2 position, string text, uint color)
{
    public V2 Position = position;
    public string Text = text;
    public uint Color = color;
    public double Age;
    public double Lifetime = 1.65;
    public bool Alive => Age < Lifetime;
}

internal enum PowerupKind
{
    RapidFire, AirBrakes, Luck, TripleFire, LongRange, Shields, Freeze, SmartBomb
}

internal sealed class Shot(V2 position, V2 velocity, bool enemy, double lifetime)
    : Body(position, velocity, enemy ? 4 : 3)
{
    public bool Enemy = enemy;
    public double Lifetime = lifetime;
}

internal sealed class Particle(V2 position, V2 velocity, double lifetime, uint color, double size)
    : Body(position, velocity, size)
{
    public double Lifetime = lifetime;
    public uint Color = color;
    public double StartSize = size;
}

internal sealed class Shockwave(V2 position, double lifetime, uint color, double maxRadius)
    : Body(position, V2.Zero, 0)
{
    public double Lifetime = lifetime;
    public uint Color = color;
    public double MaxRadius = maxRadius;
}

internal sealed class Star(V2 position, double depth, double phase)
{
    public V2 Position = position;
    public double Depth = depth;
    public double Phase = phase;
}
