# Fixtures

Contains small controlled inputs used by automated tests.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Contains small controlled inputs used by automated tests.

### Start here

These files test the testing framework. Start with the parent test guide, then choose `unit`, `integration` or `acceptance` according to the scope you need to check.

### How this folder fits into testing

This folder belongs to the tooling layer. It helps create or check evidence but is not evidence by itself.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`atomicbot_compact_timing.txt`](atomicbot_compact_timing.txt) | Plain-text evidence or diagnostic output for atomicbot compact timing; read it as supporting detail, not as the final conclusion. | Supporting repository file |
| [`fake_atomicbot_runtime.py`](fake_atomicbot_runtime.py) | Python module for fake atomicbot runtime. | Executable or importable tooling |
| [`fake_llama_server.py`](fake_llama_server.py) | Python module providing `Handler`. | Executable or importable tooling |
| [`measurement_child.py`](measurement_child.py) | Python module for measurement child. | Executable or importable tooling |

### Important boundaries

- Read command help before running a script. Commands named run, build, generate, convert, publish or finalise may create or change outputs.
- Passing tests for this tooling does not mean a model benchmark or application evaluation passed.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
