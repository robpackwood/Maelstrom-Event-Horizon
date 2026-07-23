namespace MaelstromEventHorizon.Application.Services.Waves;

internal sealed class WaveEventService
{
    internal void ScheduleEvents(GameEngine game, double dt)
    {
        if (game.IsBonusStage)
        {
            game.UpdateBonusAsteroidStream(dt);
            bool bonusClear = game.BonusAsteroidsRemaining == 0 && game.Asteroids.All(asteroid => !asteroid.Alive);
            if (bonusClear)
            {
                game.NextWaveTimer += dt;
                if (game.NextWaveTimer > 1.6) game.BeginWaveOutro();
            }
            else game.NextWaveTimer = 0;
            return;
        }

        if (game.IsBossStage)
        {
            bool bossClear = game.Bosses.All(boss => !boss.Alive);
            if (bossClear)
            {
                game.NextWaveTimer += dt;
                if (game.NextWaveTimer > 1.8) game.BeginWaveOutro();
            }
            else game.NextWaveTimer = 0;
            return;
        }

        if (game.RescueTimer > 0)
        {
            game.RescueTimer -= dt;
            if (game.RescueTimer <= 0)
            {
                game.SpawnRescueShip();
                game.RescueTimer = -1;
            }
        }

        if (game.CanisterTimer > 0)
        {
            game.CanisterTimer -= dt;
            if (game.CanisterTimer <= 0) game.SpawnCanister();
        }

        if (game.MultiplierTimer > 0)
        {
            game.MultiplierTimer -= dt;
            if (game.MultiplierTimer <= 0) game.SpawnMultiplier();
        }

        if (game.CometTimer > 0)
        {
            game.CometTimer -= dt;
            if (game.CometTimer <= 0) game.SpawnComet();
        }

        if (game.BlackHoleTimer > 0)
        {
            game.BlackHoleTimer -= dt;
            if (game.BlackHoleTimer <= 0) game.SpawnVortex();
        }

        if (game.CanisterStormRemaining > 0)
        {
            game.CanisterStormSpawnTimer -= dt;
            if (game.CanisterStormSpawnTimer <= 0)
            {
                game.SpawnCanisterEntity();
                game.CanisterStormRemaining--;
                game.CanisterStormSpawnTimer = .42 + game.Random.NextDouble() * .34;
            }
        }

        if (game.CometStormRemaining > 0)
        {
            game.CometStormSpawnTimer -= dt;
            if (game.CometStormSpawnTimer <= 0)
            {
                game.SpawnCometEntity();
                game.CometStormRemaining--;
                game.CometStormSpawnTimer = .48 + game.Random.NextDouble() * .38;
            }
        }

        game.EventTimer -= dt;
        if (game.EventTimer <= 0)
        {
            game.EventTimer = Math.Max(4.5, 10.5 - game.Wave * .28) + game.Random.NextDouble() * 5;
            int roll = game.Random.Next(100);
            if (roll < 31) game.SpawnFighter();
            else if (roll < 49 && game.Wave >= 2) game.SpawnMine();
            else if (roll < 63 && game.Wave >= 4) game.SpawnNova();
            else if (roll < 76) game.SpawnBonusPickup();
            else if (roll < 90) game.SpawnBonusPickup();
            else game.SpawnFighter();
        }

        bool pendingStorm = game is { CanisterStormWave: true, CanisterSpawned: false } || game is { CometStormWave: true, CometSpawned: false } ||
            game.CanisterStormRemaining > 0 || game.CometStormRemaining > 0;
        bool waveClear = !game.PlayerRespawning && game.Asteroids.All(a => !a.Alive) && game.Fighters.All(f => !f.Alive) &&
            game.Vortices.All(v => !v.Alive) && !pendingStorm && game.BlackHoleTimer <= 0;
        if (waveClear)
        {
            game.NextWaveTimer += dt;
            if (game.NextWaveTimer > 1.6) game.BeginWaveOutro();
        }
        else game.NextWaveTimer = 0;
    }
}
