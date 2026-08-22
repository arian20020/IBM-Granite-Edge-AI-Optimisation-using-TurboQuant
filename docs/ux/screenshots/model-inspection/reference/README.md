# Model Inspection visual source

`model-inspection-complete-ordered-board-v2.svg` is the user-supplied flattened
SVG copied byte-for-byte from the approved Model Inspection Figma board. It is
an immutable geometry/provenance source, not a production UI asset.

- Figma file key: `gAmBX1DYh71hqxHVqiivus`
- Board node: `142:2148`
- Byte length: `8,190,259` bytes
- SHA-256: `8A171A3A1DF66D158990789A368C439752EBE5309B364A870E0C0107B519B7EB`
- Flattened board canvas: `4624 x 5836` px
- SVG viewBox: `0 0 4624 5836`
- Each state frame: `1440 x 1024` px
- Frame-column x coordinates: `60`, `1592`, `3124`
- Frame-row y coordinates: `82`, `1244`, `2406`, `3568`, `4730`
- Standard content geometry: centered from `x = 300` to `x = 1140`, an
  `840 px` content column with `792 px` nested rows

The board contains these 13 state nodes at the following exact flattened-board
frame coordinates:

| State | Figma node | x | y | Frame | Presentation |
|---:|---|---:|---:|---|---|
| 01 | `142:2151` | `60` | `82` | `1440 x 1024` | Inspection progress shell |
| 02 | `142:2213` | `1592` | `82` | `1440 x 1024` | Ready, collapsed |
| 03 | `142:2280` | `3124` | `82` | `1440 x 1024` | Ready, expanded |
| 04 | `142:2403` | `60` | `1244` | `1440 x 1024` | Ready with warnings, collapsed |
| 05 | `142:2476` | `1592` | `1244` | `1440 x 1024` | Ready with warnings, expanded |
| 06 | `142:2599` | `3124` | `1244` | `1440 x 1024` | Conversion required, collapsed |
| 07 | `142:2664` | `60` | `2406` | `1440 x 1024` | Conversion required, expanded |
| 08 | `142:2787` | `1592` | `2406` | `1440 x 1024` | Incomplete package |
| 09 | `142:2851` | `3124` | `2406` | `1440 x 1024` | Unsupported model |
| 10 | `142:2910` | `60` | `3568` | `1440 x 1024` | Invalid model, collapsed |
| 11 | `142:2973` | `1592` | `3568` | `1440 x 1024` | Invalid model, expanded |
| 12 | `142:3096` | `3124` | `3568` | `1440 x 1024` | Inspection cancelled |
| 13 | `142:3154` | `60` | `4730` | `1440 x 1024` | Inspection operational failure |

These are 13 state nodes. Production must render validated evidence and fixed
fallbacks; sample strings in the flattened reference are not application data.

## Strict-reference status

**STRICT PIXEL DOD: BLOCKED.** Exact 1440 x 1024 PNG goldens require exact
Figma node exports for every node listed above. That export path is unavailable
in the current session. The flattened SVG is a board/geometry reference only;
it and its individual frames must not be cropped or rerasterized into strict
goldens. AI-generated, reconstructed, placeholder, blank, or otherwise derived
images are also forbidden as reference evidence.

The following reference-bundle files are therefore deliberately absent and
deferred, not skipped or passed:

- `tests/TestFixtures/ModelInspectionVisual/References/visual-reference-manifest.json`
- `tests/TestFixtures/ModelInspectionVisual/References/01-inspection-progress.png`
- `tests/TestFixtures/ModelInspectionVisual/References/02-ready.png`
- `tests/TestFixtures/ModelInspectionVisual/References/03-ready-expanded.png`
- `tests/TestFixtures/ModelInspectionVisual/References/04-ready-with-warnings.png`
- `tests/TestFixtures/ModelInspectionVisual/References/05-ready-with-warnings-expanded.png`
- `tests/TestFixtures/ModelInspectionVisual/References/06-conversion-required.png`
- `tests/TestFixtures/ModelInspectionVisual/References/07-conversion-required-expanded.png`
- `tests/TestFixtures/ModelInspectionVisual/References/08-incomplete-package.png`
- `tests/TestFixtures/ModelInspectionVisual/References/09-unsupported.png`
- `tests/TestFixtures/ModelInspectionVisual/References/10-invalid.png`
- `tests/TestFixtures/ModelInspectionVisual/References/11-invalid-expanded.png`
- `tests/TestFixtures/ModelInspectionVisual/References/12-cancelled.png`
- `tests/TestFixtures/ModelInspectionVisual/References/13-operational-failure.png`

The corresponding fail-closed executable evidence is also deferred:

- `ModelInspectionVisualReferenceIntegrityTests.cs`
- `ModelInspectionVisualRegressionTests.cs`
- `ModelInspectionControlledAccessibilityTests.cs`
- reference `Content` items in the packaged test project

The permanent ordinary workflow now excludes the two unavailable strict
categories, and the separate manual workflow only performs a fail-closed
prerequisite check. It cannot build, execute, retain, or upload controlled
evidence while the files/classes above and approved environment pins are
absent.

The ordinary packaged suite may prove deterministic capture, effective-pixel
geometry, palette, typography, responsive hierarchy, accessibility structure,
and reduced-motion policy. Those results do not prove exact Figma pixels,
external UI Automation or Narrator output, real Windows High Contrast, or real
Windows 200% text scale. Raw identity-bearing TRX is not uploaded by the
ordinary workflow; a sanitized controlled result schema and upload closure
remain open.

The Task 11 privacy scanner approves only the current fixed nine-property
preflight manifest shape with ordered states `01` through `13`, plus PNGs whose
optional encoder metadata exactly matches the pinned WinUI
`sRGB`/`gAMA`/`pHYs` sequence. It checks neither rendered pixel text nor IDAT
content. Task 12 must extend the schema atomically when hashed per-state result
entries and a sanitized upload layout are implemented.

Consequently DoD 2, 8, 11, 12, and 13 remain open. They can close only after
the exact node exports, controlled OS runs, sanitized artifact pipeline,
hosted exact-head verification, and retained reproducible manual acceptance
exist. The checked-in 17-step guide is preparation for that acceptance, not an
execution claim.
