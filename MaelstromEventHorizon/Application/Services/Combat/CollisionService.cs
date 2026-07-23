using MaelstromEventHorizon.Domain.Effects;
using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Application.Services.Combat;

internal sealed class CollisionService
{
    internal void HandleCollisions(GameEngine game)
    {
        foreach (Shot shot in game.Shots.Where(s => s is { Alive: true, Enemy: false }).ToArray())
        {
            Asteroid? asteroid = game.Asteroids.FirstOrDefault(a => a.Alive && game.Touching(shot, a));

            if (asteroid is not null)
            {
                V2 hitPosition = shot.Position;
                shot.Alive = false;
                game.AwardImmediateScore(100, hitPosition);
                HitAsteroid(game, asteroid, shot.Damage);
                continue;
            }

            Fighter? fighter = game.Fighters.FirstOrDefault(f => f.Alive && game.Touching(shot, f));

            if (fighter is not null)
            {
                shot.Alive = false;

                if ((fighter.HitPoints -= shot.Damage) <= 0)
                {
                    DestroyFighter(game, fighter);
                }
                else
                {
                    game.Spark(fighter.Position, 0xfff0a060, 4 + shot.Damage * 2);
                }

                continue;
            }

            AlienBoss? boss = game.Bosses.FirstOrDefault(b => b.Alive && game.Touching(shot, b));

            if (boss is not null)
            {
                shot.Alive = false;
                DamageBoss(game, boss, shot.Damage, shot.Position);
                continue;
            }

            HomingMine? mine = game.Mines.FirstOrDefault(m => m.Alive && game.Touching(shot, m));

            if (mine is not null)
            {
                shot.Alive = false;

                if ((mine.HitPoints -= shot.Damage) <= 0)
                {
                    DestroyMine(game, mine);
                }

                continue;
            }

            GravityVortex? vortex = game.Vortices.FirstOrDefault(v => v.Alive && game.Touching(shot, v));

            if (vortex is not null)
            {
                shot.Alive = false;
                DestroyVortex(game, vortex);
                continue;
            }

            Nova? nova = game.Novas.FirstOrDefault(
                n => n is { Alive: true, Detonated: false } && game.Touching(shot, n));

            if (nova is not null)
            {
                shot.Alive = false;
                game.NeutralizeNova(nova);
                continue;
            }

            Comet? comet = game.Comets.FirstOrDefault(c => c.Alive && game.TouchingComet(shot, c));

            if (comet is not null)
            {
                V2 hitPosition = shot.Position;
                shot.Alive = false;
                comet.Alive = false;
                game.AddCometCash(comet.Value);
                game.Explosion(hitPosition, 28, comet.Tint);
                game.Shockwaves.Add(new Shockwave(hitPosition, .55, comet.Tint, 125));
                game.FloatingTexts.Add(new FloatingText(hitPosition, $"+${comet.Value:N0}", comet.Tint));
                game.Audio.Play(SoundCue.CometCelebration, .9);
                continue;
            }

            Pickup? prize = game.Pickups.FirstOrDefault(p =>
                p is { Alive: true, Kind: PickupKind.Multiplier or PickupKind.Bonus } && game.Touching(shot, p));

            if (prize is not null)
            {
                shot.Alive = false;
                prize.Alive = false;

                if (prize.Kind == PickupKind.Multiplier)
                {
                    game.Multiplier = Math.Max(game.Multiplier, prize.Value);
                    game.ShowBanner($"{prize.Value}X MULTIPLIER", 1.8);
                    game.Audio.Play(SoundCue.MultiplierWoohoo, .78);
                }
                else
                {
                    game.AddScore(prize.Value);
                    game.Audio.Play(SoundCue.Coin, .78);
                }
            }
        }

        if (!game.PlayerRespawning)
        {
            foreach (Shot shot in game.Shots.Where(s => s is { Alive: true, Enemy: true }).ToArray())
            {
                if (game.Touching(shot, game.Player))
                {
                    shot.Alive = false;
                    DamagePlayer(game, false, shot.Position);
                }
            }
        }

        foreach (Pickup pickup in game.Pickups.Where(p =>
                         !game.PlayerRespawning && p is
                             { Alive: true, Kind: PickupKind.Canister or PickupKind.RescueShip })
                     .ToArray())
        {
            if (!game.Touching(pickup, game.Player))
            {
                continue;
            }

            pickup.Alive = false;

            if (pickup.Kind == PickupKind.RescueShip)
            {
                game.Lives++;
                game.ShowBanner("RESCUE +1 SHIP", 2);
                game.Audio.Play(SoundCue.Life);
            }
            else
            {
                game.AwardCanister();
                if (game.IsDemoMode)
                {
                    game.DemoPowerupCollected = true;
                }
            }
        }

        Asteroid? bonusImpact = game is { PlayerRespawning: false, IsBonusStage: true, BonusStageFailed: false }
            ? game.Asteroids.FirstOrDefault(
                a => a is { Alive: true, ExitsArena: true } && game.Touching(game.Player, a))
            : null;

        if (bonusImpact is not null)
        {
            FailBonusStage(game, bonusImpact);
            return;
        }

        if (game is { PlayerRespawning: false, Player.Invulnerable: <= 0 })
        {
            Body? danger = game.Asteroids.Cast<Body>().Concat(game.Fighters).Concat(game.Bosses).Concat(game.Mines)
                .Concat(game.Vortices)
                .FirstOrDefault(b => b.Alive && game.Touching(game.Player, b));

            if (danger is not null)
            {
                if (danger is GravityVortex blackHole)
                {
                    CollapseVortex(game, blackHole, false);
                    DamagePlayer(game, true);
                }
                else if (danger is Asteroid asteroid && game.Player is { Shielding: true, Shield: > 0 })
                {
                    RamAsteroid(game, asteroid);
                }
                else if (danger is AlienBoss boss)
                {
                    V2 away = game.ArenaDelta(boss.Position, game.Player.Position).Normalized;
                    game.Player.Velocity += away * 150;
                    boss.Velocity -= away * 95;
                    DamagePlayer(game, false, boss.Position);
                    game.Player.Invulnerable = Math.Max(game.Player.Invulnerable, .45);
                }
                else
                {
                    if (danger is Asteroid { ExitsArena: true } bonusAsteroid)
                    {
                        bonusAsteroid.Alive = false;
                    }

                    if (danger is HomingMine mine)
                    {
                        DestroyMine(game, mine);
                    }

                    DamagePlayer(game);
                }
            }
        }
    }

