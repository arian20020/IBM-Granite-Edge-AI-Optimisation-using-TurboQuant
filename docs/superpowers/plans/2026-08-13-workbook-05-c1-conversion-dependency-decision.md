# C1 Conversion Dependency Decision

**Status:** Authoritative C1 correction  
**Date:** 13 August 2026  
**Implementation status:** Not started

This decision replaces the illustrative C1 conversion dependency versions. The earlier example combined `optimum-intel==2.0.0` with versions that its own dependency constraints cannot resolve. Implementation must not copy that example into a live environment.

## Exact reviewed source and package set

```text
optimum-intel @ git+https://github.com/huggingface/optimum-intel.git@a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0
optimum @ git+https://github.com/huggingface/optimum.git@982e495540364f95da1e4b6f62d2d4e5907d08fd
transformers==5.5.0
huggingface-hub==1.21.0
nncf==3.2.0
openvino==2026.2.1
openvino-tokenizers==2026.2.1.0
```

The selected `optimum-intel` source commit declares:

```text
optimum~=2.3.0
transformers>=4.51,<5.6
huggingface-hub>=0.23.2,<1.22
nncf>=2.19.0
openvino>=2026.0
openvino-tokenizers>=2026.0
```

The pinned `optimum` commit is the official `v2.3.0` release commit. The remaining direct package versions fit those declared ranges and form one reviewable candidate environment.

This is still a candidate until the clean dependency preflight passes. A written plan is not executable evidence.

## Why the earlier example is rejected

The C1 plan previously illustrated:

```text
optimum-intel==2.0.0
transformers==5.14.1
huggingface-hub==1.24.0
nncf==3.2.0
```

That set is invalid because released `optimum-intel==2.0.0` constrains Transformers below `5.1`. It also does not provide a coherent reason to select the other direct versions together. The implementation must not try to repair that set by letting `pip` choose moving transitive versions.

## Required dependency preflight

C1 must complete this preflight before any Granite source snapshot is downloaded:

1. Verify both VCS repositories, origins, full commit SHAs, clean trees, and complete source-tree SHA-256 manifests.
2. Create a fresh Python `3.12.10` virtual environment in a new normal `C:\w5c` workspace.
3. Generate a complete dependency lock from the exact direct set. Hash every normal wheel or source distribution supported by the installer; retain the two immutable VCS commit identities and complete source-tree manifests separately.
4. Install only from the reviewed lock and immutable VCS commits. Do not install an unbounded or moving dependency.
5. Record Python, pip, package, executable, source-commit, wheel, and source-tree identities.
6. Run these import checks in a new Python process:

```python
import optimum
import optimum.intel
import transformers
import nncf
import openvino
```

7. Run:

```text
optimum-cli --help
```

and require exit code `0`.
8. Run the repository-controlled conversion argument-construction and no-model compatibility tests. No model path is accessed during this step.
9. Confirm that the primary conversion command does not contain `--trust-remote-code` and that loader configuration retains `trust_remote_code=False` where supported.
10. Write a schema-validated dependency-preflight decision and independently validate its text-only evidence bundle.

## Failure and drift rules

- Resolver conflict, import failure, CLI failure, source mismatch, hash mismatch, unsafe path, or unexpected package version classifies C1 as `Blocked` or `IntegrityFailure` as defined by the package contracts.
- The process does not automatically choose newer, older, or alternative versions.
- A different dependency combination requires its own reviewed decision, exact identity, tests, and evidence. It cannot overwrite this attempt.
- No model download, conversion, model execution, or scientific claim is authorised until this preflight passes and its artifact is accepted.

## Required C1 plan interpretation

Where the C1 package plan names the earlier direct versions, workers must use this file instead. The C1 test suite must include a dependency-contract test that compares the reviewed direct set and VCS commits exactly, checks the declared compatibility ranges from the pinned source, and rejects the superseded combination.

## Non-claims

This decision does not prove that the candidate environment resolves, that Granite converts, that the converted model loads, or that OpenVINO inference succeeds. Those remain executable C1 and later package gates.