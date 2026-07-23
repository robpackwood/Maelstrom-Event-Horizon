using System.Windows.Input;
using MaelstromEventHorizon.Application.Input;
using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;
using MaelstromEventHorizon.Domain.Scores;

namespace MaelstromEventHorizon.Application.Services.Input;

internal sealed class GameInputService
{
    private void StartGame(GameEngine game, bool bonusOnly = false, bool bossOnly = false)
    {
        game.IsDemoMode = false;
        game.BonusOnlyMode = bonusOnly;
        game.BossOnlyMode = bossOnly;
        game.HighlightedHighScore = null;
        game.TitleSecretBuffer = "";
        game.TitleIdleTime = 0;
        game.ClearWorld();
        game.Score = 0;
        game.Wave = 0;
        game.Lives = 3;
        game.Multiplier = 1;
        game.NextLifeScore = 50_000;
        game.Player = new Ship(new V2(GameEngine.Width / 2, GameEngine.Height / 2));
        game.TurnVelocity = 0;
        game.ThrustRamp = 0;
        game.Mode = GameMode.Playing;
        game.BeginNextWave();
    }

    internal void StartDemo(GameEngine game)
    {
        StartGame(game);
        game.IsDemoMode = true;
        game.DemoElapsed = 0;
        game.DemoFireCooldown = 0;
        game.DemoStage = 0;
        game.DemoPowerupCollected = false;
        game.DemoEnemyDestroyed = false;
        game.DemoBlackHoleDestroyed = false;
        game.EventTimer = double.MaxValue;
        game.CanisterTimer = -1;
        game.MultiplierTimer = -1;
        game.CometTimer = -1;
        game.BlackHoleTimer = -1;
        game.RescueTimer = -1;
        game.CanisterStormRemaining = 0;
        game.CometStormRemaining = 0;
        game.Pickups.Add(new Pickup(new V2(900, GameEngine.Height / 2), V2.Zero, PickupKind.Canister));
        game.ShowBanner("AUTOPILOT DEMONSTRATION", 2.2);
    }

