# UO1 Handoff — Canonical Cross-Route Optimisation UI

**Worker:** UO1 (optimisation UI)  
**Date:** 2026-08-24  
**Status:** Canonical presentation feature complete; ready for I0 integration.

## Branch and contract base

| Field | Value |
|---|---|
| Branch | `feature/cross-route-optimisation-ui-v1` |
| Worktree | `C:\UO1` |
| C1 remote branch | `origin/feature/cross-route-optimisation-contracts-v1` |
| C1 verified SHA / UO1 base | `f999443279ae3505f7df1c686f98c5163c190acd` |
| UI code tip before this handoff | `e9e4fc342fdea8213b4e94b0bcd17dc138c40dca` |
| Branch tip | Resolve with `git rev-parse feature/cross-route-optimisation-ui-v1` |

The branch was created directly from the verified remote C1 tip. The C1
standalone preflight passed all 659 tests before UO1 implementation began.

## Canonical UI

The self-contained `Features/ModelOptimization` feature provides:

- separate recommended Automatic selection and the existing continuous
  `ModelPreferenceSlider`, with the exact five C1 preference labels;
- a live complete candidate summary with truthful `New model copy: Yes/No`;
- confirmation of persistent/runtime effects, original preservation, memory,
  disk, limitations, output and route validation;
- seven ordered, equal-height progress rows from Preflight through Publish;
- Cancelled, Replan required and Failed recovery states with bounded support
  codes;
- persistent and runtime-only destinations with truthful Chat and Save copy;
- optimisation-owned modern-light and High Contrast resources;
- compact, standard and wide page states, natural scrolling and a simulated
  200% text layout contract;
- a debug gallery covering all 15 required fixtures.

The feature projects presentation only. It does not execute a route, open a
route tool, mutate navigation, or write a model.

## Typed I0 seam

`OptimizationPage.IntentRequested` emits `OptimizationIntentEventArgs` with
the current `OptimizationPreferenceSelection`, where applicable. The closed
intent set is:

- `PreferenceChanged`
- `ReviewConfigurationRequested`
- `ConfirmRequested`
- `BackRequested`
- `CancelRequested`
- `RetryRequested`
- `ReviewAgainRequested`
- `ChatRequested`
- `SaveRequested`

I0 supplies immutable presentation states through
`OptimizationPage.ApplyPresentation(OptimizationPresentationState)` and owns
navigation/execution wiring. UO1 must remain the only optimisation XAML owner.

## Fixture and visual matrix

The gallery contains the complete matrix:

`selection-automatic`, `selection-manual`, `confirmation`,
`progress-preflight`, `progress-staging`, `progress-optimise`,
`progress-validate`, `progress-smoke`, `progress-reinspect`,
`progress-publish`, `cancelled`, `replan-required`, `failed`,
`success-persistent`, and `success-runtime-profile`.

Packaged UI coverage applies every fixture to the canonical page and verifies
compact/standard/wide state declarations, 360 px compact geometry,
200%-equivalent text growth, scrolling, equal progress rows, focus visuals,
44 px minimum targets, automation names, full-width disclosures, light theme
and the High Contrast dictionary. No screenshot files are committed; the
`OptimizationFixtureGalleryPage` is the deterministic visual-review surface.

Read-only visual references used:

- `feature/model-import-drag-drop@6c96f0203b9e38ac02639433d13b53c8dc2eecc8`
- `feature/model-inspection-hardware-template-v1@ba4fd7bad5c473208248247fcba27e6f22c356ab`
- `feature/hardware-inspection-functional-v1@f521e9eea81b59f5814fcf100e4f527391ee67d2`
- `feature/hardware-inspection-page-v1@ed8bc75881b2637beda6fe5689611a46fb00bf2b`

## Verification evidence

| Check | Result |
|---|---|
| C1 standalone preflight before implementation | 659 passed, 0 failed |
| C1 standalone preflight after implementation | 659 passed, 0 failed |
| Final C1 TRX | `C:\UO1\TestResults\UO1\Final\C1\GraniteEdgeAI.ModelHardwareCompatibility.Tests_net8.0_x64.trx` |
| Final UO1 packaged tests | 11 passed, 0 failed |
| Final UO1 TRX | `C:\UO1\TestResults\UO1\Final\uo1-final.trx` |
| Debug packaged inherited baseline | 956 total, 910 passed, 46 inherited failures |
| Baseline TRX | `C:\UO1\TestResults\UO1\Baseline\baseline-debug.trx` |
| Debug test project build | succeeded, 0 errors |
| Whitespace check | no errors |

The 46 full-suite failures were present at the exact C1 base before any UO1
change. They cover inherited Model Inspection worker handshake/layout timeout,
compatibility XAML parse and geometry-tolerance cases. The packaged Release
baseline is also blocked before UO1 by inherited debug-only compatibility
types/XAML. UO1's filtered packaged suite is fully green.

During final verification, an independently running Visual Studio update
replaced SDK 10.0.301 with 10.0.400 and changed WinUI workload availability.
The final C1 preflight was therefore run with an ignored test-artifact
`global.json` selecting the installed 10.0.400 SDK and the repository's
Microsoft Testing Platform runner. It passed the same 659 tests. No system
installation state was modified by UO1.

## Scope audit

Compare the final branch with the exact C1 base:

```powershell
git diff --name-only f999443279ae3505f7df1c686f98c5163c190acd..HEAD
```

Production changes are confined to
`IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/**`.
Additional changes are only component-local tests and this handoff. No route
executor, shared navigation, application/project file, or shared resource was
changed. The branch has not been pushed or merged.
