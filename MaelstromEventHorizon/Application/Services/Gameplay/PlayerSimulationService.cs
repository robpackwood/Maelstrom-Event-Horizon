using System.Windows.Input;
using MaelstromEventHorizon.Domain.Effects;
using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Application.Services.Gameplay;

internal sealed class PlayerSimulationService
{
    internal void TickTimers(GameEngine game, double dt)
    {
        game.FireCooldown -= dt;

        if (game.RapidFireReload > 0)
        {
            game.RapidFireReload -= dt;
            if (game.RapidFireReload <= 0)
            {
                game.RapidFireRoundsFired = 0;
            }
        }

        game.BannerTime -= dt;
        game.LastPowerupTime -= dt;
        game.FreezeTime -= dt;
        game.Player.Invulnerable -= dt;
        game.Player.SpawnShieldTime -= dt;
        game.ShieldImpactTime = Math.Max(0, game.ShieldImpactTime - dt);

        if (game.RespawnTimer > 0)
        {
            game.RespawnTimer = Math.Max(0, game.RespawnTimer - dt);

            if (game.RespawnTimer <= 0)
            {
                game.RespawnPlayer();
            }
        }

        game.ScreenShakeTime = Math.Max(0, game.ScreenShakeTime - dt);

        if (game.IsBonusStage)
        {
            game.BonusTravelTime += dt;
        }

        game.UpdateLevelBonus(dt);
    }

    internal void UpdatePlayer(GameEngine game, double dt)
    {
        double turn = 0;

        if (Keyboard.IsKeyDown(game.Bindings[GameAction.TurnLeft]))
        {
            turn -= 1;
        }

        if (Keyboard.IsKeyDown(game.Bindings[GameAction.TurnRight]))
        {
            turn += 1;
        }

        double targetTurnVelocity = turn * 4.35;
        game.TurnVelocity += (targetTurnVelocity - game.TurnVelocity) * Math.Min(1, dt * 12);
        game.Player.Angle += game.TurnVelocity * dt;
        bool wasThrusting = game.Player.Thrusting;
        bool wasShielding = game.Player.Shielding;
        game.Player.Thrusting = Keyboard.IsKeyDown(game.Bindings[GameAction.Thrust]);
        bool shieldHeld = !game.IsBonusStage && Keyboard.IsKeyDown(game.Bindings[GameAction.Shield]);

        if (shieldHeld && game.Player.Shield > 0)
        {
            game.ShieldReleaseTimer = GameEngine.ShieldReleaseDelay;
        }
        else
        {
            game.ShieldReleaseTimer = Math.Max(0, game.ShieldReleaseTimer - dt);
        }

        if (game.IsBonusStage)
        {
            game.ShieldReleaseTimer = 0;
        }

        game.Player.Shielding = game is { IsBonusStage: false, Player.Shield: > 0 } &&
                                (shieldHeld || game.ShieldReleaseTimer > 0);

        if (game.Player.Thrusting)
        {
            game.ThrustRamp = Math.Min(1, game.ThrustRamp + dt * 2.4);
            V2 facing = V2.FromAngle(game.Player.Angle);
            double acceleration = 493 + game.ThrustRamp * 195.5;
            game.Player.Velocity += facing * (acceleration * dt);
            game.EmitThrust();

            if (!wasThrusting)
            {
                game.Audio.Play(SoundCue.Thrust, .5);
            }
        }
        else
        {
            game.ThrustRamp = Math.Max(0, game.ThrustRamp - dt * 1.8);
        }

        if (game.AirBrakesActive)
        {
            game.Player.Velocity *= Math.Pow(.08, dt);
        }
        else
        {
            game.Player.Velocity *= Math.Pow(.99965, dt * 60);
        }

        if (game.Player.Shielding)
        {
            game.Player.Shield = Math.Max(0, game.Player.Shield - 22 * dt);

            if (!wasShielding)
            {
                game.Audio.Play(SoundCue.Shield, .45);
            }
        }

        bool firing = Keyboard.IsKeyDown(game.Bindings[GameAction.Fire]);
        bool rapidWeaponReady = game is { FireCooldown: <= 0, RapidFireReload: <= 0 };

        if (game is { IsBonusStage: false, RapidFireActive: true } && firing && rapidWeaponReady)
        {
            FirePlayer(game);
        }

        game.ApplyPlayerGravity(dt);
        double maxSpeed = GameEngine.PlayerMaxSpeed;

        if (game.Player.Velocity.Length > maxSpeed)
        {
            game.Player.Velocity = game.Player.Velocity.Normalized * maxSpeed;
        }

        game.Player.Position = game.MoveBody(game.Player, game.Player.Position + game.Player.Velocity * dt);
    }

