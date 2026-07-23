using MaelstromEventHorizon.Domain.Effects;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Application.Services.Progression;

internal sealed class ScoreTransitionService
{
    internal void AddScore(GameEngine game, int basePoints)
    {
        game.WaveBaseCash += basePoints;
    }

    internal void AwardImmediateScore(GameEngine game, int amount, V2 position)
    {
        BankScore(game, amount);
        game.FloatingTexts.Add(new FloatingText(position, $"+${amount:N0}", 0xffffd76a));
    }

    internal void AddCometCash(GameEngine game, int amount)
    {
        game.WaveCometCash += amount;
    }

    private void BankScore(GameEngine game, int amount)
    {
        if (amount <= 0) return;
        game.Score += amount;
        while (game.Score >= game.NextLifeScore)
        {
            game.Lives++;
            game.NextLifeScore += 50_000;
            game.ShowBanner("EXTRA SHIP", 2);
            game.Audio.Play(SoundCue.Life);
        }
    }

    internal void EnsureLuckyWaveEvents(GameEngine game)
    {
        if (game.BonusSpawnsDisabled) return;
        if (game is { CanisterSpawned: false, CanisterTimer: <= 0 }) game.CanisterTimer = .8 + game.Random.NextDouble() * 2.8;
        if (game is { MultiplierSpawned: false, MultiplierTimer: <= 0 }) game.MultiplierTimer = 1.1 + game.Random.NextDouble() * 3.0;
        if (game is { CometSpawned: false, CometTimer: <= 0 }) game.CometTimer = 1.4 + game.Random.NextDouble() * 3.2;
    }

    private void BeginWaveSummary(GameEngine game)
    {
        if (game.Mode != GameMode.WaveOutro) return;
        ClearCompletedWaveObjects(game);
        game.SummaryBaseCash = game.WaveBaseCash;
        game.SummaryCometCash = game.WaveCometCash;
        game.SummaryLevelBonusCash = game.LevelBonusCash;
        game.SummaryMultiplier = game.Multiplier;
        game.SummaryTotalCash = game.SummaryBaseCash + game.SummaryLevelBonusCash + game.SummaryCometCash * game.SummaryMultiplier;
        game.SummaryDeposited = 0;
        game.SummaryElapsed = 0;
        game.SummaryScreenElapsed = 0;
        game.CashTickCooldown = 0;
        game.CashConfettiTime = game.SummaryTotalCash > 10_000 ? 2.7 : 0;
        game.TransitionAlpha = 1;
        game.Mode = GameMode.WaveSummary;
        if (game.CashConfettiTime > 0) game.Audio.Play(SoundCue.CashBonus, .9);
    }

    private void ClearCompletedWaveObjects(GameEngine game)
    {
        game.Pickups.Clear();
        game.Comets.Clear();
        game.Bosses.Clear();
        game.FloatingTexts.Clear();
        game.Shots.Clear();
        game.Mines.Clear();
        game.Vortices.Clear();
        game.Novas.Clear();
    }

    internal void UpdateWaveSummary(GameEngine game, double dt)
    {
        game.SummaryScreenElapsed += dt;
        game.TransitionAlpha = Math.Clamp(1 - game.SummaryScreenElapsed / GameEngine.SummaryFadeInDuration, 0, 1);
        if (game.SummaryScreenElapsed < GameEngine.SummaryFadeInDuration) return;

        if (game.SummaryComplete) return;

        game.SummaryElapsed += dt;
        game.CashTickCooldown -= dt;
        double duration = Math.Clamp(1.6 + game.SummaryTotalCash / 7000.0, 1.6, 4.2);
        double progress = Math.Clamp(game.SummaryElapsed / duration, 0, 1);
        double eased = 1 - Math.Pow(1 - progress, 3);
        int targetDeposit = progress >= 1 ? game.SummaryTotalCash : (int)Math.Round(game.SummaryTotalCash * eased);
        int delta = targetDeposit - game.SummaryDeposited;
        if (delta > 0)
        {
            BankScore(game, delta);
            game.SummaryDeposited += delta;
            if (game.CashTickCooldown <= 0)
            {
                game.Audio.Play(SoundCue.CashRegister, .48);
                game.CashTickCooldown = .075;
            }
        }
    }

