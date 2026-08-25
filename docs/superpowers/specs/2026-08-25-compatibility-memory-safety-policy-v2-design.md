# Compatibility memory safety policy v2

## Status

Implemented on `fix/hardware-inspection-loq-baseline` for the production
Model/Hardware Compatibility engine. The v1 policy remains available so prior
fixtures and evidence can still be reproduced.

## Problem

Hardware Inspection supplies a fresh Windows available-physical-memory value.
That value already excludes memory currently used by Windows, drivers, this
application, and other running applications. The v1 fit policy then removed a
fixed 2 GiB operating-system allowance and a fixed 1 GiB operational reserve
from that available value. This counted current use twice and could turn a
credible multi-gigabyte available-memory reading into a misleading zero budget.

## Decision

Production uses `fit-safety-policy-v2`:

- Start with the fresh Windows available-physical-memory value supplied at the
  compatibility boundary.
- Hold back 10% of that available pool, with a 512 MiB minimum reserve.
- Do not subtract another fixed operating-system or application allowance.
- Continue adding the estimator uncertainty margin independently: 10% of the
  predicted peak, with a 512 MiB minimum.
- Saturate at zero when the available pool is smaller than the reserve.
- Re-read hardware/memory evidence on a new compatibility check; never reuse a
  stale value as if it were current.

The resulting safe budget is therefore:

`max(0, current available physical memory - max(10% of current available physical memory, 512 MiB))`

The predicted peak still includes its separate uncertainty margin before it is
compared with that safe budget.

## User experience

When no verified setup fits, the screen must identify the result as a memory
warning and show both the lightest estimated requirement and the current safe
budget. It advises the user to close unused applications and browser tabs and
then check again. This is an estimate, not a measured inference run.

The application must not terminate other applications. A future memory-help
feature may identify large consumers or open Task Manager, but closing a user
process requires an explicit user choice and confirmation.

## Visual corrections coupled to this outcome

- Hide the model-summary card when no path-private display name/detail was
  supplied, instead of rendering an empty card.
- Resolve generated row colours against the page's explicit light theme even
  when the host application theme has not finished propagating during page
  construction.
- Keep runtime rows, badges, cards, estimates, and warnings readable in the
  established light onboarding shell.

## Verification

- Unit tests pin the v1 fixed allowance and the v2 proportional/floor cases.
- Production-engine tests pin selection of policy version v2.
- Packaged WinUI tests pin the explicit warning, hidden empty card, and readable
  generated-row foreground.
- A native packaged walkthrough uses an actual GGUF file through Model Import,
  Model Inspection, Hardware Inspection, and the compatibility result.
