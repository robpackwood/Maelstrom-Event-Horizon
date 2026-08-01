using MaelstromEventHorizon.Domain.Effects;
using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Application.Services.Gameplay;

internal sealed class EffectsPhysicsService
{
    private const int MaxParticles = 900;
    private const int MaxShockwaves = 24;
    private const int MaxDebris = 32;
    private const int MaxShots = 240;
    private const int MaxFloatingTexts = 24;
    internal void SpawnShipWreck(GameEngine game)
    {
        V2[] offsets = [new(15, 0), new(-4, -10), new(-4, 10), new(-12, 0), new(3, 0)];

        for (int i = 0; i < offsets.Length; i++)
        {
            V2 outward = Rotate(offsets[i], game.Player.Angle);

            V2 velocity = game.Player.Velocity * .32 + outward.Normalized * game.Random.Next(85, 185) +
                          RandomDirection(game) * game.Random.Next(20, 75);

            double spin = (game.Random.NextDouble() * 2 - 1) * (2.8 + game.Random.NextDouble() * 5.2);

            ShipDebris piece = game.SpawnShipDebris(
                game.Player.Position + outward * game.Player.VisualScale, velocity, i, game.Player.Angle, spin);

            piece.Position = MoveBody(game, piece, piece.Position, false);
        }
    }

    internal void UpdateShipDebris(GameEngine game, double dt)
    {
        foreach (ShipDebris piece in game.ShipDebrisPieces)
        {
            piece.Age += dt;
            piece.Angle += piece.Spin * dt;
            piece.Position = MoveBody(game, piece, piece.Position + piece.Velocity * dt);
            piece.Velocity *= Math.Pow(.994, dt * 60);

            if (piece.Age >= piece.Lifetime)
            {
                piece.Alive = false;
            }
        }

        game.RecycleShipDebris();
    }

    internal void EmitThrust(GameEngine game)
    {
        V2 back = -V2.FromAngle(game.Player.Angle);

        for (int i = 0; i < 2; i++)
        {
            AddParticle(game, 
                game.Player.Position + back * (18 * game.Player.VisualScale) + RandomDirection(game) * 2,
                game.Player.Velocity * .2 + back * game.Random.Next(120, 260) + RandomDirection(game) * 25,
                .28 + game.Random.NextDouble() * .25, i == 0 ? 0xff5be8ff : 0xffff7b45, game.Random.Next(2, 6));
        }
    }

