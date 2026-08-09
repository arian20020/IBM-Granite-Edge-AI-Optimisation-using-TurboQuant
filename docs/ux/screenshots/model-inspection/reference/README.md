# Model Inspection visual source

`model-inspection-complete-ordered-board-v2.svg` is the user-supplied flattened
SVG copied byte-for-byte from the approved Model Inspection Figma board. It is
an immutable geometry/provenance source, not a production UI asset.

- Figma file key: `gAmBX1DYh71hqxHVqiivus`
- Board node: `142:2148`
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