    private void FailBonusStage(GameEngine game, Asteroid impact)
    {
        if (game.Mode != GameMode.Playing || !game.IsBonusStage || game.BonusStageFailed)
        {
            return;
        }

        game.BonusStageFailed = true;
        game.BonusAsteroidsRemaining = 0;

        foreach (Asteroid asteroid in game.Asteroids.Where(a => a.ExitsArena))
        {
            asteroid.Alive = false;
        }

        game.WaveBaseCash = 0;
        game.WaveCometCash = 0;
        game.LevelBonusCash = 0;
        game.Multiplier = 1;
        game.Explosion(impact.Position, 30, 0xffff6b64);
        game.Shockwaves.Add(new Shockwave(impact.Position, .65, 0xffff745f, 155));
        game.FloatingTexts.Add(new FloatingText(impact.Position, "BONUS FAILED  $0", 0xffff8175));
        game.Player.Velocity *= .35;
        game.ShowBanner("BONUS STAGE FAILED - $0", 2.2);
        game.Audio.Play(SoundCue.BonusFailed, .9);
        game.BeginWaveOutro();
    }

    private void RamAsteroid(GameEngine game, Asteroid asteroid)
    {
        double shieldCost = asteroid.Size switch { 3 => 24, 2 => 17, _ => 10 };
        game.Player.Shield = Math.Max(0, game.Player.Shield - shieldCost);

        V2 deflection = (asteroid.ExitsArena
            ? game.Player.Position - asteroid.Position
            : game.ArenaDelta(asteroid.Position, game.Player.Position)).Normalized;

        if (deflection.LengthSquared < .001)
        {
            deflection = -game.Player.Velocity.Normalized;
        }

        game.Player.Velocity += deflection * (65 + asteroid.Size * 22);
        game.Player.Invulnerable = .65;
        game.Shockwaves.Add(new Shockwave(game.Player.Position, .34, 0xff65e7ff, 72 + asteroid.Size * 10));

        if (asteroid.ExitsArena)
        {
            asteroid.Alive = false;
            game.Explosion(asteroid.Position, 20, 0xffbdefff);
            RegisterShieldImpact(game, asteroid.Position, 1);
            return;
        }

        if (asteroid.Steel)
        {
            asteroid.Steel = false;
            asteroid.HitPoints = 1;
            game.ShowBanner("STEEL CORE RAMMED", 1.2);
        }

        HitAsteroid(game, asteroid);
        RegisterShieldImpact(game, asteroid.Position, 1);
    }

