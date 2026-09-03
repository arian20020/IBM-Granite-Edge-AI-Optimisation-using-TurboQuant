# Reporting

Builds and validates the common Markdown, DOCX, PDF and data reports.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Builds and validates the common Markdown, DOCX, PDF and data reports.

### Start here

Start with the [testing CLI guide](../cli/README.md). These modules are internal building blocks for report generation and validation.

### How this folder fits into testing

This folder belongs to the tooling layer. It helps create or check evidence but is not evidence by itself.

### Generated child folders

**Python cache folders:** 1 folder(s), for example `__pycache__/`. These are temporary Python bytecode caches. They are not source files or test evidence and do not receive README files.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`__init__.py`](__init__.py) | Marks this directory as a Python package and may expose its public imports. | Executable or importable tooling |
| [`comparison.py`](comparison.py) | Guarded cross-route comparisons and collection-wide catalogs. | Executable or importable tooling |
| [`csvio.py`](csvio.py) | Deterministic CSV and JSON output for final-results artifacts. | Executable or importable tooling |
| [`docx_renderer.py`](docx_renderer.py) | Professional, deterministic DOCX rendering for canonical final-results reports. | Executable or importable tooling |
| [`evidence.py`](evidence.py) | Portable evidence provenance and checksum-manifest helpers. | Executable or importable tooling |
| [`layout.py`](layout.py) | Declarative migration rules for the compact published-route layout. | Executable or importable tooling |
| [`llama_adapter.py`](llama_adapter.py) | Evidence-bound normalization and publication for historical llama.cpp routes. | Executable or importable tooling |
| [`markdown_renderer.py`](markdown_renderer.py) | Deterministic canonical Markdown rendering for final-results reports. | Executable or importable tooling |
| [`models.py`](models.py) | Immutable, normalized records used by the final-results pipeline. | Executable or importable tooling |
| [`openvino_adapter.py`](openvino_adapter.py) | Normalize the final OpenVINO campaigns into canonical route records. | Executable or importable tooling |
| [`openvino_report.py`](openvino_report.py) | Evidence-bound master reports for normalized OpenVINO campaigns. | Executable or importable tooling |
| [`parity.py`](parity.py) | Semantic parity comparison for canonical Markdown and generated DOCX reports. | Executable or importable tooling |
| [`report_model.py`](report_model.py) | Rendering-neutral blocks for canonical final-results reports. | Executable or importable tooling |
| [`reproduction.py`](reproduction.py) | Contributor-facing, read-only release verification guidance. | Executable or importable tooling |
| [`validate.py`](validate.py) | Read-only validation gates for unified final-results route collections. | Executable or importable tooling |
| [`workbook_portability.py`](workbook_portability.py) | Create deterministic portable XLSX derivatives without changing source evidence. | Executable or importable tooling |

### Important boundaries

- Read command help before running a script. Commands named run, build, generate, convert, publish or finalise may create or change outputs.
- Passing tests for this tooling does not mean a model benchmark or application evaluation passed.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