    internal void UpdateDemoPlayer(GameEngine game, double dt)
    {
        Body? target = game.DemoStage switch
        {
            0 => game.Pickups.FirstOrDefault(p => p is { Alive: true, Kind: PickupKind.Canister }),
            1 => game.Fighters.FirstOrDefault(f => f.Alive),
            2 => game.Vortices.FirstOrDefault(v => v.Alive),
            _ => game.Asteroids.Where(a => a.Alive)
                .OrderBy(a => game.ArenaDelta(game.Player.Position, a.Position).LengthSquared).FirstOrDefault()
        };

        V2 targetDelta = target is null
            ? V2.FromAngle(game.Player.Angle)
            : game.ArenaDelta(game.Player.Position, target.Position);

        bool combatTarget = target is Asteroid or Fighter or GravityVortex;

        if (combatTarget && target is not null)
        {
            double lead = Math.Min(.7, targetDelta.Length / GameEngine.PlayerShotSpeed);
            targetDelta += target.Velocity * lead;
        }

        double desiredAngle = Math.Atan2(targetDelta.Y, targetDelta.X);

        double angleError = Math.Atan2(Math.Sin(desiredAngle - game.Player.Angle),
            Math.Cos(desiredAngle - game.Player.Angle));

        double targetTurnVelocity = Math.Clamp(angleError * 5.5, -3.75, 3.75);
        game.TurnVelocity += (targetTurnVelocity - game.TurnVelocity) * Math.Min(1, dt * 9);
        game.Player.Angle += game.TurnVelocity * dt;
        bool wasThrusting = game.Player.Thrusting;
        bool wasShielding = game.Player.Shielding;
        double targetDistance = targetDelta.Length;

        game.Player.Thrusting = target is Pickup
            ? targetDistance > 34 && game.Player.Velocity.Length < 325
            : target is not null && targetDistance > 285 && game.Player.Velocity.Length < 290;

        if (game.Player.Thrusting)
        {
            game.ThrustRamp = Math.Min(1, game.ThrustRamp + dt * 2.4);
            double acceleration = 493 + game.ThrustRamp * 195.5;
            game.Player.Velocity += V2.FromAngle(game.Player.Angle) * (acceleration * dt);
            game.EmitThrust();

            if (!wasThrusting)
            {
                game.Audio.Play(SoundCue.Thrust, .45);
            }
        }
        else
        {
            game.ThrustRamp = Math.Max(0, game.ThrustRamp - dt * 1.8);
        }

        bool shieldWanted =
            game.Shots
                .Any(shot => shot is { Alive: true, Enemy: true } &&
                             game.ArenaDelta(game.Player.Position, shot.Position).LengthSquared <
                             145 * 145)
            ||
            game.Asteroids
                .Any(asteroid => asteroid.Alive &&
                                 game.ArenaDelta(game.Player.Position, asteroid.Position)
                                     .LengthSquared < 92 * 92);

        if (shieldWanted && game.Player.Shield > 0)
        {
            game.ShieldReleaseTimer = GameEngine.ShieldReleaseDelay;
        }
        else
        {
            game.ShieldReleaseTimer = Math.Max(0, game.ShieldReleaseTimer - dt);
        }

        game.Player.Shielding = game.Player.Shield > 0 && (shieldWanted || game.ShieldReleaseTimer > 0);

        if (game.Player.Shielding)
        {
            game.Player.Shield = Math.Max(0, game.Player.Shield - 22 * dt);

            if (!wasShielding)
            {
                game.Audio.Play(SoundCue.Shield, .42);
            }
        }

        game.DemoFireCooldown -= dt;
        bool weaponReady = !game.RapidFireActive || game is { FireCooldown: <= 0, RapidFireReload: <= 0 };

        if (combatTarget &&
            Math.Abs(angleError) < .11 &&
            targetDistance < 690 &&
            game.DemoFireCooldown <= 0 &&
            weaponReady)
        {
            FirePlayer(game);
            game.DemoFireCooldown = game.RapidFireActive ? .09 : .19;
        }

        game.ApplyPlayerGravity(dt);
        game.Player.Velocity *= Math.Pow(.9988, dt * 60);

        if (game.Player.Velocity.Length > 340)
        {
            game.Player.Velocity = game.Player.Velocity.Normalized * 340;
        }

        game.Player.Position = game.MoveBody(game.Player, game.Player.Position + game.Player.Velocity * dt);
    }

