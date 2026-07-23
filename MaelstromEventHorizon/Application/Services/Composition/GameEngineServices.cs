using MaelstromEventHorizon.Application.Services.Combat;
using MaelstromEventHorizon.Application.Services.Gameplay;
using MaelstromEventHorizon.Application.Services.Input;
using MaelstromEventHorizon.Application.Services.Progression;
using MaelstromEventHorizon.Application.Services.Waves;

namespace MaelstromEventHorizon.Application.Services.Composition;

internal sealed class GameEngineServices(
    GameInputService gameInputService,
    PlayerSimulationService playerSimulationService,
    BossCombatService bossCombatService,
    CollisionService collisionService,
    PowerupService powerupService,
    WaveEventService waveEventService,
    WaveSpawnService waveSpawnService,
    ScoreTransitionService scoreTransitionService,
    EffectsPhysicsService effectsPhysicsService)
{
    public GameInputService GameInputService { get; } = gameInputService;
    public PlayerSimulationService PlayerSimulationService { get; } = playerSimulationService;
    public BossCombatService BossCombatService { get; } = bossCombatService;
    public CollisionService CollisionService { get; } = collisionService;
    public PowerupService PowerupService { get; } = powerupService;
    public WaveEventService WaveEventService { get; } = waveEventService;
    public WaveSpawnService WaveSpawnService { get; } = waveSpawnService;
    public ScoreTransitionService ScoreTransitionService { get; } = scoreTransitionService;
    public EffectsPhysicsService EffectsPhysicsService { get; } = effectsPhysicsService;
}
