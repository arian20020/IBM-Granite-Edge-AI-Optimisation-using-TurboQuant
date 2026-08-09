# Model Inspection ViewModel

**Status:** Implemented for one replaceable inspection journey
**Last reviewed:** 2026-08-09

[Back to Model Inspection architecture](../README.md)

## Purpose

`ModelInspectionViewModel` owns one exact immutable navigation request and the
application state for replaceable asynchronous inspection attempts. It exposes
application contracts and commands; it does not know WinUI controls, worker
protocol types, or native runtime details.

## Attempt lifecycle

```text
Start or Retry
    -> publish a fresh attempt identity
    -> retire/cancel any replaced attempt
    -> clear prior progress and result
    -> stream progress for the current attempt only
    -> retire the attempt before publishing its terminal result
```

Progress and terminal callbacks from retired attempts are ignored. This
prevents a cancelled or navigated-away run from repainting a newer page state.
Notifications are marshalled to the synchronization context captured at
construction when one exists.

## Commands

- `CancelCommand` is enabled only while the current attempt can still be
  cancelled and disables immediately when cancellation is requested.
- `RetryCommand` starts a new attempt for the same exact request after a
  terminal result.
- `ChooseAnotherCommand` invalidates/cancels the active attempt before raising
  `ChooseAnotherRequested`.

An `OperationCanceledException` is not treated as trusted cancellation because
only a cooperative worker terminal can prove that state. It becomes the stable
privacy-safe `MI-OP-CANCELLATION-UNCONFIRMED` operational failure. Unexpected
service exceptions become `MI-OP-SERVICE-UNEXPECTED` without exception-derived
technical detail.

`Deactivate()` invalidates callbacks before requesting cancellation.
`Dispose()` is idempotent, disables commands, cancels active work, and clears
the choose-another event subscription.

## Ownership boundary and non-claims

The ViewModel does not inspect files, launch processes, classify evidence,
construct WinUI presentation objects, or navigate the onboarding shell. It
does not add OpenVINO, TurboQuant, Vulkan/GPU, context creation, inference,
benchmarking, conversion, Hardware Fit, or chat behavior.

## Tests

- `DelegateCommandTests`
- `ModelInspectionViewModelTests`

The focused suite covers command state, auto-replacement semantics,
cancellation, retry, stale progress/results, UI-context notification,
deactivation, choose-another ordering, privacy-safe unexpected failure, and
disposal.
