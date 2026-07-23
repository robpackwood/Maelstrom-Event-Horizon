# Procedural Audio Inventory

Audit date: 2026-07-23

The game defines 28 sound cues. Twenty-seven are normally generated from
mathematical oscillators, envelopes, deterministic noise, filters, and
project-authored mixing code. `ShipCrash` normally loads the licensed
`ship-destruction.wav`; it also has a synthesized fallback.

## Sound cues

- Fire
- EnemyFire
- EnemyWarning
- BossAlarm
- MenuMove
- Thrust
- Explosion
- AsteroidExplosion
- SteelHit
- Pickup
- Shield
- ShieldImpact
- Nova
- Wave
- Life
- Mine
- Vortex
- CashRegister
- Coin
- CashBonus
- ChaChing
- CometCelebration
- MultiplierWoohoo
- ShipCrash
- ShipBlast
- BonusFailed
- GiantGrow
- GiantShrink

## Source evidence

| File | SHA-256 | Purpose |
| --- | --- | --- |
| `Domain/Enums/SoundCue.cs` | `9D07EC414D3F39CD41E8A8D5F96C475FF3DB11E20B49F3A6415B82DB5ABEC660` | Canonical cue inventory |
| `Infrastructure/Audio/SynthSoundEffectLibrary.cs` | `399A9E4D1B09FE1A4E64095140FE771BDD42B2DB80C4B1CD2BF6D0CDAF43F086` | Waveform construction and ShipCrash fallback |
| `Infrastructure/Audio/SynthAudio.cs` | `6C38ACE27B2E8DABD60A20C13CE83BFDB805DFD14CAE67E09D6F874182C26203` | Playback, layering, volume, and music routing |

The synthesis source is present in Git history under the project's contributor
identity. That is technical provenance and custody evidence, not a substitute
for a contributor ownership declaration. Complete
`AUTHOR-ASSET-DECLARATION.md` truthfully if stronger ownership evidence is
required.
