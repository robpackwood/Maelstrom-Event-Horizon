using MaelstromEventHorizon.Application;
using MaelstromEventHorizon.Application.Input;
using MaelstromEventHorizon.Application.Services.Combat;
using MaelstromEventHorizon.Application.Services.Composition;
using MaelstromEventHorizon.Application.Services.Contracts;
using MaelstromEventHorizon.Application.Services.Gameplay;
using MaelstromEventHorizon.Application.Services.Input;
using MaelstromEventHorizon.Application.Services.Progression;
using MaelstromEventHorizon.Application.Services.Waves;
using MaelstromEventHorizon.Infrastructure.Assets;
using MaelstromEventHorizon.Infrastructure.Audio;
using MaelstromEventHorizon.Infrastructure.Persistence;
using MaelstromEventHorizon.Infrastructure.Randomness;
using MaelstromEventHorizon.Presentation;
using MaelstromEventHorizon.Presentation.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace MaelstromEventHorizon.Bootstrap;

internal static class GameCompositionRoot
{
    public static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAppDataPathProvider, AppDataPathProvider>();
        services.AddSingleton<IAssetProvider, BundledAssetProvider>();
        services.AddSingleton<IDisplaySettingsStore, JsonDisplaySettingsStore>();
        services.AddSingleton<IHighScoreRepository, JsonHighScoreRepository>();
        services.AddSingleton<IControlBindingStore, JsonControlBindingStore>();
        services.AddSingleton<ISoundEffectLibrary, SynthSoundEffectLibrary>();
        services.AddSingleton<IAudioService, SynthAudio>();
        services.AddSingleton<IRandomSource, SeededRandomSource>();
        services.AddSingleton<GameInputService>();
        services.AddSingleton<PlayerSimulationService>();
        services.AddSingleton<BossCombatService>();
        services.AddSingleton<CollisionService>();
        services.AddSingleton<PowerupService>();
        services.AddSingleton<WaveEventService>();
        services.AddSingleton<WaveSpawnService>();
        services.AddSingleton<ScoreTransitionService>();
        services.AddSingleton<EffectsPhysicsService>();
        services.AddSingleton<GameEngineServices>();
        services.AddSingleton<SceneRenderer>();
        services.AddSingleton<PlayerAsteroidRenderer>();
        services.AddSingleton<CombatActorRenderer>();
        services.AddSingleton<HazardPickupRenderer>();
        services.AddSingleton<EffectsHudRenderer>();
        services.AddSingleton<OverlayRenderer>();
        services.AddSingleton<TitleScreenRenderer>();
        services.AddSingleton<DrawingPrimitiveService>();
        services.AddSingleton<GameRenderServices>();
        services.AddSingleton(provider => provider.GetRequiredService<IDisplaySettingsStore>().Load());
        services.AddSingleton<ControlBindings>();
        services.AddSingleton<GameEngine>();
        services.AddSingleton<GameView>();
        services.AddSingleton<GameWindow>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}
