using MaelstromEventHorizon.Domain.Effects;
using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Application.Services.Gameplay;

internal sealed class PowerupService
{
    internal void UpdateDeathEffects(GameEngine game, double dt)
    {
        foreach (Particle particle in game.Particles)
        {
            particle.Age += dt;
            particle.Position += particle.Velocity * dt;
            particle.Velocity *= Math.Pow(.96, dt * 60);

            if (particle.Age >= particle.Lifetime)
            {
                particle.Alive = false;
            }
        }

        foreach (Shockwave ring in game.Shockwaves)
        {
            ring.Age += dt;

            if (ring.Age >= ring.Lifetime)
            {
                ring.Alive = false;
            }
        }

        foreach (FloatingText text in game.FloatingTexts)
        {
            text.Age += dt;
        }

        game.UpdateShipDebris(dt);
        game.Particles.RemoveAll(particle => !particle.Alive);
        game.Shockwaves.RemoveAll(ring => !ring.Alive);
        game.FloatingTexts.RemoveAll(text => !text.Alive);
    }

    internal void RespawnPlayer(GameEngine game)
    {
        game.CenterPlayerWithShield();
        game.Player.Shield = 67;
        ClearRespawnZone(game);
        game.ShowBanner("SHIP RESTORED", 1.4);
    }

    private static void ClearRespawnZone(GameEngine game)
    {
        for (int i = 0; i < game.Fighters.Count; i++)
        {
            Fighter fighter = game.Fighters[i];

            if (!fighter.Alive)
            {
                continue;
            }

            V2 away = game.ArenaDelta(game.Player.Position, fighter.Position);

            if (away.Length < 280)
            {
                away = away.LengthSquared < 1 ? game.RandomDirection() : away.Normalized;
                fighter.Position = game.Wrap(game.Player.Position + away * 330);
                fighter.Velocity = away * 135;
                fighter.FireDelay = Math.Max(fighter.FireDelay, 1.5);
            }
        }

        for (int i = 0; i < game.Bosses.Count; i++)
        {
            AlienBoss boss = game.Bosses[i];

            if (!boss.Alive)
            {
                continue;
            }

            V2 away = game.ArenaDelta(game.Player.Position, boss.Position);

            if (away.Length < 390)
            {
                away = away.LengthSquared < 1 ? game.RandomDirection() : away.Normalized;
                boss.Position = game.Wrap(game.Player.Position + away * 440);
                boss.Velocity = away * 110;
                boss.AttackTimer = Math.Max(boss.AttackTimer, 1.8);
            }
        }
    }

    internal void AwardCanister(GameEngine game)
    {
        if (HasEveryEquipablePowerup(game))
        {
            game.LastPowerupTime = 4;
            game.AwardImmediateScore(5_000, game.Player.Position);
            game.ShowBanner("ALL UPGRADES — $5,000 BONUS", 2.2);
            game.Audio.Play(SoundCue.Pickup, .9);
            return;
        }

        List<PowerupKind> available = [];

        foreach (PowerupKind candidate in Enum.GetValues<PowerupKind>())
        {
            if (CanAwardPowerup(game, candidate))
            {
                available.Add(candidate);
            }
        }

        if (available.Count == 0)
        {
            game.LastPowerupTime = 4;
            game.AwardImmediateScore(5_000, game.Player.Position);
            game.ShowBanner("$5,000 BONUS", 2.2);
            game.Audio.Play(SoundCue.Pickup, .9);
            return;
        }

        PowerupKind power = available[game.Random.Next(available.Count)];
        game.LastPowerupTime = 4;

        switch (power)
        {
            case PowerupKind.AirBrakes:
                game.AirBrakesActive = true;
                break;
            case PowerupKind.Luck:
                game.LuckActive = true;
                game.EnsureLuckyWaveEvents();
                break;
            case PowerupKind.TripleFire:
                game.TripleFireActive = true;
                break;
            case PowerupKind.RiftVolley:
                game.RiftVolleyActive = true;
                break;
            case PowerupKind.LongRange:
                game.LongRangeActive = true;
                break;
            case PowerupKind.Shields:
                game.Player.Shield = Math.Min(100, game.Player.Shield + 65);
                break;
            case PowerupKind.ReflectionShield:
                game.ReflectionShieldActive = true;
                break;
            case PowerupKind.Freeze:
                game.FreezeTime = 8;
                break;
            case PowerupKind.RicochetArena:
                game.RicochetArenaActive = true;
                break;
            case PowerupKind.GiantShip:
                game.Player.SetGiant(true);
                game.Player.Invulnerable = Math.Max(game.Player.Invulnerable, .65);
                game.SpawnShockwave(game.Player.Position, .72, 0xffffd85a, 145);
                game.Spark(game.Player.Position, 0xffffef9c, 24);
                break;
        }

        game.ShowBanner(PowerupAnnouncementText(power), 2.2);
        game.Audio.Play(SoundCue.Pickup, .9);

        if (power == PowerupKind.GiantShip)
        {
            game.Audio.Play(SoundCue.GiantGrow, .96);
        }
    }

