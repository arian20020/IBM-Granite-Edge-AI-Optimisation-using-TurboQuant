# Hardware Inspection Approved Visual Restoration Design

**Status:** User-selected on 20 August 2026; ready for implementation review.

## Decision

Production Hardware Inspection will reproduce the previously agreed visual family rather than introduce a new design language:

- Completed and Completed-with-warnings use **Direction B — scan-first hierarchy** from `hardware-layout-direction-b-refinement-v3.html`.
- All seven active stages use **Direction B — measured checklist** from `hardware-progress-directions-v3.html`.
- Warnings, critical failure, transient failure, repair-required failure, Stopping, and Cancelled use the existing audited family from `hardware-outcome-recovery-family-v2.html`.

This decision supersedes the experimental terminal treatment recorded in `2026-08-20-hardware-inspection-terminal-polish-design.md`. It does not supersede the approved behavioral, privacy, accessibility, or state contracts.

## Pinned visual sources

- Completed Direction B: SHA-256 `6C677D5E9BF9F2F58F1F404ECA3798CD6D17C68911F966CA21DEC01CA902FB4B`.
- Progress Direction B: SHA-256 `EDA670DDB8E6F3628D3F900B0F8A47FED19D51B763A90EF9C691132BF401E8DF`.
- Audited outcome and recovery family: SHA-256 `24D34112F5BDA58A464032915C672EA825705880293BFFB88031CEB97F7B5286`.

The HTML artifacts are structural and visual authorities. Production remains native WinUI and must use semantic resources, keyboard behavior, UI Automation, and High Contrast support rather than browser implementation details.

## Completed composition

Direction B owns the scan order and geometry:

1. Page title and local-only subtitle.
2. Semantic outcome card with a correctly centred vector status glyph.
3. Dominant `This computer` facts card.
4. Supporting `Local AI tools` and `Information sources` cards.
5. Warning-only review section when applicable.
6. Full-width `Inspection details` disclosure and nested `Technical information for IT` disclosure.
7. Centred typed actions.
8. Shell-owned five-step footer.

Wide layouts use the selected asymmetric facts/support composition. Medium and compact layouts stack in the same reading order with no clipped or horizontal content.

## Active-stage composition

Direction B uses the measured checklist:

- one current-stage hero;
- truthful `N of 7` progress with no invented percentage;
- exactly seven canonical rows in order;
- completed tick, one active orbit located on the active row, and numbered waiting rows;
- visible non-colour `Complete`, `Active`, and `Waiting` labels;
- centred `Cancel inspection` action;
- no terminal facts, details, or conclusions while active.

## Outcome and recovery composition

The audited recovery family is reproduced without reinterpretation:

- exact top-level copy, actions, and retryability;
- semantic amber, red, and neutral outlines and surfaces;
- properly centred vector glyphs rather than font-positioned `×`, `!`, or tick characters;
- compact guidance rows without decorative accent rails or experimental step tiles;
- the local-processing boundary where specified;
- native `Inspection details` disclosure with the state-specific summary and seven stage records;
- Stopping contains no report/details disclosure and remains bounded until cleanup completes.

## Visual system

- Light is the reference presentation.
- Inter typography, 840-pixel content column, 24-pixel desktop gutters, 16-pixel compact gutters, 12-pixel card radii, one-pixel semantic borders, and minimum 44-pixel targets remain fixed.
- Surfaces stay crisp and restrained: no gradients, glass effects, ornamental shadows, novel badges, or invented copy.
- Status glyph geometry is explicit and optically centred in a fixed square/circle container.
- Dark and High Contrast use semantic equivalents without changing layout or information hierarchy.

## Acceptance

- Native captures at 1440×1100, 900×1000, 480×900, and 720×900 at 200% text structurally match the three pinned sources.
- Every one of the 15 observable states uses the selected family consistently.
- Progress, Completed, warnings, failures, Stopping, and Cancelled retain their approved behavior and exact copy.
- Vector status glyphs are centred at all supported scale factors.
- Packaged tests, Hardware contract tests, keyboard/UIA checks, responsive checks, and theme checks pass.