    internal bool HandleCommandKey(GameEngine game, Key key, bool isRepeat)
    {
        if (game.IsDemoMode)
        {
            ReturnToTitle(game);
            return true;
        }

        if (game.Mode == GameMode.Title)
        {
            game.TitleIdleTime = 0;
        }

        if (game.Mode == GameMode.Title && key == Key.Escape)
        {
            System.Windows.Application.Current.Shutdown();
            return true;
        }

        if (game.Mode == GameMode.GameOverDelay)
        {
            return true;
        }

        if (game.Mode == GameMode.Controls)
        {
            if (game.WaitingForBinding)
            {
                if (key == Key.Escape && ControlBindings.Actions[game.ControlSelection] != GameAction.Quit)
                {
                    game.WaitingForBinding = false;
                }
                else
                {
                    game.Bindings.Assign(ControlBindings.Actions[game.ControlSelection], key);
                    game.WaitingForBinding = false;
                }

                return true;
            }

            if (isRepeat)
            {
                return true;
            }

            if (key == Key.Up)
            {
                game.ControlSelection = (game.ControlSelection + ControlBindings.Actions.Length - 1) %
                                        ControlBindings.Actions.Length;

                game.Audio.Play(SoundCue.MenuMove, .72);
            }
            else if (key == Key.Down)
            {
                game.ControlSelection = (game.ControlSelection + 1) % ControlBindings.Actions.Length;
                game.Audio.Play(SoundCue.MenuMove, .72);
            }
            else if (key == Key.Enter)
            {
                game.WaitingForBinding = true;
            }
            else if (key == Key.R)
            {
                game.Bindings.Reset();
            }
            else if (key == Key.Escape)
            {
                game.Mode = GameMode.Title;
            }
            else
            {
                return false;
            }

            return true;
        }

        if (game.Mode == GameMode.QuitConfirm)
        {
            if (isRepeat)
            {
                return true;
            }

            if (key is Key.Enter or Key.Y)
            {
                ReturnToTitle(game);
            }
            else if (key is Key.Escape or Key.N || key == game.Bindings[GameAction.Quit])
            {
                CancelQuitConfirmation(game);
            }

            return true;
        }

        if (game.Mode is not GameMode.NameEntry and not GameMode.Controls && key == game.Bindings[GameAction.Quit])
        {
            if (game.Mode is GameMode.Playing or GameMode.Paused or GameMode.WaveOutro or GameMode.WaveSummary or
                GameMode.WaveSummaryExit or GameMode.WaveIntro or GameMode.GameOver)
            {
                BeginQuitConfirmation(game);
            }

            return true;
        }

        if (game.Mode == GameMode.Title)
        {
            if (key == Key.Up && !isRepeat)
            {
                game.TitleMenuSelection = (game.TitleMenuSelection + GameEngine.TitleMenuItemCount - 1) %
                                          GameEngine.TitleMenuItemCount;

                game.Audio.Play(SoundCue.MenuMove, .78);
            }
            else if (key == Key.Down && !isRepeat)
            {
                game.TitleMenuSelection = (game.TitleMenuSelection + 1) % GameEngine.TitleMenuItemCount;
                game.Audio.Play(SoundCue.MenuMove, .78);
            }
            else if (game.TitleMenuSelection is 2 or 3 && key is Key.Left or Key.Right)
            {
                AdjustTitleVolume(game, game.TitleMenuSelection == 2,
                    key == Key.Right ? GameEngine.VolumeStep : -GameEngine.VolumeStep);
            }
            else if (game.TitleMenuSelection == 4 && !isRepeat && key is Key.Space or Key.Left or Key.Right)
            {
                ToggleFullScreen(game);
            }
            else if (key == Key.Enter && !isRepeat)
            {
                if (game.TitleMenuSelection == 0)
                {
                    StartGame(game);
                }
                else if (game.TitleMenuSelection == 1)
                {
                    game.Mode = GameMode.Controls;
                }
                else if (game.TitleMenuSelection == 4)
                {
                    ToggleFullScreen(game);
                }
                else if (game.TitleMenuSelection == 5)
                {
                    System.Windows.Application.Current.Shutdown();
                }
            }
            else
            {
                return false;
            }

            return true;
        }

        if (isRepeat)
        {
            return false;
        }

        if (game.Mode == GameMode.GameOver && key == Key.Enter)
        {
            ReturnToTitle(game);
            return true;
        }

        if (game.Mode == GameMode.Playing && key == game.Bindings[GameAction.Fire])
        {
            if (game is { IsBonusStage: false, PlayerRespawning: false } &&
                (!game.RapidFireActive || game is { FireCooldown: <= 0, RapidFireReload: <= 0 }))
            {
                game.FirePlayer();
            }

            return true;
        }

        if (game.Mode == GameMode.WaveSummary)
        {
            if (!game.SummaryInputReady)
            {
                return true;
            }

            if (!game.SummaryComplete)
            {
                game.CompleteWaveSummary();
            }

            game.BeginWaveSummaryExit();
            return true;
        }

        if (game.Mode is GameMode.WaveOutro or GameMode.WaveSummaryExit or GameMode.WaveIntro)
        {
            return true;
        }

        if (game.Mode is GameMode.Playing or GameMode.Paused && key == game.Bindings[GameAction.Pause])
        {
            if (game.Mode == GameMode.Playing)
            {
                game.Mode = GameMode.Paused;
                game.Audio.PauseAll();
            }
            else
            {
                game.Mode = GameMode.Playing;
                game.Audio.ResumeAll();
            }

            return true;
        }

        if (game.Mode == GameMode.Playing && key == game.Bindings[GameAction.Hyperspace])
        {
            game.Hyperspace();
            return true;
        }

        return false;
    }


    private void ToggleFullScreen(GameEngine game)
    {
        game.FullScreenEnabled = !game.FullScreenEnabled;
        SavePreferences(game);
        game.RaiseFullScreenChanged(game.FullScreenEnabled);
    }

    private void AdjustTitleVolume(GameEngine game, bool music, double amount)
    {
        if (music)
        {
            game.MusicVolume = Math.Round(Math.Clamp(game.MusicVolume + amount, 0, 1), 2);
        }
        else
        {
            game.EffectsVolume = Math.Round(Math.Clamp(game.EffectsVolume + amount, 0, 1), 2);
        }

        game.Audio.SetVolumes(game.MusicVolume, game.EffectsVolume);
        SavePreferences(game);

        if (!music)
        {
            game.Audio.Play(SoundCue.Pickup, .55);
        }
    }

