# Workbook 05 Phase 3 Binding Implementation Decisions

**Status:** Authoritative companion to the approved design and roadmap  
**Date:** 13 August 2026  
**Implementation status:** Not started

These decisions resolve implementation choices that must not be left to an individual package worker. Where an example or broader sentence in C1–C5 conflicts with this file, this file controls.

## 1. Granite remote code is disabled

The primary C1 conversion command must not pass `--trust-remote-code`, and model/tokenizer loading must use `trust_remote_code=False` where the API exposes that option.

The official `ibm-granite/granite-4.1-3b` usage example loads the model through standard `transformers.pipeline`, `AutoTokenizer`, and `AutoModelForCausalLM` calls without enabling remote code. Therefore, executing repository-supplied Python code is unnecessary for the reviewed first conversion candidate.

If a pinned conversion tool later reports that remote code is genuinely required, C1 records that attempt as failed or blocked. Enabling remote code would require a separate reviewed security decision that identifies the immutable source files, hashes every executable Python file, explains why the standard loader is insufficient, and creates a new conversion identity. It must not be added as an automatic fallback.

## 2. C3 JSON parser is the accepted GenAI parser

The C++ probe parses its closed request using:

```cpp
ov::genai::JsonContainer::from_json_string(request_text)
```

from the exact accepted OpenVINO GenAI API. The probe adds no JSON package, performs no CMake network fetch, and does not implement an ad-hoc parser. Python JSON Schema validation remains the outer contract; the C++ parser repeats the security-critical closed-field and type checks before constructing the pipeline.

## 3. C++ contract tests add no test-framework dependency

C3 C++ contract tests use project-owned standard-library test executables:

```cpp
int main() {
    run_request_tests();
    cache_property_tests();
    return 0;
}
```

Each named test throws or returns a nonzero result on failure and prints a stable test identifier. The `TEST_CASE` snippets in the C3 plan express intended behavior only; they do not authorise Catch2, GoogleTest, or another dependency. CTest registers the project-owned executables.

## 4. Bundle validators reuse existing primitives

There is no new `bundle_policy.py`. C1–C5 validators reuse the stable hash-manifest, path, payload, secret-pattern, and `BundleIssue` behavior from:

```text
scripts/testing/workbook05/bundle_validation.py
```

Each package adds only its own schema and cross-record scientific checks.

## 5. Route fields remain distinct

Where a record contains both concepts:

```text
route_id = route-a-merged-openvino
route    = official-openvino
```

`route_id` identifies the Workbook 05 engineering route. `route` preserves compatibility with the existing measured/smoke record vocabulary. Neither value may be silently substituted for the other.

## 6. Dependency-lock generation is separate from runtime execution

`pip-tools` is used only in a clean dependency-lock generation environment. The generated hash-locked requirements file is reviewed and committed. Live C1 installs use only that lock with hash checking; they do not resolve a moving dependency graph and do not install `pip-tools` into the model-conversion runtime environment.

## 7. No plan example authorises a claim

Example JSON, hashes, model metadata, metrics, paths, and statuses in the package plans are test fixtures or required shapes. They are not observed evidence. Only live records that pass the package schema, cross-record validator, hosted validation, and project-owner digest acceptance may promote a claim.