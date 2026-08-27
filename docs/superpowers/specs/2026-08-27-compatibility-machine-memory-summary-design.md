# Compatibility Machine Memory Summary Design

## Status

Approved for implementation on 2026-08-27.

## Problem

Hardware Inspection captures installed system memory and a timestamped
available-memory reading. Model and Hardware Compatibility consumes the fresh
available-memory reading and derives a safety reserve and safe model budget,
but its presentation contract exposes only the model requirement and safe
budget. Consequently, the compatibility page cannot show the computer values
behind its verdict.

## Scope

Add one route-neutral **This computer** memory summary to every established
GGUF and OpenVINO compatibility result. It shows:

- total physically installed system memory;
- system memory available at evaluation time;
- memory reserved for Windows and other applications by the active safety
  policy; and
- the resulting safe system-memory budget available to the model.

The summary does not change hardware capture, estimation, candidate selection,
safety policy, compatibility decisions, optimisation planning, or navigation.

## Data flow

`CompatibilityEngine.EvaluateProduction` creates an immutable machine-memory
view from the same `CompatibilityProductionInput` and safety policy used for
the compatibility decision. The view travels with `CompatibilityEvaluation`,
then the application presentation factory formats it into user-facing text.
The XAML page renders only that presentation snapshot; it performs no memory
arithmetic.

The summary is absent when production evidence is absent, stale, or the engine
cannot establish a production evaluation. Debug fixtures may explicitly supply
a non-authoritative sample summary for visual coverage, but no fixture value is
allowed into production navigation.

## Interaction and layout

Place a light, full-width **This computer** card after the model summary and
before the compatibility outcome. Use four equal responsive facts: **Installed
RAM**, **Available now**, **Safety reserve**, and **Safe for this model**. On a
narrow window, wrap them into two columns. Values use the compatibility page's
existing byte formatter, typography, surfaces, borders, and spacing.

The existing **Check again** memory-recovery action performs a new production
evaluation. The card therefore refreshes from the new timestamped reading and
never presents an old reading as current.

## Error and privacy behaviour

Zero available memory is a valid observed value and is displayed honestly.
Arithmetic is checked and fails closed. The summary contains capacities only;
it does not expose process names, paths, host identity, diagnostics, or provider
payloads.

## Verification

- Core tests prove that the summary uses the exact installed, available,
  reserve, and safe-budget values used by the decision.
- Presentation tests prove all four labels and formatted values.
- Page tests prove established summaries render and absent summaries collapse.
- Existing GGUF, OpenVINO, memory-recovery, privacy, and navigation tests remain
  passing.
- Build the Debug x64 app, launch the self-contained output once, and verify the
  window stays responsive.
