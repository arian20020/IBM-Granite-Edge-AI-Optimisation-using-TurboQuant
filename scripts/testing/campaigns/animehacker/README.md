# animehacker Campaign Modules

Implements the lower-level campaign logic for the animehacker TQ3_0 route. Use the supported CLI instead of calling these modules directly.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Implements the lower-level campaign logic for the animehacker TQ3_0 route. Use the supported CLI instead of calling these modules directly.

### Start here

Most users should not call these modules directly. Start with the [supported CLI guide](../../cli/README.md), which selects the correct campaign module and forwards validated options.

### How this folder fits into testing

This folder belongs to the tooling layer. It helps create or check evidence but is not evidence by itself.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`__init__.py`](__init__.py) | Marks this directory as a Python package and may expose its public imports. | Executable or importable tooling |
| [`activation.py`](activation.py) | Evidence-based cache and backend activation classification. | Executable or importable tooling |
| [`build_reconcile.py`](build_reconcile.py) | Strict reconciliation helpers for animehacker build evidence. | Executable; may create or change outputs |
| [`large_host.py`](large_host.py) | Safety and evidence primitives for completing guarded WB-03 rows on a large host. | Executable or importable tooling |
| [`matrix.py`](matrix.py) | Strict loading for the controlled animehacker TQ3_0 retest matrix. | Executable or importable tooling |
| [`runner.py`](runner.py) | Command construction, selection, and unique identifiers for WB-03 runs. | Executable; may create or change outputs |
| [`state.py`](state.py) | Crash-safe state persistence for the animehacker retest. | Executable or importable tooling |

### Important boundaries

- Read command help before running a script. Commands named run, build, generate, convert, publish or finalise may create or change outputs.
- Passing tests for this tooling does not mean a model benchmark or application evaluation passed.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