    private void RegisterShieldImpact(GameEngine game, V2 position, double strength)
    {
        game.ShieldImpactPoint = position;
        game.ShieldImpactTime = .42;
        game.Shockwaves.Add(new Shockwave(game.Player.Position, .48, 0xff73efff, 118));
        game.Shockwaves.Add(new Shockwave(position, .32, 0xffe8ffff, 62));
        game.Spark(position, 0xffd9ffff, 18);
        game.Spark(game.Player.Position, 0xff55dfff, 10);
        game.Audio.Play(SoundCue.ShieldImpact, Math.Clamp(strength, 0, 1));
    }


    internal void HitAsteroid(GameEngine game, Asteroid asteroid, int damage = 1)
    {
        if (asteroid.ExitsArena)
        {
            game.Spark(asteroid.Position, 0xffd9f7ff, 8);
            game.Audio.Play(SoundCue.SteelHit, .55);
            return;
        }

        if (asteroid.Steel)
        {
            asteroid.HitPoints -= Math.Max(1, damage);
            game.Spark(asteroid.Position, 0xffd9f7ff, 7 + damage * 3);
            game.Audio.Play(SoundCue.SteelHit, .55);

            if (asteroid.HitPoints <= 0)
            {
                if (game.Random.Next(10) == 0)
                {
                    asteroid.Steel = false;
                    asteroid.HitPoints = 1;
                    game.ShowBanner("STEEL CORE FRACTURED", 1.4);
                }
                else if (game.Random.Next(5) == 0)
                {
                    asteroid.Alive = false;
                    game.Mines.Add(new HomingMine(asteroid.Position, asteroid.Velocity));
                    game.Audio.Play(SoundCue.Mine);
                }
                else
                {
                    asteroid.HitPoints = 5;
                }
            }

            return;
        }

        asteroid.Alive = false;
        game.AddScore(asteroid.Size switch { 3 => 20, 2 => 50, _ => 100 });
        game.Explosion(asteroid.Position, asteroid.Size == 3 ? 26 : 15, 0xffff9b4a);

        if (asteroid.Size > 1)
        {
            int fragments = RollAsteroidFragmentCount(game);

            for (int i = 0; i < fragments; i++)
            {
                V2 velocity = asteroid.Velocity * .4 + game.RandomDirection() * game.Random.Next(75, 175);

                game.Asteroids.Add(new Asteroid(asteroid.Position + game.RandomDirection() * 8, velocity,
                    asteroid.Size - 1, false, game.Random.Next()));
            }
        }

        game.RollDrop(asteroid.Position);
        game.Audio.Play(SoundCue.AsteroidExplosion, asteroid.Size == 3 ? 1 : asteroid.Size == 2 ? .9 : .82);
    }

    internal int RollAsteroidFragmentCount(GameEngine game)
    {
        if (game.Random.Next(3) != 0)
        {
            return 3;
        }

        return game.Random.Next(2) + 1;
    }

    internal void DestroyFighter(GameEngine game, Fighter fighter)
    {
        fighter.Alive = false;

        if (game.IsDemoMode)
        {
            game.DemoEnemyDestroyed = true;
        }

        game.AwardImmediateScore(fighter.Kind == FighterKind.Interceptor ? 1000 : 500, fighter.Position);
        game.Explosion(fighter.Position, 24, fighter.Kind == FighterKind.Interceptor ? 0xff58e9ff : 0xffff4f83);
        game.RollDrop(fighter.Position, .24);
        game.Audio.Play(SoundCue.Explosion, .75);
    }

    internal void DamageBoss(GameEngine game, AlienBoss boss, int damage, V2 hitPosition)
    {
        if (!boss.Alive)
        {
            return;
        }

        boss.HitPoints -= damage;
        boss.HurtFlash = .14;
        game.Spark(hitPosition, game.BossTint(boss.Kind), 7 + damage * 2);

        if (boss.HitPoints <= 0)
        {
            DestroyBoss(game, boss);
        }
        else
        {
            game.Audio.Play(SoundCue.SteelHit, .38);
        }
    }

