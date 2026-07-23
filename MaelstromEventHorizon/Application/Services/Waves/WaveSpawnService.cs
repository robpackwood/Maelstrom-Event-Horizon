using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Application.Services.Waves;

internal sealed class WaveSpawnService
{
    internal void BeginNextWave(GameEngine game)
    {
        game.Mode = GameMode.Playing;
        game.CashConfettiTime = 0;
        game.RicochetArenaActive = false;
        game.CenterPlayerWithShield();
        game.Wave = game.BonusOnlyMode
            ? (game.Wave < 5 ? 5 : game.Wave + 5)
            : game.BossOnlyMode ? (game.Wave < 6 ? 6 : game.Wave + 5) : game.Wave + 1;
        game.IsBonusStage = game.BonusOnlyMode || !game.BossOnlyMode && game.Wave % 5 == 0;
        game.IsBossStage = game.BossOnlyMode || game is { BonusOnlyMode: false, Wave: > 1 } && game.Wave % 5 == 1;
        bool standardWave = game is { IsBonusStage: false, IsBossStage: false };
        game.BonusStageFailed = false;
        game.RespawnTimer = 0;
        game.FighterSpawnedThisWave = false;
        game.BonusTravelTime = 0;
        game.BonusAsteroidsDodged = 0;
        game.WaveBaseCash = 0;
        game.WaveCometCash = 0;
        game.Multiplier = 1;
        game.CanisterSpawned = false;
        game.MultiplierSpawned = false;
        game.CometSpawned = false;
        game.BlackHoleSpawned = false;
        game.BonusSpawnsDisabled = false;
        game.LevelBonusCash = 2_000;
        game.LevelBonusCountdown = 5;
        game.CanisterStormRemaining = 0;
        game.CometStormRemaining = 0;
        game.CanisterStormWave = standardWave && game.Random.NextDouble() < .075;
        game.CometStormWave = standardWave && game.Random.NextDouble() < .075;
        bool lucky = game.LuckActive;
        game.CanisterTimer = standardWave && (game.CanisterStormWave || lucky || game.Random.Next(3) == 0) ? 2.5 + game.Random.NextDouble() * 7.5 : -1;
        game.MultiplierTimer = standardWave && (lucky || game.Random.Next(3) == 0) ? 3 + game.Random.NextDouble() * 7.5 : -1;
        game.CometTimer = standardWave && (game.CometStormWave || lucky || game.Random.Next(3) == 0) ? 3.5 + game.Random.NextDouble() * 7.5 : -1;
        game.BlackHoleTimer = standardWave && game.Wave > 1 && game.Random.NextDouble() < .125 ? 3 + game.Random.NextDouble() * 8 : -1;
        game.BonusAsteroidTotal = 0;
        game.BonusAsteroidsDodged = 0;
        game.BonusAsteroidsRemaining = 0;
        if (game.IsBonusStage)
        {
            int bonusStageNumber = game.Wave / 5;
            game.BonusStageVariant = (BonusStageKind)((bonusStageNumber - 1) % Enum.GetValues<BonusStageKind>().Length);
            game.BonusAsteroidTotal = game.BonusStageVariant switch
            {
                BonusStageKind.SlalomGates => Math.Min(40, 22 + bonusStageNumber * 2),
                BonusStageKind.SpiralSwarm => Math.Min(34, 14 + bonusStageNumber * 2),
                _ => Math.Min(40, 16 + bonusStageNumber * 2)
            };
            game.BonusAsteroidsRemaining = game.BonusAsteroidTotal;
            game.BonusPatternStep = 0;
            game.BonusAsteroidSpawnTimer = game.BonusStageVariant == BonusStageKind.SlalomGates ? 1.6 : 1.25;
            game.EventTimer = double.MaxValue;
            game.RescueTimer = -1;
            game.Player.Shielding = false;
            game.Player.SpawnShieldTime = 0;
            game.Player.Invulnerable = 0;
            game.ShieldReleaseTimer = 0;
            game.Shots.RemoveAll(shot => !shot.Enemy);
        }
        else if (game.IsBossStage)
        {
            game.EventTimer = double.MaxValue;
            game.RescueTimer = -1;
            SpawnAlienBoss(game);
        }
        else
        {
            int normalRocks = 3 + (game.Wave - 1) / 2;
            int rocks = normalRocks;
            for (int i = 0; i < rocks; i++)
            {
                V2 pos = game.SafeEdgePosition();
                bool steel = game.Wave >= 4 && i == normalRocks - 1 && game.Wave % 3 == 1;
                game.Asteroids.Add(new Asteroid(pos, game.RandomDirection() * game.Random.Next(32, 78 + game.Wave * 3), 3, steel, game.Random.Next()));
            }
            if (game.Wave >= 2 && game.Wave % 2 == 0) SpawnFighter(game);
            game.EventTimer = 6 + game.Random.NextDouble() * 4;
            double rescueChance = Math.Min(.30, .12 + game.Wave * .008 + (game.LuckActive ? .10 : 0));
            game.RescueTimer = game.Random.NextDouble() < rescueChance ? 5 + game.Random.NextDouble() * 10 : -1;
        }
        game.NextWaveTimer = 0;
        game.Audio.StartWaveMusic(game.Wave, game.IsBonusStage || game.IsBossStage);
        if (game.IsBossStage)
        {
            AlienBoss boss = game.Bosses[0];
            game.ShowBanner(game.BossOnlyMode
                ? $"BOSS ONLY - {game.BossName(boss.Kind)} - SCORES DISABLED"
                : $"WARNING - {game.BossName(boss.Kind)}", 3.2);
            game.Audio.Play(SoundCue.BossAlarm);
        }
        else
        {
            string banner = game.BonusOnlyMode
                ? $"BONUS ONLY - {game.BonusStageName} - SCORES DISABLED"
                : game.IsBonusStage ? $"BONUS - {game.BonusStageName} - DODGE ONLY" : $"WAVE {game.Wave}";
            game.ShowBanner(banner, game.IsBonusStage ? 4 : 2.2);
            game.Audio.Play(SoundCue.Wave, .8);
        }
    }

