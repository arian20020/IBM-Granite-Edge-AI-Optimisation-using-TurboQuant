# Tests

Tests the testing tools themselves. These are not model benchmark results.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Tests the testing tools themselves. These are not model benchmark results.

### Start here

These files test the testing framework. Start with the parent test guide, then choose `unit`, `integration` or `acceptance` according to the scope you need to check.

### How this folder fits into testing

This folder belongs to the tooling layer. It helps create or check evidence but is not evidence by itself.

### Folders

| Folder | What it contains |
| --- | --- |
| [`acceptance/`](acceptance/README.md) | Checks complete contributor-facing workflows and release behaviour. |
| [`fixtures/`](fixtures/README.md) | Contains small controlled inputs used by automated tests. |
| [`integration/`](integration/README.md) | Checks that several testing components work together. |
| [`unit/`](unit/README.md) | Checks small functions and modules in isolation. |

### Generated child folders

**Python cache folders:** 1 folder(s), for example `__pycache__/`. These are temporary Python bytecode caches. They are not source files or test evidence and do not receive README files.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`test_turbovec_chunking.py`](test_turbovec_chunking.py) | Python module providing `canonical_hash`, `ChunkingTests`. | Executable or importable tooling |
| [`test_turbovec_contracts.py`](test_turbovec_contracts.py) | Python module providing `TurboVecControlContractTests`. | Executable or importable tooling |
| [`test_turbovec_decision.py`](test_turbovec_decision.py) | Python module providing `candidate`, `DecisionTests`. | Executable or importable tooling |
| [`test_turbovec_decision_documents.py`](test_turbovec_decision_documents.py) | Python module providing `DecisionDocumentTests`. | Executable or importable tooling |
| [`test_turbovec_embedding.py`](test_turbovec_embedding.py) | Python module providing `FakeRawProvider`, `EmbeddingTests`. | Executable or importable tooling |
| [`test_turbovec_evidence_scripts.py`](test_turbovec_evidence_scripts.py) | Python module providing `pwsh`, `EvidenceScriptTests`. | Executable or importable tooling |
| [`test_turbovec_indexes.py`](test_turbovec_indexes.py) | Python module providing `unit_vectors`, `IndexContractTests`. | Executable or importable tooling |
| [`test_turbovec_metrics.py`](test_turbovec_metrics.py) | Python module providing `MetricsTests`. | Executable or importable tooling |
| [`test_turbovec_runner.py`](test_turbovec_runner.py) | Python module providing `FailingProvider`, `RunnerTests`. | Executable or importable tooling |

### Important boundaries

- Read command help before running a script. Commands named run, build, generate, convert, publish or finalise may create or change outputs.
- Passing tests for this tooling does not mean a model benchmark or application evaluation passed.

### Related guides

- [Parent guide](../README.md)
- [acceptance guide](acceptance/README.md)
- [fixtures guide](fixtures/README.md)
- [integration guide](integration/README.md)
- [unit guide](unit/README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
