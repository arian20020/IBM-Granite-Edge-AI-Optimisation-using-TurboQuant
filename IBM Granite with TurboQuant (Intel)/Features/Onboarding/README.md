# Onboarding architecture

**Status:** Exact Model Import-to-Inspection handoff and Choose-another reset lifecycle implemented
**Last reviewed:** 2026-08-09

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
  `ChooseAnotherModelRequested`;
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
- an empty, non-go-backable frame history after both successful transitions.

`ModelInspectionPageNavigationTests` covers inspection-page ownership and
stale-callback suppression when the shell navigates away.

## Scope and non-claims

The connected stage-two route is the local GGUF Windows x64 CPU
LLamaSharp/llama.cpp `VocabOnly` inspection path only. Hardware Fit,
configuration, chat, OpenVINO, TurboQuant, Vulkan/GPU, context creation,
inference, conversion, and benchmarking remain downstream.

## Related documentation

- [Model Import architecture](../ModelImport/README.md)
- [Model Inspection architecture](../ModelInspection/README.md)
- [Onboarding controls](./Controls/README.md)
