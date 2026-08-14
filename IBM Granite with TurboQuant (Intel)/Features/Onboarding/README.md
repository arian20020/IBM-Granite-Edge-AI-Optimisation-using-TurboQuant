# Onboarding architecture

**Status:** Exact Model Import-to-Inspection handoff, live footer status, and Choose-another reset lifecycle implemented
**Last reviewed:** 2026-08-13

[Back to application feature architecture](../README.md)

## Purpose

Onboarding owns the multi-stage setup shell. It keeps one active stage page in
`StageFrame` while the persistent stage indicator remains outside the frame and
synchronized with the active page.

Stage pages report intent and carry project-owned data contracts. They do not
search for or manipulate the shell's frame.

## Current stage model

| Stage | Enum | Current implementation |
|---:|---|---|
| 1 | `ImportModel` | Local GGUF selection and immutable inspection request |
| 2 | `InspectModel` | Automatic protected GGUF inspection and terminal result |
| 3 | `CheckHardwareFit` | Not implemented in this slice |
| 4 | `ConfigureModel` | Not implemented in this slice |
| 5 | `ReadyToChat` | Not implemented in this slice |

## Exact request handoff

```text
ModelImportPage
    -> raises ModelInspectionRequested(exact Request)
OnboardingShellPage
    -> navigates StageFrame with the same Request instance
ModelInspectionPage
    -> retains that exact Request in its ViewModel
```

The shell does not reconstruct the request, re-read the file, or reduce the
handoff to a path. It updates `CurrentStage` and the persistent indicator only
after `Frame.Navigate` succeeds and the expected inspection page exists. It
then clears `StageFrame.BackStack`, so the retired import page and path-bearing
request cannot be resurrected through frame back navigation.

## Active-page subscription lifecycle

The shell has one active page event subscription at a time:

- while Model Import is active, it listens for `ModelInspectionRequested`;
- after successful inspection navigation, it attaches the new inspection page
  before detaching the old import page;
- while Model Inspection is active, it listens for
  `ChooseAnotherModelRequested` and `FooterStatusChanged`;
- duplicate attachment to the same page is ignored;
- an inactive import or inspection page cannot drive shell navigation.

## Choose another model

When the active inspection page requests another model, the shell navigates to
a fresh `ModelImportPage`, attaches its request event, detaches the retired
inspection page, and resets both `CurrentStage` and the indicator to
`ImportModel`. It clears `StageFrame.BackStack` after the successful transition,
leaving the fresh import page as the only reachable stage page.

If navigation fails, the current stage and active subscription are preserved.
The next successful import request is again forwarded as the exact object
instance to a fresh inspection page.

## Inspection footer status

The active inspection page publishes only the semantic footer states
`InProgress`, `Complete`, `NotComplete`, and `Interrupted`. The shell forwards
the current value to `OnboardingStageIndicator.InspectionStatus` and detaches
that event together with Choose another when page ownership changes. A stale
inspection page cannot alter the persistent footer. These values describe the
current Model Inspection result; they do not advance to Hardware Fit or claim
that any downstream action executed.

## Debug fixture gallery entry

In a packaged `Debug`/`x64` build, the onboarding shell adds a
`Fixture gallery` button. Clicking it replaces the active stage-frame content
with the synthetic Model Inspection gallery, clears frame history, and retires
the previous inspection page when present. Only the gallery's Close command
returns through the shell-owned fresh Model Import route. Choosing another
retires the nested inspection page and session, clears the active fixture
selection, and leaves the gallery open at `gallery:no-active-fixture`. This
entry is compiled out of non-Debug product builds and does not advance the
onboarding stage model or provide a production feature path.

The gallery owns only deterministic packaged fixtures and safe declared
interactions. It cannot start the production worker or convert synthetic
outcomes into real-worker evidence. The separately verified N-001 page journey
remains external evidence for its linked Ready screens; strict Figma PNG,
actual OS High Contrast/200% text scale, and manual Narrator evidence remain
open controlled gates.

## Ownership boundary

The onboarding shell owns frame navigation, stage synchronization, and active
page event lifetimes. It does not select or scan models, run the inspection
service, map worker evidence, classify results, or choose a hardware backend.

The inspection page reports user intent; it does not navigate the shell. Model
Import constructs/validates the request; it does not own stage navigation.

## Tests

`OnboardingModelInspectionNavigationTests` covers:

- the real event/navigation chain with exact request identity;
- current-stage and indicator updates;
- null request and failed navigation behavior;
- Choose-another reset with a fresh import page;
- the next exact request after reset;
- duplicate subscription prevention;
- stale import and inspection pages unable to drive the shell;
- initial and changed footer status, including stale-page suppression;
- an empty, non-go-backable frame history after both successful transitions.

`ModelInspectionPageNavigationTests` covers inspection-page ownership and
stale-callback suppression when the shell navigates away.

The final local hosted-equivalent packaged candidate passed all 14
`OnboardingModelInspectionNavigationTests` and all 39 protected
`ModelInspectionPageNavigationTests` within the 691/691 Release campaign. The
separately filtered packaged N-001 page journey passed 1/1. Actual controlled
High Contrast/200% text scale, manual Narrator, strict Figma pixels, and hosted
exact-head evidence remain open.

## Scope and non-claims

The connected stage-two route is the local GGUF Windows x64 CPU
LLamaSharp/llama.cpp `VocabOnly` inspection path only. Hardware Fit,
configuration, chat, OpenVINO, TurboQuant, Vulkan/GPU, context creation,
inference, conversion, and benchmarking remain downstream.

## Related documentation

- [Model Import architecture](../ModelImport/README.md)
- [Model Inspection architecture](../ModelInspection/README.md)
- [Synthetic fixture catalogue](../../../docs/evidence/testing/Model-Inspection-Fixture-Catalog.md)
- [Onboarding controls](./Controls/README.md)
