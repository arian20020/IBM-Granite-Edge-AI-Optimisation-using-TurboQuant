# Embedded Model Download Card and Responsive Onboarding Indicator

**Date:** 2026-07-30
**Status:** Approved design

## Problem

The model-import screen currently has two layout problems:

1. Fullscreen windows can leave a large empty gap between **Continue to model
   inspection** and the persistent onboarding indicator.
2. The indicator gives each label the same 40-pixel layout slot as its numbered
   box. The labels therefore render as clipped fragments even when the window
   has enough horizontal space.

The screen also sends the user to a separate recommended-model page through a
small **Download a recommended model** button, although that page contains only
a heading, back navigation, and the reusable `ModelDownloadCard`.

## Goals

- Replace the recommended-model navigation button with the complete
  `ModelDownloadCard` on `ModelImportPage`.
- Keep model downloading explicitly outside this change.
- Keep the onboarding indicator persistent below the scrollable stage page.
- Let the indicator use more horizontal space on wide windows.
- Show complete stage names and wrap whole words only when required.
- Let the indicator grow vertically when labels wrap.
- Preserve all existing local-file selection, quick-scan, cancellation,
  readiness, progress, and accessibility behaviour.
- Keep the combined page usable with keyboard input and at narrow or short
  window sizes.

## Non-goals

- Implementing a model catalogue, network request, download service, progress
  reporting, cancellation, integrity checking, or downloaded-model validation.
- Enabling **Continue to model inspection** from the recommended-model card.
- Connecting the preference slider to a real model selection.
- Changing the onboarding stage state machine, colours, numbered boxes,
  connector completion behaviour, or live-region announcements.
- Refactoring unrelated model-import or quick-scanner code.

## Current Behaviour and Ownership

- `ModelImportPage` owns model-format selection, the GGUF picker, active-scan
  cancellation and replacement, validated scan state, the import card, and the
  Continue button.
- `ModelDownloadCard` is already a reusable `UserControl`. It displays seeded
  model data and privately maps slider values to descriptive labels. Its
  download button has no click handler or command.
- `RecommendedModelDownloadPage` only wraps `ModelDownloadCard` with a title,
  description, and back button.
- `OnboardingShellPage` keeps its stage frame in a star-sized row and the
  indicator in an auto-sized footer row.

These boundaries mean the card can be composed directly into the import page
without joining the independent local-import and future-download workflows.

## Model Import Page Layout

The scrollable page content will appear in this order:

1. Granite Edge AI logo
2. **Import a model to begin** heading
3. Local model import card
4. The existing **or** separator
5. Full recommended-model card
6. **Continue to model inspection**

The embedded control will be named `RecommendedModelDownloadCard` and receive
an accessible name identifying it as the recommended-model download option.
The complete existing card remains visible: profile badge, model name, package
summary, description, four specification fields, preference slider, endpoint
labels, and visual-only download button.

The old `RecommendedModelDownloadButton` and
`RecommendedModelDownloadButton_Click` handler will be removed. The now
unreachable `RecommendedModelDownloadPage` XAML and code-behind will be retired,
along with explicit project entries if the project contains them.

`ModelImportPage` will retain its existing `ScrollViewer`. The indicator remains
outside that viewer, so it stays visible while the taller page scrolls. The
page will use 24 pixels of bottom padding and a 16-pixel bottom margin. This
provides a 40-pixel gap between the Continue button and the indicator when the
page is scrolled to the end, instead of the current 80-pixel fixed spacing plus
fullscreen surplus.

## Download Card Behaviour

Embedding the card does not create a download workflow:

- Seeded profile content remains unchanged.
- The slider continues to update only `ModelScaleValueText`.
- **Download selected model** remains visual-only and does not modify page
  state.
- The card does not enable Continue, advance onboarding, or replace a selected
  local model.
- Continue remains disabled until `ModelImportPage` receives a successful local
  quick-scan result.

The focus order follows visual order: local import controls, preference slider,
visual download action, and Continue when enabled.

## Responsive Download Card

The full card must remain readable when the window narrows. A wide visual state
applies at window widths of 720 pixels and above; the narrow state applies below
720 pixels:

- At wide widths, the model name and package summary remain side by side, and
  the four specification fields remain in one row.
- At narrow widths, the package summary moves below the model name and the
  specification fields become a two-column layout.
