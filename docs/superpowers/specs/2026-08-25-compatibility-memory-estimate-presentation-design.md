# Compatibility Memory Estimate Presentation Design

## Status

Approved by the user on 25 August 2026.

## Goal

Make an evaluated model-and-hardware compatibility result explain its memory
estimate without requiring the user to open a disclosure. When currently free
RAM is the limiting factor, give an immediate, actionable recovery instruction.

## Scope

This is a presentation-only change. It uses the existing
`CompatibilitySetupView` values and does not change estimation formulas,
safety policy, hardware collection, route admission, compatibility decisions,
navigation, or execution behavior.

## Visible estimate breakdown

Every screen with an evaluated setup shows an always-visible **Estimated memory
breakdown** beneath the memory budget diagram. It contains:

- Model weights.
- KV cache.
- Runtime and buffers, formed only by grouping the existing compute buffer,
  backend allocation, staging buffer, model state, application overhead, and
  persistent-artifact components that are present at the peak phase.
- Margin for error, using the existing uncertainty allowance.
- Estimated peak total, using the existing required-bytes value.
- Safe memory available, using the existing safe-budget value.

Missing categories display `0 MB` only when an evaluated setup genuinely
contains no bytes for that category. A run with no evaluated setup continues to
show no estimate breakdown, so absence is never presented as a zero-cost model.
All figures retain the existing coarse GB/MB formatting and are labelled as
estimates rather than measurements.

The existing **How we worked this out** disclosure remains available for the
methodology, assumptions, provenance, and detailed component presentation. The
new summary does not duplicate or alter the calculation.

## Low-memory guidance

When the concluded state is `NothingFits` because the evaluated configurations
exceed the safe budget, the prominent outcome detail tells the user to close
unused applications and browser tabs to free memory, then check again. The
**What would help** recovery repeats the instruction and explains that current
free memory—not installed memory—is what the safety decision uses.

This instruction is not shown for unavailable evidence, unsupported routes,
inspection failures, cancellations, or other non-memory outcomes.

## Layout and accessibility

The breakdown follows the approved light compatibility visual language:
rounded bordered surfaces, existing typography and colour tokens, readable
labels, and no colour-only meaning. It fits inside the centred 1240-pixel
desktop column and reflows without horizontal clipping in the existing narrow
assessment layout. Values and labels remain readable at Windows text scaling.

## Verification

Automated tests must establish that:

1. Evaluated states expose the six visible summary values from the existing
   setup data.
2. Weights, KV cache, grouped runtime/buffers, and margin add up consistently
   with the existing peak total.
3. Unevaluated states do not render a false zero-valued breakdown.
4. Only the memory-blocked concluded state contains the applications/browser
   tabs recovery instruction.
5. Existing compatibility fixture, rendering, navigation, and core calculation
   tests remain green.

The packaged Debug/x64 application must build successfully, the focused WinUI
tests must pass through packaged VSTest, and `git diff --check` must remain
clean.

## Non-goals

- Changing the 2 GB Windows allowance, 1 GB operational reserve, or estimator
  calibration margin.
- Adding live GPU-memory collection or enabling a new GPU route.
- Claiming any estimate is measured.
- Adding a new page, navigation step, retry mechanism, or backend field.
