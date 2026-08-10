<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# Accessibility Evidence

## Purpose

Planned evidence location for requirement N-M13: The main workflow must support keyboard operation and Windows text scaling.

## What belongs here

- Test procedure and device/settings.
- Expected and observed result.
- Screenshots/video where useful.
- Defect, fix and retest evidence.
- Evidence matching AC-N-M13.
- Acceptance criteria: Core tasks complete keyboard-only; focus is visible; content remains usable at 200% Windows text scaling with no critical clipping.
- Verification method: Accessibility checklist and task test
- Evidence index or README linking the artefacts to the requirement.

## Related IDs

N-M13

## Current Model Inspection evidence boundary

The local ordinary packaged candidate executes automation names/help text,
non-color status, disclosure Enter/Space behavior, focus retention,
polite/assertive live-region policy, responsive layouts, 200%-equivalent
layout simulation, High-Contrast resource selection, and animations-disabled
endpoints. `ModelInspectionAccessibilityTests` contributed 9 passing executions
to the filtered 686/686 candidate.

That automation is not evidence of an actual Windows High Contrast session,
actual Windows 200% text scale, manual keyboard task completion, or Narrator
output. Those campaigns remain open. The [manual controlled workflow](../../../.github/workflows/model-inspection-visual-regression.yml)
is deliberately a fail-closed preflight only; missing self-hosted labels may
prevent scheduling before preflight, and there is no controlled build, VSTest,
artifact, or upload stage yet.

Use the [17-step Visual Studio Debug guide](../../development/Model-Inspection-Visual-Studio-Debug-Guide.md)
for a future operator pass and the [Task 12 verification record](../../evidence/testing/Model-Inspection-Figma-Visual-Verification.md)
for the current `NOT RUN` statuses.

## Evidence rules

- Folder creation is only preparation; it is not proof of completion.
- Use stable requirement, work-package, test/evidence and research-question IDs.
- Preserve raw evidence; derive processed results with version-controlled scripts.
- Record dates, versions, hashes, units, actual device/backend state and failures where relevant.
- Do not commit secrets, API keys, private personal data, large model weights or unlicensed material.
- Prefer relative repository links so evidence remains usable after cloning.
- Scan the exact finalized directory that will be uploaded. Scanner success
  must gate upload.
- Do not upload raw TRX or VSTest attachment trees containing machine/user/path
  data.
- Do not claim actual OS settings or Narrator output from simulations, skipped,
  inconclusive, zero-match, empty-data, or preflight-only results.

## Source

Repository evidence structure; RTM Planned Evidence Path

