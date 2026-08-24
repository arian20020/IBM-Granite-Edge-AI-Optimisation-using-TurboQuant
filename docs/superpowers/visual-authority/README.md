# Visual authority for Model/Hardware Compatibility

Copied into the repository on 2026-08-22 so the design these screens are built
against travels with the code. They were previously only in a Downloads folder
and a brainstorm scratch directory, where a hunt was needed to find them and
nothing recorded which version was approved.

| File | SHA-256 | What it is |
|---|---|---|
| `hardware-layout-direction-b-refinement-v3.html` | `6C677D5E9BF9F2F58F1F404ECA3798CD6D17C68911F966CA21DEC01CA902FB4B` | The Direction B grammar. **This hash matches the one the approved design spec pins**, so it is verifiably the file the spec means. |
| `compat-visual-style-v3.html` | `3FFC2F3664363F20908DEDF110E52B53CEE0860FB24C096BEDE4D7DB979A03DF` | The compatibility page with Direction B applied, titled "Compatibility front end — geometry corrected". Confirmed by the user on 2026-08-22 as the approved compatibility design. |
| `budget-diagram.html` | `873199160F7660F82F0B1604210C0CBBA26A29C4CFEFD45EC521EFC416473EC6` | The memory budget diagram treatments. |

Originals: `C:\Users\Arian\Downloads\HI-P4-Upload\` and
`…\IBM-Granite-TurboQuant-Intel\.superpowers\brainstorm\1186-1787199997\content\`.

## What these do and do not settle

`compat-visual-style-v3.html` is a **geometry and style authority, not a
ten-state contact sheet.** It renders one state fully — *Estimated compatible*,
screen 02 — and every rule the design spec's section 12 names is expressed in
its CSS rather than described in prose:

- `.b-columns { grid-template-columns: 1.35fr .85fr; }` — the dominant facts card and stacked mini-cards
- `.hi [class] { min-width: 0; }` — every grid and flex child may shrink, so no pill or label escapes its card
- `.b-side { grid-auto-rows: 1fr; }` and `.facts-grid { grid-auto-rows: 1fr; }` — equal rows, equal tile heights
- `.fact-detail { margin-top: auto; }` — the detail line pinned to the bottom of its tile
- `--gap: 10px` — one token driving all vertical rhythm
- `.stepper .steps { grid-template-columns: repeat(5, …); }` — the 5-step stepper
- `.details-disclosure` — the calculation disclosure
- `.budget` — the memory budget bar

The other nine states are **described** in the design spec's section 12 but not
drawn here. They share this grammar and differ in the outcome card's colour,
glyph and wording, in which facts are shown, and in which actions are enabled.
Building them means applying this grammar to the spec's written description — a
derivation, not a transcription, and worth reviewing against the intent rather
than against a pixel reference that does not exist.

The three PNGs in `Downloads\Granite-C1-Claude-Upload-Supported\` are related
but are **not** authority: their own provenance note says they are "visual /
reference evidence, not automatically approved final Compatibility pixels".