    private void SpawnAlienBoss(GameEngine game)
    {
        int encounter = Math.Max(1, (game.Wave - 1) / 5);
        AlienBossKind kind = (AlienBossKind)((encounter - 1) % 4);
        V2 position = new(GameEngine.Width * .78, GameEngine.Height * (.28 + game.Random.NextDouble() * .34));
        game.Bosses.Add(new AlienBoss(position, kind, encounter));
    }

    internal void UpdateBonusAsteroidStream(GameEngine game, double dt)
    {
        if (game.BonusAsteroidsRemaining <= 0) return;
        game.BonusAsteroidSpawnTimer -= dt;
        if (game.BonusAsteroidSpawnTimer > 0) return;

        int bonusStageNumber = game.Wave / 5;
        int difficulty = Math.Min(7, 1 + (bonusStageNumber - 1) / 2);
        switch (game.BonusStageVariant)
        {
            case BonusStageKind.DiagonalStorm: SpawnDiagonalStorm(game, difficulty); break;
            case BonusStageKind.Crossfire: SpawnCrossfire(game, difficulty); break;
            case BonusStageKind.SlalomGates: SpawnSlalomGate(game, difficulty); break;
            case BonusStageKind.SpiralSwarm: SpawnSpiralSwarm(game, difficulty); break;
        }
        game.BonusPatternStep++;
    }

    private void SpawnDiagonalStorm(GameEngine game, int difficulty)
    {
        bool aimed = game.BonusPatternStep % 3 == 1;
        V2 origin = new(GameEngine.Width + 60 + game.Random.NextDouble() * 100, -70 + game.Random.NextDouble() * 155);
        V2 target = aimed
            ? game.Player.Position + game.Player.Velocity * .38 + new V2(game.Random.Next(-28, 29), game.Random.Next(-28, 29))
            : new V2(-120, 90 + game.BonusPatternStep % Math.Clamp(6 + difficulty, 7, 11) * 72);
        SpawnBonusRock(game, origin, target, 302 + difficulty * 18 + game.Random.NextDouble() * 62, 3);
        game.BonusAsteroidSpawnTimer = Math.Max(.42, .74 - difficulty * .025) + game.Random.NextDouble() * .16;
    }

    private void SpawnCrossfire(GameEngine game, int difficulty)
    {
        int side = game.BonusPatternStep % 4;
        double edgeX = 80 + game.Random.NextDouble() * (GameEngine.Width - 160);
        double edgeY = 75 + game.Random.NextDouble() * (GameEngine.Height - 150);
        V2 origin = side switch
        {
            0 => new V2(-72, edgeY),
            1 => new V2(GameEngine.Width + 72, edgeY),
            2 => new V2(edgeX, -72),
            _ => new V2(edgeX, GameEngine.Height + 72)
        };
        V2 target;
        if (game.BonusPatternStep % 3 == 0)
        {
            V2 aim = game.Player.Position + game.Player.Velocity * .3 + game.RandomDirection() * game.Random.Next(8, 42);
            target = origin + (aim - origin).Normalized * 1700;
        }
        else target = side switch
        {
            0 => new V2(GameEngine.Width + 100, GameEngine.Height - edgeY),
            1 => new V2(-100, GameEngine.Height - edgeY),
            2 => new V2(GameEngine.Width - edgeX, GameEngine.Height + 100),
            _ => new V2(GameEngine.Width - edgeX, -100)
        };
        SpawnBonusRock(game, origin, target, 300 + difficulty * 15 + game.Random.NextDouble() * 56, game.BonusPatternStep % 4 == 2 ? 2 : 3);
        game.BonusAsteroidSpawnTimer = Math.Max(.4, .66 - difficulty * .02) + game.Random.NextDouble() * .15;
    }