- Long descriptions, specification values, preference labels, and slider
  endpoint labels wrap instead of overflowing.
- The existing page-level vertical scrolling handles the additional height.
- No horizontal scrolling is introduced.

The responsive change is presentational only and does not add card state or
business logic.

## Responsive Onboarding Indicator

The indicator will keep its existing visual language while replacing the
40-pixel label constraint with responsive geometry.

### Width and track geometry

- Increase the inner track's `MaxWidth` from 934 to 1200 pixels while retaining
  32-pixel side margins.
- Use ten equal layout columns.
- Each step box and its label span two columns, placing step centres at 10%,
  30%, 50%, 70%, and 90% of the available width.
- Each connector spans the two columns between adjacent step centres and uses
  20-pixel end margins so it begins and ends at the edges of the 40-pixel boxes.

This keeps boxes, connectors, and labels centred together while allowing each
label to use one fifth of the available width.

### Labels and height

The visible labels will be:

1. **Choose model**
2. **Inspect model**
3. **Check hardware fit**
4. **Configure model**
5. **Ready to chat**

Labels use centred text, `WrapWholeWords`, and no trimming. They stay on one
line when they fit and wrap only as the available width decreases.

The control changes from a fixed `Height` to `MinHeight="128"`. The label row
uses automatic height with a 28-pixel minimum. Consequently, the footer remains
128 pixels high at wide widths and grows only enough to display wrapped labels
at narrower widths.

The divider, eyebrow, boxes, connector fills, dependency property, automation
name, live-region notification, and completed/current/future visual states
remain unchanged.

## Error and State Handling

This work introduces no new asynchronous operation or recoverable error path.
Existing quick-scan failures, cancellation, stale-result protection, and
invalid-stage restoration continue to use their current implementations.

The visual download button must not catch, synthesize, or report download
results because no download is attempted. Recommended-model interactions must
not mutate `SelectedModelPath`, `ValidatedScanResult`, `HasValidatedModel`, or
`CurrentStage`.

## Testing

Implementation will follow test-driven development.

### Automated UI tests

- Verify `ModelImportPage` contains `RecommendedModelDownloadCard`.
- Verify the old recommended-model navigation button is absent.
- Verify constructing and interacting with the embedded card does not change
  local import readiness or navigation.
- Preserve all existing model-import state-machine and quick-scan tests.
- Verify the card initially displays the **Balanced** slider label and retains
  the existing slider-label boundaries.
- Verify the indicator contains all five complete labels.
- Verify labels use whole-word wrapping and no text trimming.
- Verify the indicator has a minimum rather than fixed height.
- Measure the indicator at representative wide and narrow widths and verify the
  narrow layout can request additional height without losing any stage.
- Preserve all existing progress-state, backward-reset, invalid-stage,
  automation-peer, live-region, shell-composition, and startup-routing tests.

Tests will assert semantic properties and relative layout behaviour rather than
screen pixels.

### Manual layout checks

Inspect representative widths of 500, 800, 1024, and 1440 pixels and heights of
720, 900, and 1080 pixels. The layout's reviewed minimum window width is 500
pixels. Confirm:

- no text, boxes, connectors, or card content overlap;
- labels wrap only when needed and remain fully readable;
- the footer grows rather than clips;
- the page scroll reaches Continue and all card controls;
- no horizontal scrollbar appears;
- keyboard focus follows the visual order and remains visible; and
- the indicator remains pinned while the stage page scrolls.

### Build and regression verification

- Build the WinUI application for the repository's supported x64 configuration.
- Build the packaged unit-test project.
- Run focused model-import, model-download-card, and onboarding tests.
- Run the complete packaged test suite and inspect the generated TRX summary.
- Run whitespace and staged-scope checks before completion.

## Acceptance Criteria

- `ModelImportPage` shows the full existing download card instead of the old
  navigation button.
- No model-download implementation is added.
- The old recommended-model wrapper page is no longer reachable or retained as
  dead application code.
- Continue remains governed exclusively by successful local-model validation.
- The indicator uses additional width on large windows without unbounded
  stretching.
- Every full stage label is readable; narrow layouts wrap whole words and grow
  vertically without ellipses or clipping.
- The card is readable without horizontal scrolling at the reviewed 500-pixel
  minimum window width.
- Existing import, scan, onboarding, accessibility, and startup behaviour
  remains covered and passing.
