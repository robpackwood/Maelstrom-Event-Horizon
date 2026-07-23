namespace MaelstromEventHorizon.Presentation.Rendering;

internal sealed class GameRenderServices(
    SceneRenderer sceneRenderer,
    PlayerAsteroidRenderer playerAsteroidRenderer,
    CombatActorRenderer combatActorRenderer,
    HazardPickupRenderer hazardPickupRenderer,
    EffectsHudRenderer effectsHudRenderer,
    OverlayRenderer overlayRenderer,
    TitleScreenRenderer titleScreenRenderer,
    DrawingPrimitiveService drawingPrimitiveService)
{
    public SceneRenderer SceneRenderer { get; } = sceneRenderer;
    public PlayerAsteroidRenderer PlayerAsteroidRenderer { get; } = playerAsteroidRenderer;
    public CombatActorRenderer CombatActorRenderer { get; } = combatActorRenderer;
    public HazardPickupRenderer HazardPickupRenderer { get; } = hazardPickupRenderer;
    public EffectsHudRenderer EffectsHudRenderer { get; } = effectsHudRenderer;
    public OverlayRenderer OverlayRenderer { get; } = overlayRenderer;
    public TitleScreenRenderer TitleScreenRenderer { get; } = titleScreenRenderer;
    public DrawingPrimitiveService DrawingPrimitiveService { get; } = drawingPrimitiveService;
}
