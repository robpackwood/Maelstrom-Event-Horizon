# Maelstrom: Event Horizon — Unity experiment

This is a separate Unity 2022.3 LTS 2D implementation. The original WPF project and its assets are deliberately untouched; this project uses procedural vector-style visuals and does not duplicate those files.

## Opening it

1. Open `UnityEventHorizon` in Unity Hub with Unity **2022.3 LTS**.
2. Create an empty scene and add an empty GameObject named `Game`.
3. Add `EventHorizonGame` from `Assets/Scripts/Core` to it.
4. Save as `Assets/Scenes/EventHorizon.unity` and make it the first build scene.

Controls: Left/Right turn, Space thrust, Up fire, Down shield, H hyperspace, P pause, Enter starts/restarts.

The project is code-first: it creates its camera, visuals, stars, HUD, enemies, hazards, boss encounters, bonus trials, pickups, and audio effects at runtime. The source is organized for native Unity expansion rather than importing or changing the original game.
