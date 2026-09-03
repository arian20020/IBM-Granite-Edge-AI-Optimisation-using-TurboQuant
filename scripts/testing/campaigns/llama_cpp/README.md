# llama.cpp Campaign Modules

Implements the lower-level campaign logic for the upstream llama.cpp route. Use the supported CLI instead of calling these modules directly.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Implements the lower-level campaign logic for the upstream llama.cpp route. Use the supported CLI instead of calling these modules directly.

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
| [`measure_run.py`](measure_run.py) | Launch one llama command and capture raw memory, KV, and TTFT evidence. | Executable or importable tooling |
| [`measure_server.py`](measure_server.py) | Measure loaded-runtime TTFT through llama-server's streaming endpoint. | Executable or importable tooling |
| [`parse_measurement.py`](parse_measurement.py) | Validate and summarize raw llama measurement events. | Executable or importable tooling |

### Important boundaries

- Read command help before running a script. Commands named run, build, generate, convert, publish or finalise may create or change outputs.
- Passing tests for this tooling does not mean a model benchmark or application evaluation passed.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
