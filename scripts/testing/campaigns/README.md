# Campaigns

Implements route-specific test campaign logic behind the supported command line tools.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Implements route-specific test campaign logic behind the supported command line tools.

### Start here

Choose a route folder only when maintaining campaign internals. To run a supported command, start with [`../cli/`](../cli/README.md).

### How this folder fits into testing

This folder belongs to the tooling layer. It helps create or check evidence but is not evidence by itself.

### Folders

| Folder | What it contains |
| --- | --- |
| [`animehacker/`](animehacker/README.md) | Implements the lower-level campaign logic for the animehacker TQ3_0 route. Use the supported CLI instead of calling these modules directly. |
| [`atomicbot/`](atomicbot/README.md) | Implements the lower-level campaign logic for the AtomicBot TurboQuant route. Use the supported CLI instead of calling these modules directly. |
| [`llama_cpp/`](llama_cpp/README.md) | Implements the lower-level campaign logic for the upstream llama.cpp route. Use the supported CLI instead of calling these modules directly. |
| [`openvino/`](openvino/README.md) | Implements the lower-level campaign logic for the OpenVINO routes. Use the supported CLI instead of calling these modules directly. |

### Generated child folders

**Python cache folders:** 1 folder(s), for example `__pycache__/`. These are temporary Python bytecode caches. They are not source files or test evidence and do not receive README files.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`__init__.py`](__init__.py) | Marks this directory as a Python package and may expose its public imports. | Executable or importable tooling |

### Important boundaries

- Read command help before running a script. Commands named run, build, generate, convert, publish or finalise may create or change outputs.
- Passing tests for this tooling does not mean a model benchmark or application evaluation passed.

### Related guides

- [Parent guide](../README.md)
- [animehacker guide](animehacker/README.md)
- [atomicbot guide](atomicbot/README.md)
- [llama_cpp guide](llama_cpp/README.md)
- [openvino guide](openvino/README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