    internal void UpdateLevelBonus(GameEngine game, double dt)
    {
        if (game.LevelBonusCash <= 0) return;
        game.LevelBonusCountdown -= dt;
        while (game is { LevelBonusCountdown: <= 0, LevelBonusCash: > 0 })
        {
            game.LevelBonusCash = Math.Max(0, game.LevelBonusCash - 50);
            game.LevelBonusCountdown += 5;
            if (game is { LevelBonusCash: <= 1_000, BonusSpawnsDisabled: false }) DisableBonusSpawns(game);
        }
    }

    private void DisableBonusSpawns(GameEngine game)
    {
        game.BonusSpawnsDisabled = true;
        game.CanisterTimer = -1;
        game.MultiplierTimer = -1;
        game.CometTimer = -1;
        game.RescueTimer = -1;
        game.CanisterStormRemaining = 0;
        game.CometStormRemaining = 0;
        game.CanisterStormWave = false;
        game.CometStormWave = false;
    }

    internal void CompleteWaveSummary(GameEngine game)
    {
        int remaining = game.SummaryTotalCash - game.SummaryDeposited;
        if (remaining > 0)
        {
            BankScore(game, remaining);
            game.SummaryDeposited += remaining;
            game.Audio.Play(SoundCue.CashRegister, .65);
        }
    }

    internal void BeginWaveOutro(GameEngine game)
    {
        if (game.Mode != GameMode.Playing) return;
        if (game.IsBonusStage || game.IsBossStage) game.Audio.StopMusic(false);
        game.Mode = GameMode.WaveOutro;
        game.TransitionElapsed = 0;
        game.TransitionAlpha = 0;
        game.Player.Thrusting = false;
        game.Player.Shielding = false;
    }

    internal void UpdateWaveOutro(GameEngine game, double dt)
    {
        game.TransitionElapsed += dt;
        game.TransitionAlpha = Math.Clamp(game.TransitionElapsed / GameEngine.FadeToSummaryDuration, 0, 1);
        if (game.TransitionElapsed < GameEngine.FadeToSummaryDuration) return;

        if (game.BonusOnlyMode || game.BossOnlyMode)
        {
            ClearCompletedWaveObjects(game);
            game.BeginNextWave();
            game.Mode = GameMode.WaveIntro;
            game.TransitionElapsed = 0;
            game.TransitionAlpha = 1;
            return;
        }

        BeginWaveSummary(game);
    }

    internal void BeginWaveSummaryExit(GameEngine game)
    {
        game.Mode = GameMode.WaveSummaryExit;
        game.TransitionElapsed = 0;
        game.TransitionAlpha = 0;
    }

    internal void UpdateWaveSummaryExit(GameEngine game, double dt)
    {
        game.TransitionElapsed += dt;
        game.TransitionAlpha = Math.Clamp(game.TransitionElapsed / GameEngine.FadeToWaveDuration, 0, 1);
        if (game.TransitionElapsed < GameEngine.FadeToWaveDuration) return;

        game.BeginNextWave();
        game.Mode = GameMode.WaveIntro;
        game.TransitionElapsed = 0;
        game.TransitionAlpha = 1;
    }

    internal void UpdateWaveIntro(GameEngine game, double dt)
    {
        game.TransitionElapsed += dt;
        game.TransitionAlpha = Math.Clamp(1 - game.TransitionElapsed / GameEngine.WaveFadeInDuration, 0, 1);
        if (game.TransitionElapsed < GameEngine.WaveFadeInDuration) return;

        game.TransitionAlpha = 0;
        game.Mode = GameMode.Playing;
    }

    internal void Hyperspace(GameEngine game)
    {
        if (game.PlayerRespawning || game.Player.Invulnerable > 1) return;
        game.Explosion(game.Player.Position, 10, 0xff68d9ff);
        game.Player.Position = game.SafePosition(0);
        game.Player.Velocity *= .25;
        game.Player.Invulnerable = 1.2;
        if (game.Random.NextDouble() < .08 && !game.LuckActive) game.DamagePlayer();
        else game.Shockwaves.Add(new Shockwave(game.Player.Position, .35, 0xff82ddff, 75));
    }

    internal void CenterPlayerWithShield(GameEngine game)
    {
        game.Player.Position = new V2(GameEngine.Width / 2, GameEngine.Height / 2);
        game.Player.Velocity = V2.Zero;
        game.Player.Angle = 0;
        game.Player.Invulnerable = 3;
        game.Player.SpawnShieldTime = 3;
        game.Player.Thrusting = false;
        game.Player.Shielding = false;
        game.ShieldReleaseTimer = 0;
        game.ShieldImpactTime = 0;
        game.TurnVelocity = 0;
        game.ThrustRamp = 0;
    }
}