    internal void UpdateDemoScript(GameEngine game)
    {
        if (game.DemoStage == 0)
        {
            if (!game.DemoPowerupCollected)
            {
                if (!game.Pickups.Any(p => p is { Alive: true, Kind: PickupKind.Canister }))
                {
                    game.Pickups.Add(new Pickup(game.Wrap(game.Player.Position + V2.FromAngle(game.Player.Angle) * 230),
                        V2.Zero, PickupKind.Canister));
                }

                return;
            }

            game.DemoStage = 1;
            game.FighterSpawnedThisWave = true;
            V2 fighterPosition = game.Wrap(game.Player.Position + new V2(390, 115));
            game.Fighters.Add(new Fighter(fighterPosition, new V2(-24, 18), FighterKind.Raider) { FireDelay = 3.4 });
            game.ShowBanner("AUTOPILOT TARGET: RAIDER", 1.8);
            game.Audio.Play(SoundCue.EnemyWarning, .62);
            return;
        }

        if (game is { DemoStage: 1, DemoEnemyDestroyed: true })
        {
            game.DemoStage = 2;
            SpawnDemoBlackHole(game);
            return;
        }

        if (game.DemoStage == 2)
        {
            if (game.DemoBlackHoleDestroyed)
            {
                game.DemoStage = 3;
                game.ShowBanner("AUTOPILOT COMBAT PATROL", 1.8);
            }
            else if (!game.Vortices.Any(v => v.Alive))
            {
                SpawnDemoBlackHole(game);
            }
        }
    }

    private void SpawnDemoBlackHole(GameEngine game)
    {
        game.BlackHoleSpawned = true;
        V2 position = game.Wrap(game.Player.Position + new V2(355, -125));
        game.Vortices.Add(new GravityVortex(position) { Lifetime = 20 });
        game.ShowBanner("AUTOPILOT TARGET: BLACK HOLE", 1.8);
    }

    internal void FirePlayer(GameEngine game)
    {
        int availableRounds = game.RapidFireActive
            ? Math.Min(GameEngine.RapidFireBurstSize - game.RapidFireRoundsFired,
                GameEngine.RapidFireBurstSize - game.Shots.Count(shot => shot is { Alive: true, Enemy: false }))
            : int.MaxValue;

        if (availableRounds <= 0)
        {
            return;
        }

        V2 facing = V2.FromAngle(game.Player.Angle);
        double range = game.LongRangeActive ? 1.45 : 1;
        int roundsFired = 0;

        void Add(double offset)
        {
            if (roundsFired >= availableRounds)
            {
                return;
            }

            V2 direction = V2.FromAngle(game.Player.Angle + offset);

            Shot shot = new(game.Player.Position + direction * (22 * game.Player.VisualScale),
                game.Player.Velocity * .231 + direction * GameEngine.PlayerShotSpeed, false, .82 * range)
            {
                Radius = 4.3,
                Damage = 1,
                PowerLevel = 0
            };

            game.Shots.Add(shot);
            roundsFired++;
        }

        Add(0);

        if (game.TripleFireActive)
        {
            Add(-.17);
            Add(.17);
        }

        if (game.RapidFireActive)
        {
            game.RapidFireRoundsFired += roundsFired;
            game.FireCooldown = GameEngine.RapidFireShotInterval;

            if (game.RapidFireRoundsFired >= GameEngine.RapidFireBurstSize)
            {
                game.RapidFireReload = GameEngine.RapidFireReloadDuration;
            }
        }
        else
        {
            game.FireCooldown = 0;
        }

        game.Player.Velocity -= facing * .8;
        game.Audio.Play(SoundCue.Fire, .58);
    }

