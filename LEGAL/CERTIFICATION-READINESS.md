# Microsoft Store Rights Readiness

Audit date: 2026-07-23

Current decision: `NOT READY TO CLAIM COMPLETE RIGHTS CLEARANCE`

## Blocking items

- [ ] Obtain a truthful signed declaration and supporting evidence for every
  image row marked `DECLARATION_REQUIRED`.
- [ ] Establish the provenance of `Assets/maelstrom-icon.png`; all Store package
  images depend on it.
- [ ] Perform a title clearance search for `Maelstrom - Event Horizon` and
  confusingly similar names in every intended market. Retain the search report
  and counsel's advice or choose a demonstrably original title.
- [ ] Preserve the original OGG files and exact conversion commands for the six
  transcoded CC BY tracks, or replace the MP3s with reproducibly generated
  versions.
- [ ] Make the CC BY credits reasonably accessible to players. A Store listing
  credit is useful; an in-game Credits view is stronger.
- [ ] Retain dated copies of every source page and applicable license.

## Verified items

- [x] Every bundled MP3 and WAV has a named source and a CC0 or CC BY 4.0
  license page.
- [x] Seven bundled audio files exactly match their source download by SHA-256.
- [x] CC BY 4.0 authors, source links, license link, and modification notices
  are recorded in `THIRD-PARTY-NOTICES.md`.
- [x] Every shipped image/audio file is represented by path, byte length, and
  SHA-256 in `asset-manifest.csv`.
- [x] Runtime synthesized sound effects are traceable to project source code.
- [x] All 28 runtime sound cues are inventoried in `PROCEDURAL-AUDIO.md`.
- [x] Store package tile images are traceable to their generation script and
  source icon.

## Store policy basis

Microsoft Store Policy 11.2 requires content and metadata to be original,
appropriately licensed, permitted by the rights holder, or otherwise permitted
by law. Policy 10.1.1 addresses unique and non-misleading names and metadata.

https://learn.microsoft.com/en-us/windows/apps/publish/store-policies

Passing Store certification is not a legal determination of ownership and does
not eliminate infringement risk.