    private void SpawnSlalomGate(GameEngine game, int difficulty)
    {
        int lanes = Math.Clamp(7 + (difficulty - 1) / 3, 7, 9);
        int gapPositions = lanes - 1;
        int gapCycle = Math.Max(1, gapPositions * 2 - 2);
        int gapStep = (game.BonusPatternStep + difficulty) % gapCycle;
        int gap = gapStep < gapPositions ? gapStep : gapCycle - gapStep;
        int spawned = 0;
        double speed = 220 + difficulty * 10;
        for (int lane = 0; lane < lanes && game.BonusAsteroidsRemaining > 0; lane++)
        {
            if (lane == gap || lane == gap + 1) continue;
            double y = 48 + lane / (double)(lanes - 1) * (GameEngine.Height - 96);
            V2 origin = new(GameEngine.Width + 78 + spawned * 5, y);
            V2 target = new(-95, y + Math.Sin(game.BonusPatternStep * .72 + lane) * (12 + difficulty * 3));
            SpawnBonusRock(game, origin, target, speed + game.Random.NextDouble() * 22, 2);
            spawned++;
        }
        game.BonusAsteroidSpawnTimer = Math.Max(1.35, 1.78 - difficulty * .04);
    }

    private void SpawnSpiralSwarm(GameEngine game, int difficulty)
    {
        double angle = game.BonusPatternStep * 2.399 + difficulty * .31;
        V2 radial = V2.FromAngle(angle);
        V2 tangent = new(-radial.Y, radial.X);
        V2 center = new(GameEngine.Width / 2, GameEngine.Height / 2);
        V2 origin = center + radial * 790;
        double speed = 235 + difficulty * 10 + game.Random.NextDouble() * 32;
        V2 velocity = -radial * speed + tangent * (44 + difficulty * 2);
        var asteroid = new Asteroid(origin, velocity, game.BonusPatternStep % 5 == 0 ? 3 : 2, true, game.Random.Next(), true)
        {
            BonusCurve = (game.BonusPatternStep % 2 == 0 ? 1 : -1) * (.1 + difficulty * .005)
        };
        game.Asteroids.Add(asteroid);
        game.BonusAsteroidsRemaining--;
        game.BonusAsteroidSpawnTimer = Math.Max(.62, .86 - difficulty * .02) + game.Random.NextDouble() * .18;
        if (game.BonusPatternStep % 5 == 4) game.BonusAsteroidSpawnTimer += .35;
    }

    private void SpawnBonusRock(GameEngine game, V2 origin, V2 target, double speed, int size)
    {
        V2 direction = (target - origin).Normalized;
        game.Asteroids.Add(new Asteroid(origin, direction * speed, size, true, game.Random.Next(), true));
        game.BonusAsteroidsRemaining--;
    }

    internal void SpawnFighter(GameEngine game)
    {
        if (game.Fighters.Any(f => f.Alive)) return;
        if (game.FighterSpawnedThisWave) return;
        game.FighterSpawnedThisWave = true;
        FighterKind kind = game.Wave >= 3 && game.Random.Next(5 + game.Wave) > 6 ? FighterKind.Interceptor : FighterKind.Raider;
        V2 pos = game.SafeEdgePosition();
        game.Fighters.Add(new Fighter(pos, game.RandomDirection() * 80, kind));
        game.Audio.Play(SoundCue.EnemyWarning, .68);
    }

    internal void SpawnMine(GameEngine game)
    {
        game.Mines.Add(new HomingMine(game.SafeEdgePosition(), game.RandomDirection() * 75));
        game.ShowBanner("HOMING MINE", 1.4);
    }

    internal void SpawnVortex(GameEngine game)
    {
        if (game.BlackHoleSpawned) return;
        game.BlackHoleSpawned = true;
        game.BlackHoleTimer = -1;
        game.Vortices.Add(new GravityVortex(game.SafePosition(250)));
        game.ShowBanner("BLACK HOLE", 1.6);
    }

    internal void SpawnNova(GameEngine game)
    {
        game.Novas.Add(new Nova(game.SafePosition(300)));
        game.ShowBanner("NOVA DETECTED", 1.8);
    }

