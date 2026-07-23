# Rights and Provenance Evidence

Audit date: 2026-07-23

Asset manifest SHA-256:
`003BD289E1FC59067663350E9EA08A85AE28887B4948FB296B061EB46DA49193`

This directory is the evidence index for the assets and title used by
Maelstrom - Event Horizon. It is designed to support a Microsoft Store
submission and future rights reviews. It is not a legal opinion or a warranty
that every right has been cleared.

## Current result

| Area | Status | Evidence |
| --- | --- | --- |
| Bundled music and WAV effect | Partially verified | Every file has a named OpenGameArt source and a CC0 or CC BY 4.0 license. Seven files exactly match the source download by SHA-256. Six tracks are transcodes whose source/license is verified but whose conversion log was not retained. |
| Synthesized sound effects | Project-generated | Effects are generated at runtime by project C# code. Repository history identifies the committer, but a contributor declaration is still the strongest ownership record. |
| Artwork, sprites, backgrounds, and icon | Not yet cleared | Files are inventoried and hashed, but no source files, generation prompts, purchase receipts, third-party licenses, or signed creator declaration were found. |
| Microsoft Store tile images | Derivative; source not yet cleared | These are deterministic derivatives of `maelstrom-icon.png`. Their status depends on clearance of that source icon. |
| Product title | Not yet cleared | Store reservation establishes catalog availability only. No trademark clearance report, legal opinion, or rights-holder permission was found. |

Do not represent the artwork or title as legally cleared until the unresolved
items in `CERTIFICATION-READINESS.md` are completed truthfully.

## Files

- `ASSET-PROVENANCE.md`: audit method, findings, and limits.
- `asset-manifest.csv`: one row per shipped image/audio asset with SHA-256.
- `THIRD-PARTY-NOTICES.md`: human-readable audio credits and license notices.
- `PROCEDURAL-AUDIO.md`: inventory and source hashes for all runtime sound cues.
- `STORE-LISTING-CREDITS.txt`: plain text attribution suitable for a Store
  description or in-game Credits view.
- `AUTHOR-ASSET-DECLARATION.md`: an unsigned declaration for the actual creator
  or rights owner to complete.
- `CERTIFICATION-READINESS.md`: blockers and submission checklist.
- `Generate-AssetManifest.ps1`: reproducibly rebuilds the hash manifest.

## Important limits

A hash proves file identity, not ownership. A Git commit proves that a file was
committed at a time by an account, not that the account owned every underlying
right. Microsoft Store certification also does not replace copyright or
trademark clearance.

For a commercial release, keep dated copies of source pages, original working
files, prompts and generation terms where applicable, invoices, licenses,
contracts, and signed contributor declarations.