    private void SavePreferences(GameEngine game)
    {
        game.DisplaySettingsStore.Save(new DisplayPreferences
        {
            FullScreen = game.FullScreenEnabled,
            MusicVolume = game.MusicVolume,
            EffectsVolume = game.EffectsVolume
        });
    }

    private void BeginQuitConfirmation(GameEngine game)
    {
        game.ModeBeforeQuitConfirmation = game.Mode;
        game.Mode = GameMode.QuitConfirm;
        game.Audio.PauseAll();
    }

    private void CancelQuitConfirmation(GameEngine game)
    {
        game.Mode = game.ModeBeforeQuitConfirmation;

        if (game.Mode != GameMode.Paused)
        {
            game.Audio.ResumeAll();
        }
    }

    internal void ReturnToTitle(GameEngine game)
    {
        game.Audio.StopMusic();
        game.IsDemoMode = false;
        game.BonusOnlyMode = false;
        game.BossOnlyMode = false;
        game.TitleSecretBuffer = "";
        game.TitleIdleTime = 0;
        game.DemoElapsed = 0;
        game.ClearWorld();
        game.Player = new Ship(new V2(GameEngine.Width / 2, GameEngine.Height / 2));
        game.TurnVelocity = 0;
        game.ThrustRamp = 0;
        game.TitleMenuSelection = 0;
        game.WaitingForBinding = false;
        game.TransitionAlpha = 0;
        game.Mode = GameMode.Title;
        game.Audio.StartTitleMusic();
    }

    internal void HandleTextInput(GameEngine game, string text)
    {
        if (game.Mode == GameMode.Title)
        {
            foreach (char c in text.ToUpperInvariant())
            {
                if (!char.IsLetter(c))
                {
                    game.TitleSecretBuffer = "";
                    continue;
                }

                game.TitleSecretBuffer += c;

                if (game.TitleSecretBuffer.Length > 5)
                {
                    game.TitleSecretBuffer = game.TitleSecretBuffer[^5..];
                }

                if (game.TitleSecretBuffer.EndsWith("BONUS", StringComparison.Ordinal))
                {
                    StartGame(game, true);
                }
                else if (game.TitleSecretBuffer.EndsWith("BOSS", StringComparison.Ordinal))
                {
                    StartGame(game, bossOnly: true);
                }
                else
                {
                    continue;
                }

                break;
            }

            return;
        }

        if (game.Mode != GameMode.NameEntry || game.PendingName.Length >= 12)
        {
            return;
        }

        foreach (char c in text.ToUpperInvariant())
        {
            if (game.PendingName.Length >= 12)
            {
                break;
            }

            if (char.IsLetterOrDigit(c) || c is ' ' or '-' or '_')
            {
                game.PendingName += c;
            }
        }
    }

    internal bool HandleNameEntryKey(GameEngine game, Key key)
    {
        if (game.Mode != GameMode.NameEntry || game.BonusOnlyMode || game.BossOnlyMode)
        {
            return false;
        }

        if (key == Key.Back)
        {
            if (game.PendingName.Length > 0)
            {
                game.PendingName = game.PendingName[..^1];
            }

            return true;
        }

        if (key == Key.Enter)
        {
            string name = string.IsNullOrWhiteSpace(game.PendingName) ? "PLAYER" : game.PendingName.Trim();
            HighScoreEntry playerEntry = new(name, game.Score, game.Wave, DateTime.Now);
            game.HighScores.Add(playerEntry);

            List<HighScoreEntry> ordered = game.HighScores
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.AchievedAt)
                .Take(10)
                .ToList();

            game.HighScores.Clear();
            game.HighScores.AddRange(ordered);
            game.HighlightedHighScore = game.HighScores.Contains(playerEntry) ? playerEntry : null;
            game.HighScoreRepository.Save(game.HighScores);
            game.Mode = GameMode.GameOver;
            return true;
        }

        return false;
    }
}
