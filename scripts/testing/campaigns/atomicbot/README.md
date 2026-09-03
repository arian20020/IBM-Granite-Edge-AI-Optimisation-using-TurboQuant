# AtomicBot Campaign Modules

Implements the lower-level campaign logic for the AtomicBot TurboQuant route. Use the supported CLI instead of calling these modules directly.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Implements the lower-level campaign logic for the AtomicBot TurboQuant route. Use the supported CLI instead of calling these modules directly.

### Start here

Most users should not call these modules directly. Start with the [supported CLI guide](../../cli/README.md), which selects the correct campaign module and forwards validated options.

### How this folder fits into testing

This folder belongs to the tooling layer. It helps create or check evidence but is not evidence by itself.

### Generated child folders

**Python cache folders:** 1 folder(s), for example `__pycache__/`. These are temporary Python bytecode caches. They are not source files or test evidence and do not receive README files.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`__init__.py`](__init__.py) | Marks this directory as a Python package and may expose its public imports. | Executable or importable tooling |
| [`matrix.py`](matrix.py) | Strict AtomicBot retest matrix loading. | Executable or importable tooling |
| [`metrics.py`](metrics.py) | AtomicBot runtime metric parsing and aggregation. | Executable or importable tooling |
| [`quality.py`](quality.py) | Blind-label quality scoring and controlled perplexity fixture gates. | Executable or importable tooling |
| [`reconcile.py`](reconcile.py) | Source-level reconciliation for WB-02 fields. | Executable or importable tooling |
| [`runner.py`](runner.py) | Selection, identifiers, and bounded process execution for AtomicBot retests. | Executable; may create or change outputs |
| [`safety.py`](safety.py) | Pre-launch safety gates for high-memory AtomicBot cases. | Executable or importable tooling |
| [`state.py`](state.py) | Crash-safe AtomicBot run state persistence. | Executable or importable tooling |
| [`utilization.py`](utilization.py) | Utilisation sample parsing and summary helpers for AtomicBot runs. | Executable or importable tooling |

### Important boundaries

- Read command help before running a script. Commands named run, build, generate, convert, publish or finalise may create or change outputs.
- Passing tests for this tooling does not mean a model benchmark or application evaluation passed.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
