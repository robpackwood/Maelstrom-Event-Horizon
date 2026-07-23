# Maelstrom - Event Horizon

An original WPF arcade game inspired by the structure and feel of the classic Macintosh game Maelstrom. It uses original artwork, procedural effects, synthesized sound effects, and per-wave procedural music.

## Build and run

```powershell
dotnet build .\MaelstromEventHorizon.slnx
dotnet run --project .\MaelstromEventHorizon\MaelstromEventHorizon.csproj
```

## Packaged executable

The self-contained Windows x64 build is available at
[`ExecutableBuilds/win-x64/MaelstromEventHorizon.exe`](ExecutableBuilds/win-x64/MaelstromEventHorizon.exe).
It includes the .NET runtime and game assets, so no separate installation is required.

Regenerate it from the repository root with:

```powershell
dotnet publish .\MaelstromEventHorizon\MaelstromEventHorizon.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishDir=..\ExecutableBuilds\win-x64\
```

## Controls

- Left Arrow: turn counter-clockwise
- Right Arrow: turn clockwise
- Spacebar: thrust forward
- Up Arrow: fire
- Down Arrow: shield
- H: hyperspace jump
- P: pause
- Escape: confirm leaving the current run and return to the title screen
- Enter: start or restart

Choose **Controls** on the title screen to rebind every gameplay action. Bindings are saved for the current Windows user. Use the title screen's **Quit** menu option to close the application.

The title screen also provides separate **Music** and **Sound FX** sliders. Select either slider with Up/Down and adjust it with Left/Right; both levels are saved for the current Windows user.

## Gameplay

Clear escalating asteroid waves, fight two classes of pursuing fighters, and survive homing mines, black holes, and supernovas. Homing mines track the ship and require five direct shots to destroy. Black holes have a 12.5% chance to appear after wave 1, gently pull the ship off course, collapse when shot, and destroy the ship on contact. Canisters drift in from the edge and contain rapid fire, air brakes, luck, triple fire, long range, shield energy, enemy freeze, a smart bomb, 16-Bit Vision, Ricochet Arena, or Giant Ship. Shoot floating score bonuses, multipliers, and fast bonus comets before they escape.

Giant Ship doubles the player's visual size and collision footprint across wave transitions. The first otherwise lethal non-black-hole hit shrinks the ship to normal size, leaves it alive with brief invulnerability, and plays an original descending transformation jingle. A regular-size ship is destroyed by the next unprotected lethal hit as usual.

Every fifth wave is a weapons-off dodge trial. Diagonal Metal Storm, Quad-Cross Crossfire, Shifting Slalom, Spiral Swarm, 3D Warp Tunnel, and 3D Orbital Dive rotate between appearances while their speed and density continue to rise. Firing and shields are unavailable, stored shield energy is preserved, each avoided hazard earns $500, and one collision fails the trial for $0 without costing a ship.

Each dodge trial is followed by a dedicated alien boss encounter. The Sludge Maw, Eye Tyrant, Bone Broodmother, and Void Leech cycle through different pursuit, orbit, charge, fan, radial, and spiral-shot patterns. Later encounters increase boss health, speed, and firing tempo. A warning siren and intense music announce each boss round, and defeating the creature banks an escalating boss bounty through the normal wave recap.

16-Bit Vision renders the entire game on a 640x360 pixel grid in RGB565 color with nearest-neighbor scaling, giving backgrounds, ships, effects, and interfaces a unified 16-bit appearance without sacrificing HUD readability. It remains equipped across waves until the current player ship is destroyed.

Ricochet Arena seals the playfield with illuminated rails and replaces edge wrapping with wall reflection for the player, enemies, ordinary asteroids, hazards, pickups, comets, debris, particles, and projectiles. Every shot becomes a rotating striped beach ball and bounces until its normal lifetime expires. Bonus-stage fly-through asteroids retain their one-pass exit behavior so the dodge stage can still finish. The arena remains active until the current wave ends.

Waves can also produce a rare spinning rescue ship modeled after the player's craft. Touch it before it leaves to gain an extra life.

Each wave independently has roughly a one-in-three chance to produce one item-canister event, one score multiplier, and one bonus-comet event. Item and comet events each have a 7.5% chance to become a storm with 5-10 arrivals. Standard and storm comets are worth $500, $1,000, $2,000, $3,000, $4,000, or $5,000 and ring up with an on-screen value and cha-ching sound. No event category can trigger more than once in the same wave; Luck of the Irish guarantees any event that has not yet appeared.

Scores are shown as dollars and remain pending during play. After a wave, ordinary earnings and multiplied comet cash are counted into the bank on a summary screen. Deposits over $10,000 trigger a cash-confetti bonus, and every $50,000 in the bank awards an extra life.

The game keeps a persistent top-ten pilot table. Qualifying players enter a name after the run; scores are stored under the current Windows user's local application data folder.

Every wave receives a deterministic stereo arrangement with its own tempo, key, progression, melody pattern, bass line, and timbre. Bonus stages use a faster, more intense arrangement. Eight embedded deep-space scenes rotate with per-wave drift, color grading, and later-cycle mirroring while a contrast veil keeps gameplay objects readable.

The bundled CC0 tracks remain available as audio fallbacks: "Through The Universe" and "Singularity (Action)" by Vitalezzz, sourced from OpenGameArt.