    internal void UpdateWorld(GameEngine game, double dt)
    {
        bool frozen = game.FreezeTime > 0;

        foreach (Asteroid asteroid in game.Asteroids)
        {
            asteroid.Age += dt;
            asteroid.Angle += asteroid.Spin * dt;

            if (asteroid.ExitsArena)
            {
                if (Math.Abs(asteroid.BonusCurve) > .001)
                {
                    asteroid.Velocity = game.Rotate(asteroid.Velocity, asteroid.BonusCurve * dt);
                }

                asteroid.Position += asteroid.Velocity * dt;
                double margin = asteroid.Radius * 1.6;

                bool inside = asteroid.Position.X >= -margin * .15 &&
                              asteroid.Position.X <= GameEngine.Width + margin * .15 &&
                              asteroid.Position.Y >= -margin * .15 &&
                              asteroid.Position.Y <= GameEngine.Height + margin * .15;

                if (inside)
                {
                    asteroid.EnteredArena = true;
                }

                bool outside = asteroid.Position.X < -margin || asteroid.Position.X > GameEngine.Width + margin ||
                               asteroid.Position.Y < -margin || asteroid.Position.Y > GameEngine.Height + margin;

                if (asteroid.EnteredArena && outside)
                {
                    game.CompleteBonusAsteroid(asteroid);
                }
            }
            else
            {
                game.ApplyGravity(asteroid, dt);
                asteroid.Position = game.MoveBody(asteroid, asteroid.Position + asteroid.Velocity * dt);
            }
        }

        foreach (Fighter fighter in game.Fighters)
        {
            fighter.Age += dt;

            if (frozen)
            {
                continue;
            }

            V2 toShip = game.ArenaDelta(fighter.Position, game.Player.Position);
            V2 tangent = new(-toShip.Y, toShip.X);
            double weave = Math.Sin(fighter.Age * (fighter.Kind == FighterKind.Interceptor ? 2.8 : 1.5));

            V2 desired = toShip.Normalized * (fighter.Kind == FighterKind.Interceptor ? 118 : 72) +
                         tangent.Normalized * weave * 68;

            fighter.Velocity += (desired - fighter.Velocity) * Math.Min(1, dt * 1.15);
            fighter.Angle = Math.Atan2(fighter.Velocity.Y, fighter.Velocity.X);
            fighter.Position = game.MoveBody(fighter, fighter.Position + fighter.Velocity * dt);
            fighter.FireDelay -= dt;

            if (!game.PlayerRespawning && fighter.FireDelay <= 0 && toShip.Length < 720)
            {
                const double enemyShotSpeed = 335;

                V2 direction = game.PredictAim(
                    fighter.Position, game.Player.Position, game.Player.Velocity, enemyShotSpeed);

                double spread = fighter.Kind == FighterKind.Interceptor ? .23 : .32;
                direction = game.Rotate(direction, (game.Random.NextDouble() * 2 - 1) * spread);
                game.Shots.Add(new Shot(fighter.Position + direction * 22, direction * enemyShotSpeed, true, 2.35));

                fighter.FireDelay = fighter.Kind == FighterKind.Interceptor
                    ? 1.2 + game.Random.NextDouble() * .65
                    : 1.85 + game.Random.NextDouble() * .9;

                game.Audio.Play(SoundCue.EnemyFire, .55);
            }
        }

        game.UpdateBosses(dt, frozen);

        foreach (HomingMine mine in game.Mines)
        {
            mine.Age += dt;

            if (!frozen)
            {
                V2 delta = game.ArenaDelta(mine.Position, game.Player.Position);
                mine.Velocity += delta.Normalized * (105 * dt);

                if (mine.Velocity.Length > 220)
                {
                    mine.Velocity = mine.Velocity.Normalized * 220;
                }

                mine.Angle += dt * 4.5;
                mine.Position = game.MoveBody(mine, mine.Position + mine.Velocity * dt);
            }
        }

        foreach (GravityVortex vortex in game.Vortices)
        {
            vortex.Age += dt;
            vortex.Angle -= dt * 1.8;

            if (vortex.Age >= vortex.Lifetime)
            {
                vortex.Alive = false;
            }
        }

        foreach (Nova nova in game.Novas)
        {
            nova.Age += dt;
            nova.Angle += dt * (1 + nova.Age);

            if (nova is { Detonated: false, Age: >= Nova.Fuse })
            {
                game.DetonateNova(nova);
            }
        }

        foreach (Pickup pickup in game.Pickups)
        {
            pickup.Age += dt;
            pickup.Angle += dt * (pickup.Kind == PickupKind.RescueShip ? 4.6 : 1.7);
            V2 nextPosition = pickup.Position + pickup.Velocity * dt;
            pickup.Position = game.MoveBody(pickup, nextPosition, pickup.Kind != PickupKind.RescueShip);

            if (pickup.Age >= pickup.Lifetime)
            {
                pickup.Alive = false;
            }
        }

        foreach (Comet comet in game.Comets)
        {
            comet.Age += dt;
            comet.Position = game.MoveBody(comet, comet.Position + comet.Velocity * dt);
            comet.Angle = Math.Atan2(comet.Velocity.Y, comet.Velocity.X);

            if (comet.Age >= comet.Lifetime)
            {
                comet.Alive = false;
            }

            if (game.Random.NextDouble() < Math.Min(1, dt * 70))
            {
                V2 tail = -comet.Velocity.Normalized;

                game.Particles.Add(new Particle(comet.Position + tail * 16 + game.RandomDirection() * 6,
                    tail * game.Random.Next(90, 250) + game.RandomDirection() * 24,
                    .35 + game.Random.NextDouble() * .45,
                    game.Random.Next(3) == 0 ? 0xffffffff : comet.Tint, 2 + game.Random.NextDouble() * 5));
            }
        }

        List<Shot>? splittingSludge = null;

        foreach (Shot shot in game.Shots)
        {
            shot.Age += dt;
            shot.Angle += dt * (shot.Enemy ? -5.4 : 6.8);
            V2 nextPosition = shot.Position + shot.Velocity * dt;

            if (shot.BossShot)
            {
                shot.Position = nextPosition;
                if (nextPosition.X < -shot.Radius || nextPosition.X > GameEngine.Width + shot.Radius ||
                    nextPosition.Y < -shot.Radius || nextPosition.Y > GameEngine.Height + shot.Radius)
                {
                    shot.Alive = false;
                    continue;
                }
            }
            else
            {
                shot.Position = game.MoveBody(shot, nextPosition);
            }

            if (shot.Age >= shot.Lifetime)
            {
                shot.Alive = false;
            }
            else if (shot is { Sludge: true, SplitAge: > 0 } && shot.Age >= shot.SplitAge)
            {
                (splittingSludge ??= []).Add(shot);
            }
        }

        if (splittingSludge is not null)
        {
            foreach (Shot glob in splittingSludge)
            {
                game.SplitSludgeGlob(glob);
            }
        }

        foreach (Particle particle in game.Particles)
        {
            particle.Age += dt;
            particle.Position = game.MoveBody(particle, particle.Position + particle.Velocity * dt, false);
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
    }
}
