# Model Inspection Native Spacing and Alignment Polish

**Status:** User-approved design, pending implementation plan  
**Date:** 2026-08-20  
**Production baseline:** `85084459e9544984b0815f40ddad1ed6150cee00`  
**Visual oracle:** `model-inspection-balanced-full-approval-v3.html` served at `http://127.0.0.1:59034/`  
**Visual-oracle SHA-256:** `D44CFCE9C53BCD9D0EAAA41CA8BA9DED434F68845BBF943351518366FBB20CFF`

## Objective

Bring the native WinUI Model Inspection screens into the same polished visual
family as the user-approved localhost presentation. The correction is a
spacing, alignment, and visual-rhythm pass. It does not change Model
Inspection behavior, state classification, copy, commands, navigation,
eligibility, or backend data.

The finished native screens must no longer contain uneven row heights,
top-aligned status labels, scattered step copy, crowded recovery text,
inconsistent card padding, or controls that visually drift between states.

## Authority and precedence

1. Existing production presentation objects, bindings, commands, automation
   names, state ownership, and functional tests remain behavioral authority.
2. The approved localhost HTML and the user's 2026-08-20 screenshot feedback
   are visual geometry and composition authority.
3. Native WinUI accessibility, High Contrast, keyboard focus, and text-scale
   requirements override any HTML behavior that cannot be reproduced safely.
4. The previously approved common-template design remains in force where it
   does not conflict with this narrower spacing correction.

## Chosen approach

Use one shared native spacing system instead of applying unrelated margins to
individual states. Existing controls remain in place. Model-owned XAML theme
resources define the measurements; control-local styles and responsive visual
states consume them.

Rejected approaches:

- Independent per-screen patches: fast initially, but they reproduce the
  spacing drift visible in the current captures.
- Rewriting the Model Inspection controls: unnecessary and too risky to the
  already verified backend and accessibility behavior.
- Fixed-height text containers at every scale: they would clip or truncate at
  200% text.

## Shared visual system

The localhost oracle is translated to these native rules:

- Canvas: light semantic application canvas; no dark forced theme.
- Content column: centered, maximum 840 pixels.
- Page gutters: 24 pixels wide/medium and 16 pixels compact.
- Major card gap: 16 pixels.
- Related card/section gap: 10 or 12 pixels as specified below.
- Card corners: 12 pixels with a one-pixel semantic border.
- Internal card padding: 18 pixels horizontally and 16 to 18 pixels
  vertically; compact padding is 16 pixels.
- Fact and finding tile corners: 9 pixels.
- Action targets: at least 44 pixels, 10-pixel corners, `18,10` padding,
  semibold centered labels, and native accent/default interaction states.
- Glyph/status column: fixed 24 pixels.
- Glyph-to-copy gap: 12 pixels.
- Trailing status column: stable width, vertically centered, and aligned to
  the same right inset in every repeated row.

All surfaces use semantic theme brushes. Light is the visual comparison
reference; Dark and High Contrast remain functional and must not acquire
literal colors that bypass their resource dictionaries.

## Repeated progress and inspection rows

The five progress rows and five expanded inspection rows share one row
anatomy:

1. A fixed 24-pixel glyph or step-number column.
2. A copy column beginning 12 pixels to its right.
3. A stable trailing status column.

The glyph, title/helper stack, and trailing status are vertically centered in
the row. Titles and helper copy keep a consistent baseline and line spacing.
`Waiting`, `Checking`, `Passed`, `Warning`, and equivalent status labels must
not sit at the top edge.

At each viewport and text profile every row in the same five-row group uses
the same measured slot height. The largest required row determines the group
height; all other rows receive that height. This applies independently to:

- normal wide/medium;
- normal compact;
- representative 200% text.

The implementation should use a native XAML uniform repeated-item layout when
it preserves the existing lifecycle and prepared/clearing hooks. If that
layout cannot preserve those hooks, the fallback is a shared responsive row
height proven large enough for every maximum-safe string in the existing
fixtures. No row may clip, truncate, or grow independently.

The progress step title moves slightly right relative to the current native
capture by using the approved 12-pixel glyph gap. Step title, helper text, and
status remain in one predictable horizontal rhythm at wide/medium widths. At
compact or 200% text, the status may move to a second line only through the
existing responsive state, with the same inset as the copy column.

## Completion and technical-details rows

At normal text scale, the `All 5 inspection checks passed` summary and `View
inspection details` action use a single vertically centered 68-pixel
disclosure header. At increased text scale, every header in the repeated group
grows to the same measured height required by its largest safe string. The
24-pixel leading glyph column, title/helper content, and trailing action or
chevron are centered to the same axis.

Expanded inspection rows use the uniform-row contract above. Existing
disclosure state, keyboard behavior, focus retention, hit testing, UIA
suppression while collapsed, and technical-details command behavior remain
unchanged.

