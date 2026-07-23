using MaelstromEventHorizon.Domain.Effects;
using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Application.Services.Gameplay;

internal sealed class PowerupService
{
    internal void UpdateDeathEffects(GameEngine game, double dt)
    {
        foreach (var particle in game.Particles)
        {
            particle.Age += dt;
            particle.Position += particle.Velocity * dt;
            particle.Velocity *= Math.Pow(.96, dt * 60);
            if (particle.Age >= particle.Lifetime) particle.Alive = false;
        }
        foreach (var ring in game.Shockwaves)
        {
            ring.Age += dt;
            if (ring.Age >= ring.Lifetime) ring.Alive = false;
        }
        foreach (var text in game.FloatingTexts) text.Age += dt;
        game.UpdateShipDebris(dt);
        game.Particles.RemoveAll(particle => !particle.Alive);
        game.Shockwaves.RemoveAll(ring => !ring.Alive);
        game.FloatingTexts.RemoveAll(text => !text.Alive);
    }

    internal void RespawnPlayer(GameEngine game)
    {
        game.CenterPlayerWithShield();
        game.Player.Shield = 67;
        game.ShowBanner("SHIP RESTORED", 1.4);
    }

    internal void AwardCanister(GameEngine game)
    {
        PowerupKind power = (PowerupKind)game.Random.Next(Enum.GetValues<PowerupKind>().Length);
        game.LastPowerupTime = 4;
        switch (power)
        {
            case PowerupKind.RapidFire:
                game.RapidFireActive = true;
                game.RapidFireRoundsFired = 0;
                game.RapidFireReload = 0;
                break;
            case PowerupKind.AirBrakes: game.AirBrakesActive = true; break;
            case PowerupKind.Luck:
                game.LuckActive = true;
                game.EnsureLuckyWaveEvents();
                break;
            case PowerupKind.TripleFire: game.TripleFireActive = true; break;
            case PowerupKind.LongRange: game.LongRangeActive = true; break;
            case PowerupKind.Shields: game.Player.Shield = Math.Min(100, game.Player.Shield + 65); break;
            case PowerupKind.Freeze: game.FreezeTime = 8; break;
            case PowerupKind.SmartBomb: SmartBomb(game); break;
            case PowerupKind.RetroVision: game.RetroVisionActive = true; break;
            case PowerupKind.RicochetArena: game.RicochetArenaActive = true; break;
            case PowerupKind.GiantShip:
                game.Player.SetGiant(true);
                game.Player.Invulnerable = Math.Max(game.Player.Invulnerable, .65);
                game.Shockwaves.Add(new Shockwave(game.Player.Position, .72, 0xffffd85a, 145));
                game.Spark(game.Player.Position, 0xffffef9c, 24);
                break;
        }
        game.ShowBanner(game.PowerName(power), 2.2);
        game.Audio.Play(power == PowerupKind.GiantShip ? SoundCue.GiantGrow : SoundCue.ChaChing, .9);
    }

    internal void ShrinkGiantShip(GameEngine game, V2 impactPosition)
    {
        game.Player.SetGiant(false);
        game.Player.Invulnerable = Math.Max(game.Player.Invulnerable, 1.2);
        game.Player.Velocity *= .58;
        game.Shockwaves.Add(new Shockwave(game.Player.Position, .7, 0xffffb44f, 155));
        game.Shockwaves.Add(new Shockwave(game.Player.Position, .44, 0xffffffff, 92));
        game.Spark(impactPosition, 0xffffe8a3, 28);
        game.FloatingTexts.Add(new FloatingText(game.Player.Position, "GIANT HULL ABSORBED HIT", 0xffffdc72));
        game.ShowBanner("GIANT SHIP SHRUNK - HULL INTACT", 2.1);
        game.Audio.Play(SoundCue.GiantShrink, .95);
    }

    internal void ClearEquippedPowerups(GameEngine game)
    {
        game.RapidFireActive = false;
        game.RapidFireRoundsFired = 0;
        game.RapidFireReload = 0;
        game.AirBrakesActive = false;
        game.LuckActive = false;
        game.TripleFireActive = false;
        game.LongRangeActive = false;
        game.RetroVisionActive = false;
        game.RicochetArenaActive = false;
        game.Player.SetGiant(false);
        game.FreezeTime = 0;
    }

    private void SmartBomb(GameEngine game)
    {
        var fragments = new List<Asteroid>();
        foreach (var asteroid in game.Asteroids.Where(a => a.Alive).ToArray())
        {
            if (asteroid.Steel)
            {
                asteroid.HitPoints = Math.Max(1, asteroid.HitPoints - 3);
                game.Spark(asteroid.Position, 0xffd9f7ff, 12);
                continue;
            }
            if (asteroid.Size <= 1)
            {
                asteroid.Velocity += game.RandomDirection() * 85;
                continue;
            }

            asteroid.Alive = false;
            game.AddScore(asteroid.Size switch { 3 => 20, 2 => 50, _ => 100 });
            game.Explosion(asteroid.Position, 12, 0xffffbd5a);
            int fragmentCount = game.RollAsteroidFragmentCount();
            for (int i = 0; i < fragmentCount; i++)
            {
                V2 direction = game.RandomDirection();
                fragments.Add(new Asteroid(asteroid.Position + direction * 8,
                    asteroid.Velocity * .45 + direction * game.Random.Next(105, 190), asteroid.Size - 1, false, game.Random.Next()));
            }
        }
        game.Asteroids.AddRange(fragments);
        foreach (var fighter in game.Fighters.Where(f => f.Alive).ToArray()) game.DestroyFighter(fighter);
        foreach (var mine in game.Mines.Where(m => m.Alive).ToArray()) game.DestroyMine(mine);
        foreach (var boss in game.Bosses.Where(b => b.Alive).ToArray()) game.DamageBoss(boss, 4, boss.Position);
        game.Shockwaves.Add(new Shockwave(game.Player.Position, 1.1, 0xffffffff, 900));
        game.Audio.Play(SoundCue.Nova);
    }

    internal void DetonateNova(GameEngine game, Nova nova)
    {
        nova.Detonated = true;
        nova.Alive = false;
        foreach (var asteroid in game.Asteroids.Where(a => a is { Alive: true, Steel: false }).ToArray()) game.HitAsteroid(asteroid);
        foreach (var fighter in game.Fighters.Where(f => f.Alive).ToArray()) game.DestroyFighter(fighter);
        foreach (var mine in game.Mines.Where(m => m.Alive).ToArray()) game.DestroyMine(mine);
        foreach (var boss in game.Bosses.Where(b => b.Alive).ToArray()) game.DamageBoss(boss, 6, boss.Position);
        game.Shockwaves.Add(new Shockwave(nova.Position, 1.45, 0xffffe8a0, 1250));
        game.Explosion(nova.Position, 80, 0xffffffff);
        game.TriggerScreenShake(.9, 14);
        if (!game.Player.Shielding) game.DamagePlayer();
        game.Audio.Play(SoundCue.Nova);
        game.ShowBanner("SUPERNOVA", 2.2);
    }

    internal void NeutralizeNova(GameEngine game, Nova nova)
    {
        nova.Detonated = true;
        nova.Alive = false;
        game.AddScore(500);
        game.Spark(nova.Position, 0xffa7efff, 16);
        game.Shockwaves.Add(new Shockwave(nova.Position, .42, 0xffa7efff, 68));
        game.ShowBanner("NOVA NEUTRALIZED", 1.8);
        game.Audio.Play(SoundCue.Pickup, .58);
    }
}
