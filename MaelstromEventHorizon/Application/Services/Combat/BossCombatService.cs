using MaelstromEventHorizon.Domain.Effects;
using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Application.Services.Combat;

internal sealed class BossCombatService
{
    internal void CompleteBonusAsteroid(GameEngine game, Asteroid asteroid)
    {
        if (!asteroid.Alive)
        {
            return;
        }

        asteroid.Alive = false;
        game.BonusAsteroidsDodged++;
        game.AddScore(500);
    }

    internal void UpdateBosses(GameEngine game, double dt, bool frozen)
    {
        foreach (AlienBoss boss in game.Bosses)
        {
            boss.Age += dt;
            boss.HurtFlash = Math.Max(0, boss.HurtFlash - dt);

            if (frozen)
            {
                continue;
            }

            V2 toPlayer = game.ArenaDelta(boss.Position, game.Player.Position);
            V2 direction = toPlayer.Normalized;
            V2 tangent = new(-direction.Y, direction.X);
            double scale = Math.Min(1.4, 1 + (boss.Encounter - 1) * .055);
            V2 desired;

            switch (boss.Kind)
            {
                case AlienBossKind.SludgeMaw:
                    boss.SpecialTimer -= dt;

                    if (!game.PlayerRespawning && boss.SpecialTimer <= 0)
                    {
                        FireSludgeVomit(game, boss, direction);
                        boss.SpecialTimer = Math.Max(4.8, 6.6 - boss.Encounter * .1);
                    }

                    desired = direction * (44 * scale) + tangent * Math.Sin(boss.Age * 1.2) * 48;
                    break;
                case AlienBossKind.EyeTyrant:
                    double rangeCorrection = Math.Clamp((toPlayer.Length - 360) * .34, -72, 72);
                    desired = direction * rangeCorrection + tangent * (74 * scale);
                    break;
                case AlienBossKind.BoneBroodmother:
                    boss.SpecialTimer -= dt;

                    if (boss.SpecialTimer <= 0)
                    {
                        boss.Velocity = direction * (235 + boss.Encounter * 6);
                        boss.SpecialTimer = Math.Max(3.8, 5.8 - boss.Encounter * .1);
                        game.Spark(boss.Position, 0xffffb05f, 12);
                    }

                    desired = direction * (58 * scale) + tangent * Math.Sin(boss.Age * .85) * 26;
                    break;
                default:
                    desired = direction * (70 * scale) + tangent * Math.Sin(boss.Age * 1.9) * 80;
                    break;
            }

            double steering = boss.Kind == AlienBossKind.BoneBroodmother ? .42 : .92;
            boss.Velocity += (desired - boss.Velocity) * Math.Min(1, dt * steering);
            double speedCap = (boss.Kind == AlienBossKind.BoneBroodmother ? 260 : 155) + boss.Encounter * 3;

            if (boss.Velocity.Length > speedCap)
            {
                boss.Velocity = boss.Velocity.Normalized * speedCap;
            }

            boss.Angle = Math.Atan2(boss.Velocity.Y, boss.Velocity.X);
            boss.Phase += dt * (.85 + boss.Encounter * .025);
            boss.Position = game.MoveBody(boss, boss.Position + boss.Velocity * dt);
            boss.AttackTimer -= dt;

            if (!game.PlayerRespawning && boss.AttackTimer <= 0)
            {
                FireBossAttack(game, boss);
            }
        }
    }

    private void FireBossAttack(GameEngine game, AlienBoss boss)
    {
        double tempo = Math.Min(.32, (boss.Encounter - 1) * .025);

        V2 aim = game.Rotate(game.PredictAim(boss.Position, game.Player.Position, game.Player.Velocity, 270 + boss.Encounter * 5),
            (game.Random.NextDouble() - .5) * .2);

        switch (boss.Kind)
        {
            case AlienBossKind.SludgeMaw:
                AddSludgeGlob(game, boss, aim);
                boss.AttackTimer = Math.Max(1.75, 2.75 - tempo);
                break;
            case AlienBossKind.EyeTyrant:

                for (int i = -1; i <= 1; i++)
                {
                    AddBossShot(game, boss, game.Rotate(aim, i * .26), 315 + boss.Encounter * 6, 0xffd976ff, 3.35);
                }

                boss.AttackTimer = Math.Max(.95, 1.75 - tempo);
                break;
            case AlienBossKind.BoneBroodmother:
                int radialCount = 8 + Math.Min(2, boss.Encounter / 3);

                for (int i = 0; i < radialCount; i++)
                {
                    AddBossShot(game, boss, V2.FromAngle(i * Math.PI * 2 / radialCount + boss.Phase),
                        225 + boss.Encounter * 5, 0xffff8c4d, 4.1);
                }

                boss.AttackTimer = Math.Max(1.55, 2.75 - tempo);
                break;
            default:

                for (int i = 0; i < 5; i++)
                {
                    AddBossShot(game, boss, V2.FromAngle(boss.Phase * 2.1 + i * Math.PI * 2 / 5),
                        270 + boss.Encounter * 5, 0xff56f1d2, 3.6);
                }

                boss.AttackTimer = Math.Max(.8, 1.5 - tempo);
                break;
        }

        game.Audio.Play(SoundCue.EnemyFire, .68);
    }

