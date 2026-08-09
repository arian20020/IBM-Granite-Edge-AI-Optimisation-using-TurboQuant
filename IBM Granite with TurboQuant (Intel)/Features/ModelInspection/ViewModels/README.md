# Model Inspection ViewModel

**Status:** Implemented with atomic snapshots for one replaceable inspection journey
**Last reviewed:** 2026-08-09

[Back to Model Inspection architecture](../README.md)

## Purpose

`ModelInspectionViewModel` owns one exact immutable navigation request and the
application state for replaceable asynchronous inspection attempts. It exposes
application contracts and commands; it does not know WinUI controls, worker
protocol types, or native runtime details.

`Snapshot` is the single observable state boundary. Each immutable
`ModelInspectionViewSnapshot` combines active/cancellation state with either
current progress or a terminal result and carries a
`ModelInspectionRenderKey`. Observers receive exactly one `Snapshot` property
notification for each accepted semantic mutation instead of sampling separate
properties. The legacy `Progress`, `Result`, and `IsRunActive` getters delegate
to the current snapshot during the presentation migration.

## Attempt lifecycle

```text
Start or Retry
    -> publish a fresh attempt generation at revision zero
    -> retire/cancel any replaced attempt
    -> clear prior progress and result
    -> stream distinct progress revisions for the current attempt only
    -> publish cancellation-requested at the next revision
    -> retire progress and the attempt before publishing a terminal revision
```

Progress and terminal callbacks from retired attempts are ignored. This
prevents a cancelled or navigated-away run from repainting a newer page state.
Equal duplicate progress is not a semantic mutation and does not advance the
revision. A direct active-attempt replacement and a terminal-state Retry both
advance the generation before synchronous callbacks can observe state.
Notifications are marshalled to the synchronization context captured at
construction when one exists.

## Commands

- `CancelCommand` is enabled only while the current attempt can still be
  cancelled and disables immediately when cancellation is requested.
- `RetryCommand` starts a new attempt for the same exact request after a
  terminal result.
- `ChooseAnotherCommand` advances the generation and clears progress/terminal
  state before cancelling the active attempt and raising
  `ChooseAnotherRequested`.

An `OperationCanceledException` is not treated as trusted cancellation because
only a cooperative worker terminal can prove that state. It becomes the stable
privacy-safe `MI-OP-CANCELLATION-UNCONFIRMED` operational failure. Unexpected
service exceptions become `MI-OP-SERVICE-UNEXPECTED` without exception-derived
technical detail.

`Deactivate()` advances the generation and invalidates callbacks before
requesting cancellation. The page's subsequent `Dispose()` participates in
the same lifecycle retirement, so that pair publishes only one invalidation.
`Dispose()` remains idempotent, disables commands, cancels active work, and
clears the choose-another event subscription.

## Ownership boundary and non-claims

The ViewModel does not inspect files, launch processes, classify evidence,
construct WinUI presentation objects, or navigate the onboarding shell. It
does not add OpenVINO, TurboQuant, Vulkan/GPU, context creation, inference,
benchmarking, conversion, Hardware Fit, or chat behavior.

## Tests

- `DelegateCommandTests`
- `ModelInspectionViewSnapshotTests`
- `ModelInspectionViewModelTests`

The focused suite covers snapshot invariants, exact generation/revision keys,
single notifications, command state, auto-replacement semantics, cancellation,
retry, stale and duplicate progress/results, UI-context notification,
deactivation, choose-another ordering, privacy-safe unexpected failure,
disposal, and sequence-exhaustion transactions that leave the prior state
untouched when no further generation or revision can be represented.
