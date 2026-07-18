# Maelstrom: Event Horizon

An original WPF arcade game inspired by the structure and feel of the classic Macintosh game Maelstrom. It uses original artwork, procedural effects, synthesized sound effects, and a bundled CC0 soundtrack.

## Build and run

```powershell
dotnet build C:\MaelstromSol\MaelstromEventHorizon.slnx
dotnet run --project C:\MaelstromSol\MaelstromEventHorizon\MaelstromEventHorizon.csproj
```

## Controls

- Left Arrow: turn counter-clockwise
- Right Arrow: turn clockwise
- Spacebar: thrust forward
- Up Arrow: fire
- Down Arrow: shield
- H: hyperspace jump
- P: pause
- Q: leave the current run and return to the title screen
- Enter: start or restart

Choose **Controls** on the title screen to rebind every gameplay action. Bindings are saved for the current Windows user. Use the title screen's **Quit** menu option to close the application.

## Gameplay

Clear escalating asteroid waves, fight two classes of pursuing fighters, and survive homing mines, black holes, and supernovas. Black holes have a 12.5% chance to appear after wave 1, gently pull the ship off course, collapse when shot, and destroy the ship on contact. Canisters drift in from the edge and contain rapid fire, air brakes, luck, triple fire, long range, shield energy, enemy freeze, or a smart bomb. Shoot floating score bonuses, multipliers, and fast bonus comets before they escape.

Waves can also produce a rare spinning rescue ship modeled after the player's craft. Touch it before it leaves to gain an extra life.

Each wave independently has roughly a one-in-three chance to produce one item-canister event, one score multiplier, and one bonus-comet event. Item and comet events each have a 7.5% chance to become a storm with 5-10 arrivals. Standard and storm comets are worth $500, $1,000, $2,000, $3,000, $4,000, or $5,000 and ring up with an on-screen value and cha-ching sound. No event category can trigger more than once in the same wave; Luck of the Irish guarantees any event that has not yet appeared.

Scores are shown as dollars and remain pending during play. After a wave, ordinary earnings and multiplied comet cash are counted into the bank on a summary screen. Deposits over $10,000 trigger a cash-confetti bonus, and every $50,000 in the bank awards an extra life.

The game keeps a persistent top-ten pilot table. Qualifying players enter a name after the run; scores are stored under the current Windows user's local application data folder.

Music: "Through The Universe" by Vitalezzz, released under CC0 / Public Domain and sourced from OpenGameArt.