    private void AddBossShot(GameEngine game, AlienBoss boss, V2 direction, double speed, uint tint, double lifetime)
    {
        direction = direction.Normalized;

        game.Shots.Add(new Shot(boss.Position + direction * (boss.Radius * .72), direction * speed, true, lifetime)
        {
            Radius = 5.2,
            BossShot = true,
            Tint = tint
        });
    }

    private void AddSludgeGlob(GameEngine game, AlienBoss boss, V2 direction)
    {
        direction = direction.Normalized;

        game.Shots.Add(new Shot(boss.Position + direction * (boss.Radius * .72),
            direction * (155 + boss.Encounter * 3), true, 4.2)
        {
            Radius = 12.5,
            BossShot = true,
            Tint = 0xff86dc45,
            Sludge = true,
            SplitAge = .95 + game.Random.NextDouble() * .35,
            Angle = game.Random.NextDouble() * Math.PI * 2
        });
    }

    internal void SplitSludgeGlob(GameEngine game, Shot glob)
    {
        if (!glob.Alive)
        {
            return;
        }

        glob.Alive = false;
        V2 forward = glob.Velocity.Normalized;
        int fragments = 2 + game.Random.Next(2);

        for (int i = 0; i < fragments; i++)
        {
            double spread = (i - (fragments - 1) / 2.0) * .17 + (game.Random.NextDouble() - .5) * .12;
            V2 direction = game.Rotate(forward, spread);

            game.Shots.Add(new Shot(glob.Position + game.RandomDirection() * 5,
                direction * (125 + game.Random.NextDouble() * 45), true, 2.5 + game.Random.NextDouble() * .5)
            {
                Radius = 4.5 + game.Random.NextDouble() * 1.8,
                BossShot = true,
                Tint = game.Random.Next(3) == 0 ? 0xff4f8f2d : 0xff8fe84f,
                Sludge = true,
                Angle = game.Random.NextDouble() * Math.PI * 2
            });
        }

        game.Spark(glob.Position, 0xff9bf25b, 14);
        game.Shockwaves.Add(new Shockwave(glob.Position, .3, 0xff75cf3d, 38));
        game.Audio.Play(SoundCue.EnemyFire, .36);
    }

    private void FireSludgeVomit(GameEngine game, AlienBoss boss, V2 aim)
    {
        int droplets = 6 + Math.Min(3, boss.Encounter / 3);
        V2 tangent = new(-aim.Y, aim.X);

        for (int i = 0; i < droplets; i++)
        {
            double across = droplets == 1 ? 0 : i / (double)(droplets - 1) - .5;
            V2 direction = game.Rotate(aim, across * 1.35 + (game.Random.NextDouble() - .5) * .2);
            V2 origin = boss.Position + aim * (boss.Radius * .68) + tangent * ((game.Random.NextDouble() - .5) * 24);

            game.Shots.Add(new Shot(origin, direction * (100 + game.Random.NextDouble() * 65), true,
                2.8 + game.Random.NextDouble() * .7)
            {
                Radius = 3.8 + game.Random.NextDouble() * 3.2,
                BossShot = true,
                Tint = game.Random.Next(4) switch
                {
                    0 => 0xffb7f36a,
                    1 => 0xff46762a,
                    _ => 0xff77c93f
                },
                Sludge = true,
                SludgeVomit = true,
                Angle = game.Random.NextDouble() * Math.PI * 2
            });
        }

        game.Spark(boss.Position + aim * (boss.Radius * .7), 0xffa8ef62, 20);
        game.Audio.Play(SoundCue.EnemyFire, .78);
    }

    internal void ApplyGravity(GameEngine game, Body body, double dt)
    {
        foreach (GravityVortex vortex in game.Vortices.Where(v => v.Alive))
        {
            V2 delta = game.ArenaDelta(body.Position, vortex.Position);
            double d2 = Math.Max(1800, delta.LengthSquared);
            body.Velocity += delta.Normalized * (1_100_000 / d2 * dt);
        }
    }

    internal void ApplyPlayerGravity(GameEngine game, double dt)
    {
        foreach (GravityVortex vortex in game.Vortices.Where(v => v.Alive))
        {
            V2 delta = game.ArenaDelta(game.Player.Position, vortex.Position);
            double d2 = Math.Max(3000, delta.LengthSquared);
            game.Player.Velocity += delta.Normalized * (GameEngine.PlayerVortexGravity / d2 * dt);
        }
    }
}