    internal void Spark(GameEngine game, V2 position, uint color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            AddParticle(game, position, RandomDirection(game) * game.Random.Next(70, 260),
                .2 + game.Random.NextDouble() * .45, color, 2 + game.Random.NextDouble() * 4);
        }
    }

    internal void Explosion(GameEngine game, V2 position, int count, uint color)
    {
        for (int i = 0; i < count; i++)
        {
            double speed = game.Random.NextDouble() * game.Random.Next(100, 390);
            uint c = i % 4 == 0 ? 0xffffffff : i % 3 == 0 ? 0xffff5a36 : color;

            AddParticle(game, position, RandomDirection(game) * speed,
                .35 + game.Random.NextDouble() * .85, c, 2 + game.Random.NextDouble() * 7);
        }

        AddShockwave(game, position, .38, color, 55 + count * 1.7);
    }

    internal void AsteroidBreakup(GameEngine game, V2 position, int count, uint color)
    {
        for (int i = 0; i < count; i++)
        {
            double speed = game.Random.Next(150, 410);
            uint fragmentColor = i % 5 == 0 ? 0xffffe1a4 : i % 3 == 0 ? 0xffd77542 : color;
            AddParticle(game, position, RandomDirection(game) * speed,
                .16 + game.Random.NextDouble() * .32, fragmentColor, 1.5 + game.Random.NextDouble() * 4.5);
        }

        AddShockwave(game, position, .22, color, 38 + count * 1.15);
    }

    private static void AddParticle(GameEngine game, V2 position, V2 velocity, double lifetime, uint color, double size)
    {
        if (game.Particles.Count >= MaxParticles) game.Particles[0].Alive = false;
        game.SpawnParticle(position, velocity, lifetime, color, size);
    }

    private static void AddShockwave(GameEngine game, V2 position, double lifetime, uint color, double maxRadius)
    {
        if (game.Shockwaves.Count >= MaxShockwaves) game.Shockwaves[0].Alive = false;
        game.SpawnShockwave(position, lifetime, color, maxRadius);
    }

    internal void RemoveDead(GameEngine game)
    {
        game.Asteroids.RemoveAll(x => !x.Alive);
        game.Fighters.RemoveAll(x => !x.Alive);
        game.Bosses.RemoveAll(x => !x.Alive);
        game.Mines.RemoveAll(x => !x.Alive);
        game.Vortices.RemoveAll(x => !x.Alive);
        game.Novas.RemoveAll(x => !x.Alive);
        game.Pickups.RemoveAll(x => !x.Alive);
        game.Comets.RemoveAll(x => !x.Alive);
        game.RecycleEffects();
        game.RecycleShipDebris();
        TrimOldest(game.Shots, MaxShots);
        TrimOldest(game.FloatingTexts, MaxFloatingTexts);
    }

    private static void TrimOldest<T>(List<T> items, int maximum)
    {
        int excess = items.Count - maximum;
        if (excess > 0) items.RemoveRange(0, excess);
    }

    internal void ClearWorld(GameEngine game)
    {
        game.Asteroids.Clear();
        game.Fighters.Clear();
        game.Bosses.Clear();
        game.Mines.Clear();
        game.Vortices.Clear();
        game.Novas.Clear();
        game.Pickups.Clear();
        game.Comets.Clear();
        game.RecycleAllEffects();
        game.ShipDebrisPieces.Clear();
        game.FreezeTime = 0;
        game.ScreenShakeTime = 0;
        game.ScreenShakeDuration = 0;
        game.ScreenShakeMagnitude = 0;
        game.IsBonusStage = false;
        game.IsBossStage = false;
        game.BonusStageFailed = false;
        game.RespawnTimer = 0;
        game.FighterSpawnedThisWave = false;
        game.BonusTravelTime = 0;
        game.BonusAsteroidTotal = 0;
        game.BonusAsteroidsDodged = 0;
        game.BonusAsteroidsRemaining = 0;
        game.ClearEquippedPowerups();
    }

    internal void TriggerScreenShake(GameEngine game, double duration, double magnitude)
    {
        game.ScreenShakeDuration = Math.Max(game.ScreenShakeDuration, duration);
        game.ScreenShakeTime = Math.Max(game.ScreenShakeTime, duration);
        game.ScreenShakeMagnitude = Math.Max(game.ScreenShakeMagnitude, magnitude);
    }

    internal V2 SafeEdgePosition(GameEngine game)
    {
        return game.Random.Next(4) switch
        {
            0 => new V2(game.Random.NextDouble() * GameEngine.Width, 35),
            1 => new V2(GameEngine.Width - 35, game.Random.NextDouble() * GameEngine.Height),
            2 => new V2(game.Random.NextDouble() * GameEngine.Width, GameEngine.Height - 35),
            _ => new V2(35, game.Random.NextDouble() * GameEngine.Height)
        };
    }

    internal V2 SafePosition(GameEngine game, double distance)
    {
        for (int i = 0; i < 50; i++)
        {
            V2 point = new(90 + game.Random.NextDouble() * (GameEngine.Width - 180),
                90 + game.Random.NextDouble() * (GameEngine.Height - 180));

            if (V2.Distance(point, game.Player.Position) >= distance)
            {
                return point;
            }
        }

        return new V2(100, 100);
    }

    internal V2 RandomDirection(GameEngine game)
    {
        double a = game.Random.NextDouble() * Math.PI * 2;
        return V2.FromAngle(a);
    }

    internal bool Touching(GameEngine game, Body a, Body b)
    {
        bool onePassAsteroid = a is Asteroid { ExitsArena: true } || b is Asteroid { ExitsArena: true };
        V2 delta = onePassAsteroid ? b.Position - a.Position : ArenaDelta(game, a.Position, b.Position);
        return delta.LengthSquared < Math.Pow(a.Radius + b.Radius, 2);
    }

    internal bool TouchingComet(GameEngine game, Shot shot, Comet comet)
    {
        V2 fromHead = ArenaDelta(game, comet.Position, shot.Position);

        if (fromHead.LengthSquared < Math.Pow(comet.Radius + shot.Radius, 2))
        {
            return true;
        }

        V2 back = -comet.Velocity.Normalized;
        double along = fromHead.X * back.X + fromHead.Y * back.Y;

        if (along < 0 || along > Comet.TrailLength)
        {
            return false;
        }

        double perpendicular = Math.Abs(fromHead.X * back.Y - fromHead.Y * back.X);
        double taper = along / Comet.TrailLength;
        double trailRadius = 13 * (1 - taper) + 2.5 * taper;
        return perpendicular <= trailRadius + shot.Radius;
    }

    internal V2 MoveBody(GameEngine game, Body body, V2 nextPosition, bool wrapNormally = true)
    {
        if (body is Asteroid { ExitsArena: true })
        {
            return nextPosition;
        }

        if (!game.RicochetArenaActive)
        {
            return wrapNormally ? Wrap(nextPosition) : nextPosition;
        }

        double minX = GameEngine.ArenaWallInset + body.Radius;
        double maxX = GameEngine.Width - GameEngine.ArenaWallInset - body.Radius;
        double minY = GameEngine.ArenaWallInset + body.Radius;
        double maxY = GameEngine.Height - GameEngine.ArenaWallInset - body.Radius;
        double x = nextPosition.X;
        double y = nextPosition.Y;
        double vx = body.Velocity.X;
        double vy = body.Velocity.Y;
        bool bounced = false;

        if (x < minX)
        {
            x = minX + (minX - x);
            vx = Math.Abs(vx);
            bounced = true;
        }
        else if (x > maxX)
        {
            x = maxX - (x - maxX);
            vx = -Math.Abs(vx);
            bounced = true;
        }

        if (y < minY)
        {
            y = minY + (minY - y);
            vy = Math.Abs(vy);
            bounced = true;
        }
        else if (y > maxY)
        {
            y = maxY - (y - maxY);
            vy = -Math.Abs(vy);
            bounced = true;
        }

        body.Velocity = new V2(vx, vy);
        if (bounced && game.RicochetBounceSoundCooldown <= 0)
        {
            game.RicochetBounceSoundCooldown = .075;
            game.Audio.Play(SoundCue.RicochetBounce, body is Shot ? .36 : .6);
        }
        return new V2(Math.Clamp(x, minX, maxX), Math.Clamp(y, minY, maxY));
    }

    internal V2 ArenaDelta(GameEngine game, V2 from, V2 to) =>
        game.RicochetArenaActive ? to - from : WrappedDelta(from, to);

    internal V2 Wrap(V2 p) => new((p.X % GameEngine.Width + GameEngine.Width) % GameEngine.Width,
        (p.Y % GameEngine.Height + GameEngine.Height) % GameEngine.Height);

    private V2 WrappedDelta(V2 from, V2 to)
    {
        double x = to.X - from.X;
        double y = to.Y - from.Y;

        if (x > GameEngine.Width / 2)
        {
            x -= GameEngine.Width;
        }
        else if (x < -GameEngine.Width / 2)
        {
            x += GameEngine.Width;
        }

        if (y > GameEngine.Height / 2)
        {
            y -= GameEngine.Height;
        }
        else if (y < -GameEngine.Height / 2)
        {
            y += GameEngine.Height;
        }

        return new V2(x, y);
    }

    internal V2 PredictAim(GameEngine game, V2 origin, V2 target, V2 targetVelocity, double projectileSpeed)
    {
        V2 delta = ArenaDelta(game, origin, target);
        double lead = Math.Min(1.1, delta.Length / projectileSpeed);
        return (delta + targetVelocity * lead).Normalized;
    }

    internal V2 Rotate(V2 vector, double angle)
    {
        double c = Math.Cos(angle);
        double s = Math.Sin(angle);
        return new V2(vector.X * c - vector.Y * s, vector.X * s + vector.Y * c);
    }

    internal void ShowBanner(GameEngine game, string text, double duration)
    {
        game.Banner = text;
        game.BannerTime = duration;
    }

    internal string BossName(AlienBossKind kind) => kind switch
    {
        AlienBossKind.SludgeMaw => "THE SLUDGE MAW",
        AlienBossKind.EyeTyrant => "THE EYE TYRANT",
        AlienBossKind.BoneBroodmother => "THE BONE BROODMOTHER",
        AlienBossKind.VoidLeech => "THE VOID LEECH",
        AlienBossKind.DreadHarvester => "THE DREAD HARVESTER",
        AlienBossKind.SolarWarden => "THE SOLAR WARDEN",
        _ => "ALIEN ABOMINATION"
    };

    internal uint BossTint(AlienBossKind kind) => kind switch
    {
        AlienBossKind.SludgeMaw => 0xff8fe84f,
        AlienBossKind.EyeTyrant => 0xffd976ff,
        AlienBossKind.BoneBroodmother => 0xffff8c4d,
        AlienBossKind.DreadHarvester => 0xffd5d94a,
        AlienBossKind.SolarWarden => 0xffffcf54,
        _ => 0xff56f1d2
    };

    internal string PowerName(PowerupKind kind) => kind switch
    {
        PowerupKind.RapidFire => "RAPID FIRE",
        PowerupKind.AirBrakes => "AIR BRAKES",
        PowerupKind.Luck => "LUCK OF THE IRISH",
        PowerupKind.TripleFire => "TRIPLE FIRE",
        PowerupKind.RiftVolley => "RIFT VOLLEY",
        PowerupKind.LongRange => "LONG RANGE",
        PowerupKind.Shields => "SHIELD ENERGY",
        PowerupKind.Freeze => "TIME FREEZE",
        PowerupKind.SmartBomb => "SMART BOMB",
        PowerupKind.RicochetArena => "RICOCHET ARENA",
        PowerupKind.GiantShip => "GIANT SHIP",
        _ => kind.ToString().ToUpperInvariant()
    };
}
