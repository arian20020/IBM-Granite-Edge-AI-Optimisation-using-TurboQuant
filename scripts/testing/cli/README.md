# CLI Commands

Provides the supported command line entry points for running and validating tests.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Provides the supported command line entry points for running and validating tests.

### Start here

Read the [parent command guide](../README.md), then run the chosen command with `--help`. `validate_results.py` is the read-only entry point for checking an existing final-results tree.

### How this folder fits into testing

This folder belongs to the tooling layer. It helps create or check evidence but is not evidence by itself.

### Generated child folders

**Python cache folders:** 1 folder(s), for example `__pycache__/`. These are temporary Python bytecode caches. They are not source files or test evidence and do not receive README files.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`__init__.py`](__init__.py) | Marks this directory as a Python package and may expose its public imports. | Executable or importable tooling |
| [`build_results.py`](build_results.py) | Build or validate the unified final-results collection. | Executable; may create or change outputs |
| [`export_report.ps1`](export_report.ps1) | Runs the export report PowerShell workflow. | Executable or importable tooling |
| [`run_animehacker.py`](run_animehacker.py) | Run supported animehacker testing commands. | Executable; may create or change outputs |
| [`run_atomicbot.py`](run_atomicbot.py) | Run supported AtomicBot testing commands. | Executable; may create or change outputs |
| [`run_llama.py`](run_llama.py) | Run supported upstream llama.cpp measurement commands. | Executable; may create or change outputs |
| [`run_openvino.py`](run_openvino.py) | Run supported OpenVINO testing commands. | Executable; may create or change outputs |
| [`validate_results.py`](validate_results.py) | Validate an existing final-results collection without publishing receipts. | Executable or importable tooling |

### Important boundaries

- Read command help before running a script. Commands named run, build, generate, convert, publish or finalise may create or change outputs.
- Passing tests for this tooling does not mean a model benchmark or application evaluation passed.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