    private void DestroyBoss(GameEngine game, AlienBoss boss)
    {
        boss.Alive = false;
        int reward = 5_000 + boss.Encounter * 1_500;
        game.AddScore(reward);

        foreach (Shot shot in game.Shots.Where(s => s is { Alive: true, BossShot: true }))
        {
            shot.Alive = false;
        }

        game.Explosion(boss.Position, 92, game.BossTint(boss.Kind));

        for (int i = 0; i < 3; i++)
        {
            V2 burst = boss.Position + game.RandomDirection() * game.Random.Next(20, 68);
            game.Explosion(burst, 24, i == 1 ? 0xffffbd62 : game.BossTint(boss.Kind));
        }

        game.Shockwaves.Add(new Shockwave(boss.Position, 1.05, game.BossTint(boss.Kind), 330));
        game.FloatingTexts.Add(new FloatingText(boss.Position, $"BOSS BOUNTY  +${reward:N0}", 0xffffdc78));
        game.ShowBanner($"{game.BossName(boss.Kind)} DEFEATED", 2.8);
        game.Audio.Play(SoundCue.ShipBlast);
        game.Audio.Play(SoundCue.Explosion, .92);
    }

    internal void DestroyMine(GameEngine game, HomingMine mine)
    {
        mine.Alive = false;
        game.AddScore(500);
        game.Explosion(mine.Position, 18, 0xffffdf4d);
        game.Audio.Play(SoundCue.Explosion, .55);
    }

    private void DestroyVortex(GameEngine game, GravityVortex vortex)
    {
        CollapseVortex(game, vortex, true);
    }

    private void CollapseVortex(GameEngine game, GravityVortex vortex, bool awardScore)
    {
        vortex.Alive = false;

        if (game.IsDemoMode && awardScore)
        {
            game.DemoBlackHoleDestroyed = true;
        }

        if (awardScore)
        {
            game.AddScore(2000);
        }

        game.Shockwaves.Add(new Shockwave(vortex.Position, .8, 0xffb069ff, 230));
        game.Explosion(vortex.Position, 40, 0xff6ad7ff);
        game.Audio.Play(SoundCue.Vortex);
    }

    internal void DamagePlayer(GameEngine game, bool bypassShield = false, V2? impactPosition = null)
    {
        if (game.PlayerRespawning || game.Player.Invulnerable > 0)
        {
            return;
        }

        if (game.IsDemoMode)
        {
            if (!bypassShield && game.Player is { Shielding: true, Shield: > 0 })
            {
                game.Player.Shield = Math.Max(0, game.Player.Shield - 10);
                RegisterShieldImpact(game, impactPosition ?? game.Player.Position, .82);
            }

            game.Player.Invulnerable = .35;
            return;
        }

        if (!bypassShield && game.Player is { Shielding: true, Shield: > 0 })
        {
            game.Player.Shield = Math.Max(0, game.Player.Shield - 18);
            RegisterShieldImpact(game, impactPosition ?? game.Player.Position, .92);
            return;
        }

        if (!bypassShield && game.Player.Giant)
        {
            game.ShrinkGiantShip(impactPosition ?? game.Player.Position);
            return;
        }

        game.SpawnShipWreck();
        bool noShipsRemaining = game.Lives <= 0;

        if (!noShipsRemaining)
        {
            game.Lives--;
        }

        game.ClearEquippedPowerups();
        game.LastPowerupTime = 0;
        game.Explosion(game.Player.Position, 76, 0xff62e6ff);
        game.Shockwaves.Add(new Shockwave(game.Player.Position, 1.05, 0xffff6b5e, 265));
        game.Shockwaves.Add(new Shockwave(game.Player.Position, .68, 0xffffc06a, 145));
        game.Spark(game.Player.Position, 0xffffffff, 28);
        game.Audio.Play(SoundCue.ShipCrash);
        game.Audio.Play(SoundCue.ShipBlast);

        if (noShipsRemaining)
        {
            game.Audio.StopMusic(false);

            game.PendingGameOverHighScore =
                game is { BonusOnlyMode: false, BossOnlyMode: false } &&
                (game.HighScores.Count < 10 || game.Score > game.HighScores[^1].Score);

            game.HighlightedHighScore = null;
            game.PendingName = "";
            game.GameOverDelayTimer = GameEngine.GameOverDelayDuration;
            game.GameOverFadeElapsed = 0;
            game.Mode = GameMode.GameOverDelay;
            game.ShowBanner("SIGNAL LOST", 99);
            return;
        }

        game.Player.Thrusting = false;
        game.Player.Shielding = false;
        game.ShieldReleaseTimer = 0;
        game.RespawnTimer = GameEngine.RespawnDelay;
        game.ShowBanner("SHIP DESTROYED", GameEngine.RespawnDelay);
    }
}