    internal void AwardRarePowerup(GameEngine game, PickupKind kind)
    {
        if (kind == PickupKind.TimeFreeze)
        {
            game.FreezeTime = 8;
            game.ShowBanner("TIME FREEZE ACQUIRED", 2.2);
            game.Audio.Play(SoundCue.Pickup, .9);
            game.SpawnShockwave(game.Player.Position, .52, 0xff8eecff, 120);
            game.Spark(game.Player.Position, 0xffd7fbff, 16);
            return;
        }

        if (kind == PickupKind.SmartBomb)
        {
            SmartBomb(game);
            game.ShowBanner("SMART BOMB DETONATED", 2.2);
            game.Audio.Play(SoundCue.Pickup, .9);
            game.SpawnShockwave(game.Player.Position, .68, 0xffff674f, 155);
            game.Spark(game.Player.Position, 0xffffd49b, 22);
            return;
        }

        game.RicochetArenaActive = true;
        game.ShowBanner("RICOCHET ARENA ACQUIRED", 2.2);
        game.Audio.Play(SoundCue.Pickup, .9);
        game.SpawnShockwave(game.Player.Position, .52, 0xffd78cff, 120);
        game.Spark(game.Player.Position, 0xffffe7bb, 16);
    }

    private static string PowerupAnnouncementText(PowerupKind power)
    {
        return power switch
        {
            PowerupKind.Shields => "SHIELD ENERGY RESTORED",
            _ => $"{PowerupName(power)} ACQUIRED"
        };
    }

    private static string PowerupName(PowerupKind power)
    {
        return power switch
        {
            PowerupKind.AirBrakes => "AIR BRAKES",
            PowerupKind.Luck => "LUCK",
            PowerupKind.TripleFire => "TRIPLE FIRE",
            PowerupKind.RiftVolley => "RIFT VOLLEY",
            PowerupKind.LongRange => "LONG RANGE",
            PowerupKind.ReflectionShield => "REFLECTION SHIELD",
            PowerupKind.Freeze => "TIME FREEZE",
            PowerupKind.RicochetArena => "RICOCHET ARENA",
            PowerupKind.GiantShip => "GIANT SHIP",
            _ => "UPGRADE"
        };
    }

    private static bool CanAwardPowerup(GameEngine game, PowerupKind power)
    {
        return power switch
        {
            PowerupKind.AirBrakes => !game.AirBrakesActive,
            PowerupKind.Luck => !game.LuckActive,
            PowerupKind.TripleFire => !game.TripleFireActive,
            PowerupKind.RiftVolley => !game.RiftVolleyActive,
            PowerupKind.LongRange => !game.LongRangeActive,
            PowerupKind.Shields => game.Player.Shield < 100,
            PowerupKind.ReflectionShield => !game.ReflectionShieldActive,
            PowerupKind.GiantShip => !game.Player.Giant,
            _ => false
        };
    }

    private static bool HasEveryEquipablePowerup(GameEngine game)
    {
        return game.AirBrakesActive && game.LuckActive && game.TripleFireActive &&
               game.RiftVolleyActive && game.LongRangeActive && game.Player.Shield >= 100 &&
               game.ReflectionShieldActive && game.Player.Giant;
    }

