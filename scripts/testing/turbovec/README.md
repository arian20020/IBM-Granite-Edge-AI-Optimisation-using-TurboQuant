# TurboVec

This folder covers the TurboVec feasibility experiment.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

This folder covers the TurboVec feasibility experiment.

### Start here

Start with [`../run_turbovec_feasibility.py`](../run_turbovec_feasibility.py) for the controlled campaign. These modules implement its individual stages.

### How this folder fits into testing

This folder belongs to the tooling layer. It helps create or check evidence but is not evidence by itself.

### Generated child folders

**Python cache folders:** 1 folder(s), for example `__pycache__/`. These are temporary Python bytecode caches. They are not source files or test evidence and do not receive README files.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`__init__.py`](__init__.py) | Marks this directory as a Python package and may expose its public imports. | Executable or importable tooling |
| [`chunking.py`](chunking.py) | Deterministic project-authored granite-chunker-v1 implementation. | Executable or importable tooling |
| [`contracts.py`](contracts.py) | Closed contracts and entry-gate validation for EXP-TV-COMP-001. | Executable or importable tooling |
| [`decision.py`](decision.py) | Pure Gate A four-outcome policy. | Executable or importable tooling |
| [`embedding.py`](embedding.py) | Granite embedding adapters for the isolated feasibility campaign. | Executable or importable tooling |
| [`indexes.py`](indexes.py) | Matched Exact FP32 and TurboVec index adapters. | Executable or importable tooling |
| [`metrics.py`](metrics.py) | Pure retrieval and latency metrics. | Executable or importable tooling |
| [`provenance.py`](provenance.py) | Byte-accurate provenance helpers. | Executable or importable tooling |
| [`runner.py`](runner.py) | Append-only matched runner for EXP-TV-COMP-001. | Executable; may create or change outputs |

### Important boundaries

- Read command help before running a script. Commands named run, build, generate, convert, publish or finalise may create or change outputs.
- Passing tests for this tooling does not mean a model benchmark or application evaluation passed.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
