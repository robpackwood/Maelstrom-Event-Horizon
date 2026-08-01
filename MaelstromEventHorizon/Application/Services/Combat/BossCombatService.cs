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

    internal void UpdateBosses(GameEngine game, double dt)
    {
        if (game.BossCountdownActive)
        {
            return;
        }

        foreach (AlienBoss boss in game.Bosses)
        {
            boss.Age += dt;
            boss.HurtFlash = Math.Max(0, boss.HurtFlash - dt);

            // Respawning relocates the ship to the center. Hold the boss's course until
            // the shield window ends so it does not abruptly reverse toward that new position.
            if (game.PlayerRespawning)
            {
                boss.Phase += dt * (.85 + boss.Encounter * .025);
                boss.Position = game.MoveBody(boss, boss.Position + boss.Velocity * dt);
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
                case AlienBossKind.DreadHarvester:
                    boss.SpecialTimer -= dt;
                    if (boss.SpecialTimer <= 0)
                    {
                        boss.Velocity = tangent * (245 + boss.Encounter * 7);
                        boss.SpecialTimer = Math.Max(3.2, 5.1 - boss.Encounter * .1);
                        game.Spark(boss.Position, 0xffd5d94a, 16);
                    }

                    desired = direction * (36 * scale) + tangent * Math.Sin(boss.Age * 2.4) * 92;
                    break;
                case AlienBossKind.SolarWarden:
                    double solarRange = Math.Clamp((toPlayer.Length - 290) * .48, -98, 98);
                    desired = direction * solarRange - tangent * (95 * scale);
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

        V2 aim = game.Rotate(
            game.PredictAim(boss.Position, game.Player.Position, game.Player.Velocity, 270 + boss.Encounter * 5),
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
            case AlienBossKind.DreadHarvester:
                for (int i = 0; i < 10; i++)
                {
                    AddBossShot(game, boss, V2.FromAngle(boss.Phase * 1.7 + i * Math.PI * 2 / 10),
                        245 + boss.Encounter * 7, 0xffd5d94a, 3.9);
                }

                boss.AttackTimer = Math.Max(1.15, 2.05 - tempo);
                break;
            case AlienBossKind.SolarWarden:
                for (int i = -2; i <= 2; i++)
                {
                    AddBossShot(game, boss, game.Rotate(aim, i * .16), 360 + boss.Encounter * 8, 0xffffcf54, 3.1);
                }

                boss.AttackTimer = Math.Max(.85, 1.45 - tempo);
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

        game.Audio.Play(BossFireCue(boss.Kind));
    }

    private static SoundCue BossFireCue(AlienBossKind kind)
    {
        return kind switch
        {
            AlienBossKind.SludgeMaw => SoundCue.SludgeMawFire,
            AlienBossKind.EyeTyrant => SoundCue.EyeTyrantFire,
            AlienBossKind.BoneBroodmother => SoundCue.BoneBroodmotherFire,
            AlienBossKind.DreadHarvester => SoundCue.DreadHarvesterFire,
            AlienBossKind.SolarWarden => SoundCue.SolarWardenFire,
            _ => SoundCue.VoidLeechFire
        };
    }

    private void AddBossShot(GameEngine game, AlienBoss boss, V2 direction, double speed, uint tint, double lifetime)
    {
        direction = direction.Normalized;

        Shot shot = game.SpawnShot(boss.Position + direction * (boss.Radius * .72), direction * speed, true, lifetime);
        shot.Radius = 5.2;
        shot.BossShot = true;
        shot.Tint = tint;
    }

    private void AddSludgeGlob(GameEngine game, AlienBoss boss, V2 direction)
    {
        direction = direction.Normalized;

        Shot shot = game.SpawnShot(boss.Position + direction * (boss.Radius * .72),
            direction * (155 + boss.Encounter * 3), true, 4.2);
        shot.Radius = 12.5;
        shot.BossShot = true;
        shot.Tint = 0xff86dc45;
        shot.Sludge = true;
        shot.SplitAge = .95 + game.Random.NextDouble() * .35;
        shot.Angle = game.Random.NextDouble() * Math.PI * 2;
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

            Shot fragment = game.SpawnShot(glob.Position + game.RandomDirection() * 5,
                direction * (125 + game.Random.NextDouble() * 45), true, 2.5 + game.Random.NextDouble() * .5);
            fragment.Radius = 4.5 + game.Random.NextDouble() * 1.8;
            fragment.BossShot = true;
            fragment.Tint = game.Random.Next(3) == 0 ? 0xff4f8f2d : 0xff8fe84f;
            fragment.Sludge = true;
            fragment.Angle = game.Random.NextDouble() * Math.PI * 2;
        }

        game.Spark(glob.Position, 0xff9bf25b, 14);
        game.SpawnShockwave(glob.Position, .3, 0xff75cf3d, 38);
        game.Audio.Play(SoundCue.SludgeMawFire, .7);
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

            Shot droplet = game.SpawnShot(origin, direction * (100 + game.Random.NextDouble() * 65), true,
                2.8 + game.Random.NextDouble() * .7);
            droplet.Radius = 3.8 + game.Random.NextDouble() * 3.2;
            droplet.BossShot = true;
            droplet.Tint = game.Random.Next(4) switch { 0 => 0xffb7f36a, 1 => 0xff46762a, _ => 0xff77c93f };
            droplet.Sludge = true;
            droplet.SludgeVomit = true;
            droplet.Angle = game.Random.NextDouble() * Math.PI * 2;
        }

        game.Spark(boss.Position + aim * (boss.Radius * .7), 0xffa8ef62, 20);
        game.Audio.Play(SoundCue.SludgeMawFire);
    }

    internal void ApplyGravity(GameEngine game, Body body, double dt)
    {
        for (int i = 0; i < game.Vortices.Count; i++)
        {
            GravityVortex vortex = game.Vortices[i];
            if (!vortex.Alive)
            {
                continue;
            }

            V2 delta = game.ArenaDelta(body.Position, vortex.Position);
            double d2 = Math.Max(1400, delta.LengthSquared);
            body.Velocity += delta.Normalized * (4_200_000 / d2 * dt);
        }
    }

    internal void ApplyPlayerGravity(GameEngine game, double dt)
    {
        for (int i = 0; i < game.Vortices.Count; i++)
        {
            GravityVortex vortex = game.Vortices[i];
            if (!vortex.Alive)
            {
                continue;
            }

            V2 delta = game.ArenaDelta(game.Player.Position, vortex.Position);
            double d2 = Math.Max(2200, delta.LengthSquared);
            game.Player.Velocity += delta.Normalized * (GameEngine.PlayerVortexGravity / d2 * dt);
        }
    }
}