    internal void SpawnCanister(GameEngine game)
    {
        if (game.BonusSpawnsDisabled) return;
        if (game.CanisterSpawned) return;
        game.CanisterSpawned = true;
        game.CanisterTimer = -1;

        if (game.CanisterStormWave)
        {
            game.CanisterStormRemaining = game.Random.Next(5, 11);
            game.ShowBanner("ITEM BOX STORM", 1.8);
            SpawnCanisterEntity(game);
            game.CanisterStormRemaining--;
            game.CanisterStormSpawnTimer = .5;
            return;
        }

        SpawnCanisterEntity(game);
    }

    internal void SpawnCanisterEntity(GameEngine game)
    {
        if (game.BonusSpawnsDisabled) return;
        V2 position = game.SafeEdgePosition();
        V2 destination = new(GameEngine.Width * (.3 + game.Random.NextDouble() * .4), GameEngine.Height * (.28 + game.Random.NextDouble() * .44));
        V2 velocity = (destination - position).Normalized * game.Random.Next(48, 82) + game.RandomDirection() * 15;
        game.Pickups.Add(new Pickup(position, velocity, PickupKind.Canister));
    }

    internal void SpawnComet(GameEngine game)
    {
        if (game.BonusSpawnsDisabled) return;
        if (game.CometSpawned) return;
        game.CometSpawned = true;
        game.CometTimer = -1;

        if (game.CometStormWave)
        {
            game.CometStormRemaining = game.Random.Next(5, 11);
            game.ShowBanner("COMET STORM", 1.8);
            SpawnCometEntity(game);
            game.CometStormRemaining--;
            game.CometStormSpawnTimer = .55;
            return;
        }

        SpawnCometEntity(game);
        game.ShowBanner("BONUS COMET", 1.25);
    }

    internal void SpawnCometEntity(GameEngine game)
    {
        if (game.BonusSpawnsDisabled) return;
        bool fromLeft = game.Random.Next(2) == 0;
        V2 position = new(fromLeft ? 1 : GameEngine.Width - 1, 100 + game.Random.NextDouble() * (GameEngine.Height - 200));
        V2 target = new(fromLeft ? GameEngine.Width + 100 : -100, 90 + game.Random.NextDouble() * (GameEngine.Height - 180));
        int value = GameEngine.CometValues[game.Random.Next(GameEngine.CometValues.Length)];
        uint tint = value switch
        {
            >= 5000 => 0xffff6e9e,
            >= 3000 => 0xffc977ff,
            >= 2000 => 0xff62dcff,
            >= 1000 => 0xff63f0ca,
            _ => 0xffffc65b
        };
        game.Comets.Add(new Comet(position, (target - position).Normalized * game.Random.Next(310, 430), value, tint));
    }

    internal void SpawnMultiplier(GameEngine game)
    {
        if (game.BonusSpawnsDisabled) return;
        if (game.MultiplierSpawned) return;
        game.MultiplierSpawned = true;
        game.MultiplierTimer = -1;
        game.Pickups.Add(new Pickup(game.SafePosition(180), V2.Zero, PickupKind.Multiplier, game.Random.Next(2, 6)));
    }

    internal void SpawnBonusPickup(GameEngine game, V2? at = null)
    {
        if (game.BonusSpawnsDisabled) return;
        int value = game.Random.Next(6) == 0 ? 500 : game.Random.Next(1, 6) * 1000;
        game.Pickups.Add(new Pickup(at ?? game.SafeEdgePosition(), game.RandomDirection() * 45, PickupKind.Bonus, value));
    }

    internal void SpawnRescueShip(GameEngine game)
    {
        if (game.BonusSpawnsDisabled) return;
        if (game.Pickups.Any(p => p is { Alive: true, Kind: PickupKind.RescueShip })) return;
        V2 position = game.Random.Next(4) switch
        {
            0 => new V2(game.Random.NextDouble() * GameEngine.Width, -38),
            1 => new V2(GameEngine.Width + 38, game.Random.NextDouble() * GameEngine.Height),
            2 => new V2(game.Random.NextDouble() * GameEngine.Width, GameEngine.Height + 38),
            _ => new V2(-38, game.Random.NextDouble() * GameEngine.Height)
        };
        V2 destination = new(GameEngine.Width * (.3 + game.Random.NextDouble() * .4), GameEngine.Height * (.28 + game.Random.NextDouble() * .44));
        var rescue = new Pickup(position, (destination - position).Normalized * game.Random.Next(78, 108), PickupKind.RescueShip)
        {
            Angle = game.Random.NextDouble() * Math.PI * 2
        };
        game.Pickups.Add(rescue);
        game.ShowBanner("RESCUE SHIP", 1.7);
    }

    internal void RollDrop(GameEngine game, V2 at, double chance = .09)
    {
        if (game.Random.NextDouble() < chance * .35) SpawnBonusPickup(game, at);
    }
}