    internal void ShrinkGiantShip(GameEngine game, V2 impactPosition)
    {
        game.Player.SetGiant(false);
        game.Player.Invulnerable = Math.Max(game.Player.Invulnerable, 1.2);
        game.Player.Velocity *= .58;
        game.SpawnShockwave(game.Player.Position, .7, 0xffffb44f, 155);
        game.SpawnShockwave(game.Player.Position, .44, 0xffffffff, 92);
        game.Spark(impactPosition, 0xffffe8a3, 28);
        game.SpawnFloatingText(game.Player.Position, "GIANT HULL ABSORBED HIT", 0xffffdc72);
        game.ShowBanner("GIANT SHIP SHRUNK - HULL INTACT", 2.1);
        game.Audio.Play(SoundCue.GiantShrink, .95);
    }

    internal void ClearEquippedPowerups(GameEngine game)
    {
        game.RestoreGiantShipAfterBonus = false;
        game.ReflectionShieldActive = false;
        game.AirBrakesActive = false;
        game.LuckActive = false;
        game.TripleFireActive = false;
        game.RiftVolleyActive = false;
        game.LongRangeActive = false;
        game.RicochetArenaActive = false;
        game.Player.SetGiant(false);
        game.FreezeTime = 0;
    }

    private void SmartBomb(GameEngine game)
    {
        List<Asteroid> fragments = [];

        foreach (Asteroid asteroid in game.Asteroids.Where(a => a.Alive).ToArray())
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
                    asteroid.Velocity * .45 + direction * game.Random.Next(105, 190), asteroid.Size - 1, false,
                    game.Random.Next()));
            }
        }

        game.Asteroids.AddRange(fragments);

        foreach (Fighter fighter in game.Fighters.Where(f => f.Alive).ToArray())
        {
            game.DestroyFighter(fighter);
        }

        foreach (HomingMine mine in game.Mines.Where(m => m.Alive).ToArray())
        {
            game.DestroyMine(mine);
        }

        foreach (AlienBoss boss in game.Bosses.Where(b => b.Alive).ToArray())
        {
            game.DamageBoss(boss, 4, boss.Position);
        }

        game.SpawnShockwave(game.Player.Position, 1.1, 0xffffffff, 900);
        game.Audio.Play(SoundCue.Nova);
    }

    internal void DetonateNova(GameEngine game, Nova nova)
    {
        nova.Detonated = true;
        nova.Alive = false;
        int asteroidCount = game.Asteroids.Count;

        for (int i = 0; i < asteroidCount; i++)
        {
            Asteroid asteroid = game.Asteroids[i];

            if (!asteroid.Alive)
            {
                continue;
            }

            if (asteroid.ExitsArena)
            {
                asteroid.Alive = false;
                game.AsteroidBreakup(asteroid.Position, 16, 0xffe8c17a);
                continue;
            }

            asteroid.Steel = false;
            asteroid.HitPoints = 1;
            game.HitAsteroid(asteroid);
        }

        for (int i = 0; i < game.Fighters.Count; i++)
        {
            if (game.Fighters[i].Alive)
            {
                game.DestroyFighter(game.Fighters[i]);
            }
        }

        for (int i = 0; i < game.Mines.Count; i++)
        {
            if (game.Mines[i].Alive)
            {
                game.DestroyMine(game.Mines[i]);
            }
        }

        game.BossInvulnerability = 0;

        for (int i = 0; i < game.Bosses.Count; i++)
        {
            if (game.Bosses[i].Alive)
            {
                game.DamageBoss(game.Bosses[i], game.Bosses[i].HitPoints, game.Bosses[i].Position);
            }
        }

        for (int i = 0; i < game.Shots.Count; i++)
        {
            if (game.Shots[i].Enemy)
            {
                game.Shots[i].Alive = false;
            }
        }

        game.SpawnShockwave(nova.Position, 1.45, 0xffffe8a0, 1250);
        game.Explosion(nova.Position, 80, 0xffffffff);
        game.TriggerScreenShake(1, 16);
        game.Audio.Play(SoundCue.ShipBlast);
        game.Audio.Play(SoundCue.Nova);
        game.ShowBanner("SUPERNOVA", 2.2);
    }

    internal void NeutralizeNova(GameEngine game, Nova nova)
    {
        nova.Detonated = true;
        nova.Alive = false;
        game.AddScore(500);
        game.Spark(nova.Position, 0xffa7efff, 16);
        game.SpawnShockwave(nova.Position, .42, 0xffa7efff, 68);
        game.ShowBanner("NOVA NEUTRALIZED", 1.8);
        game.Audio.Play(SoundCue.NovaHit);
        game.Audio.Play(SoundCue.Explosion, .9);
    }
}