## Failure, unsupported, cancelled, and warning cards

All terminal content cards use the same internal rhythm:

- 18 pixels from card edge to section title;
- 12 pixels from title to the semantic result row;
- 8 pixels from result row to recovery/helper copy;
- 8 pixels from helper copy to the diagnostic/support-code row when present;
- 18 pixels from the final visible element to the card edge.

The semantic result row uses the shared 24-pixel glyph column, 12-pixel copy
gap, and vertically centered trailing status. Result-row title and helper copy
must not collide with the trailing status at compact widths.

The existing messages such as `Restart inspection or choose another model.`
retain their exact text but receive consistent whitespace above and below.
Diagnostic codes remain visually subordinate and align with the result copy
column rather than floating against the card edge.

## Action cards

Every terminal action card uses the localhost composition:

- 18 pixels top padding;
- centered action heading;
- 6 pixels to the recovery/helper sentence;
- 12 pixels from helper sentence to the first action row;
- 8 pixels between action buttons or action hosts;
- 4 pixels between a disabled future action and `Coming later`;
- 18 pixels bottom padding after the final visible action/help element.

Wide and medium layouts retain centered equal-width action columns. Compact
and 200% layouts stack actions full-width in the same logical order. Hidden
actions consume no space. Disabled future actions and their help text remain
paired so that neither creates an unexplained gap.

The cancelled, incomplete, unsupported, warning, and ready states use the
same spacing system; state-specific action counts do not change the outer card
padding or the gaps between equivalent elements.

## Model overview and overall card alignment

The balanced overview remains visually equivalent to the approved localhost
screen: approximately 1.6fr primary facts to 1fr configuration/support at
wide width, with a 16-pixel macro gap. Below 888 pixels, groups stack in the
same reading order. Below 600 pixels, fact fields become one column.

Adjacent cards share the same left/right bounds. Headings, semantic rows,
disclosure headers, and action cards use consistent horizontal insets. No card
may be narrower because of an accidental nested margin.

## Accessibility and responsive behavior

- No horizontal scrolling or clipping at 480 pixels or representative 200%
  text.
- Text remains enabled for system text scaling.
- Uniform rows grow together rather than suppressing text.
- Keyboard focus remains visible on all enabled actions and disclosures.
- Reading order, tab order, commands, and automation names remain unchanged.
- Status is communicated by text and vector shape, never color alone.
- Light, Dark, High Contrast, reduced motion, and keyboard-only interaction
  remain supported.

## Production scope

Permitted production changes are limited to Model-owned visual XAML/theme
files already covered by the common-template branch:

- `Features/ModelInspection/Presentation/ModelInspectionTheme.xaml`
- `Features/ModelInspection/Controls/InspectionContentCard.xaml`
- `Features/ModelInspection/Controls/InspectionModelCard.xaml`
- `Features/ModelInspection/Controls/InspectionActionCard.xaml`
- `Features/ModelInspection/Controls/InspectionDisclosure.xaml` only if the
  shared header alignment requires it.

Existing Model visual/layout tests may change to lock the approved geometry.
No production C#, ViewModel, service, scanner, classifier, contract, runtime,
navigation, project, App resource, Hardware Inspection, Model Import, or
Onboarding file may change.

## Test-first acceptance

Before production edits, loaded WinUI tests must fail on the current geometry
and prove each requested correction:

- equal repeated-row heights per viewport/text profile;
- centered glyph, copy stack, and trailing status;
- exact 24-pixel glyph column and 12-pixel copy gap;
- identical progress and expanded-details row anatomy;
- centered 68-pixel summary/disclosure header;
- terminal-card internal gaps and diagnostic alignment;
- action-card top/bottom padding and inter-element spacing;
- no compact/200% clipping or overlap;
- preserved command, state, focus, disclosure, automation, and named-element
  contracts.

Focused RED/GREEN runs are followed by a fresh Debug x64 build and the full
unfiltered packaged suite. The final suite must retain its exact discovered
identity multiset and contain zero non-passing results.

## Native capture acceptance

The same exact committed WinUI controls are captured—not HTML—at:

- 1440×1100 wide;
- 900×1000 medium;
- 480×900 compact;
- 720×900 representative 200% text.

Required states include progress, ready with expanded inspection details,
warning, incomplete/operational failure, unsupported, and cancelled. Captures
must additionally include a visible keyboard-focus state.

The visual audit compares native captures directly with the approved localhost
oracle for spacing rhythm, alignment, card bounds, row equality, modern action
treatment, and absence of clipping. The branch is not merged until the user
approves these new native captures.

## Non-goals

- No backend or functional repair.
- No copy rewrite.
- No new action, badge, state, or model field.
- No compatibility calculation or Hardware Inspection integration.
- No attempt to imitate browser-only behavior at the cost of native
  accessibility.
