# GGUF Quick Scanner Design

**Date:** 2026-07-24  
**Status:** Approved for implementation by the task brief  
**Branch:** `feature/winui-shell-model-import`

## Goal

Finish the in-project GGUF quick scanner used by the model-import workflow. The scanner will read only the fixed GGUF header and metadata key/value area, validate that untrusted binary structure defensively, extract the small set of display fields required by `ModelQuickScanResult`, and remain safe for large model files.

The scanner will not read tensor descriptors or tensor data, load a model for inference, modify the selected file, or introduce a general-purpose GGUF object model.

## Existing repository state

The current branch already provides the intended ownership flow:

```text
ModelImportPage
    -> ModelQuickScanner
        -> GgufQuickScanner.ScanAsync
            -> ModelQuickScanResult
```

`GgufQuickScanner` already validates a nonblank path, observes pre-requested cancellation, opens an asynchronous read-only `FileStream`, checks the four GGUF magic bytes, reads a little-endian `uint32` version, supports version 3, and returns stable failures for invalid magic and unsupported versions. It deliberately throws `NotImplementedException` after the version field.

`ModelQuickScanner` already keeps OpenVINO routing separate and converts only cancellation associated with the supplied token into a cancelled result. `ModelQuickScanResult` already enforces the success/failure/cancellation contracts needed by this feature. Those responsibilities will be preserved.

The packaged test project is valid and builds a `.build.appxrecipe`. The baseline investigation found two separate setup facts:

- the old repository-root parenthesis failure is gone: the current root is `IBM-Granite-TurboQuant-Intel`, and the generated recipe deploys successfully from that root;
- the current project copies only `I-002`, so the existing invalid-magic test fails because `I-001` is absent from the package;
- the first `vstest.console.exe` returned by the current `vswhere -all` command is Visual Studio 2022 and cannot provide a suitable runtime for this test package, while the installed Visual Studio 18 runner deploys it and executes the tests.

These are test-host/setup concerns, not scanner defects. Fixture deployment will use project-relative wildcards, and local/CI runner discovery will prefer the latest compatible Visual Studio installation.

## Responsibilities

### `ModelImportPage`

- gathers the model-format and file selection;
- displays the eventual quick-scan state;
- never parses GGUF bytes.

The page is outside the implementation scope of this scanner-only task.

### `ModelQuickScanner`

- routes `ModelFormatSelection.Gguf` to the real `GgufQuickScanner`;
- preserves the existing `None`, OpenVINO, unknown-format, and invalid-path behavior;
- catches `OperationCanceledException` only when the supplied token was cancelled;
- never duplicates binary parsing.

### `GgufQuickScanner`

- owns file opening, header parsing, metadata parsing, structural validation, known-field extraction, and controlled GGUF failures;
- uses asynchronous exact reads and explicit little-endian decoding;
- safely consumes all official metadata value types 0 through 12;
- skips irrelevant values without retaining them;
- never reads tensors or the whole file into memory.

### `ModelQuickScanResult`

- remains a result/data carrier;
- retains the current factories and property contract;
- receives only fully validated success values.

## Approaches considered

### 1. One streaming pass with bounded pending context candidates — chosen

Known fields are extracted while every metadata value is consumed. A context field that appears before `general.architecture` is retained as a tiny candidate containing only its key, declared type, and optional normalized integer value. Candidate retention has an explicit cap. Once the architecture is known, the matching `<architecture>.context_length` candidate is resolved.

This approach keeps file I/O sequential, validates each value once, avoids retaining an object model, and supports the required unusual metadata order.

### 2. Two complete metadata passes

The first pass would find the architecture and the second would extract the other fields. This avoids retaining context candidates, but it doubles validation and I/O work for potentially large tokenizer arrays and defeats the quick scanner’s sequential one-pass intent. It is not selected.

### 3. Deserialize a general GGUF model or add an external library

This would add a broader object graph and dependency surface than the feature needs, and could materialize large untrusted metadata. It is not selected.

## GGUF structure

The supported fixed header is 24 bytes:

| Offset | Size | Field | Encoding |
|---:|---:|---|---|
| 0 | 4 | magic | byte sequence `47 47 55 46` (`GGUF`) |
| 4 | 4 | version | `uint32`, little-endian |
| 8 | 8 | tensor count | `uint64`, little-endian |
| 16 | 8 | metadata key/value count | `uint64`, little-endian |

Version 3 is supported. The tensor count is read to advance and validate the complete header, but this quick scanner does not visit tensor descriptors.

A GGUF string is a little-endian `uint64` byte length followed by exactly that many UTF-8 bytes and no terminator.

Each metadata entry contains:

1. a GGUF string key;
2. a little-endian `uint32` value-type identifier;
3. the encoded value.

The official value identifiers are:

| ID | Type | Fixed payload size |
|---:|---|---:|
| 0 | `uint8` | 1 |
| 1 | `int8` | 1 |
| 2 | `uint16` | 2 |
| 3 | `int16` | 2 |
| 4 | `uint32` | 4 |
| 5 | `int32` | 4 |
| 6 | `float32` | 4 |
| 7 | boolean | 1, and only `0` or `1` is valid |
| 8 | string | variable |
| 9 | array | variable |
| 10 | `uint64` | 8 |
| 11 | `int64` | 8 |
| 12 | `float64` | 8 |

An array contains a `uint32` element type, a `uint64` element count, and that many encoded elements. The official GGUF specification explicitly permits arrays of arrays, so the implementation uses bounded recursive consumption.

## Parser structure

`ScanAsync` remains the public-internal entry point. Its high-level sequence is:

1. validate the path and pre-requested cancellation;
2. open an existing `FileStream` with `FileAccess.Read`, `FileShare.Read`, and `FileOptions.Asynchronous | FileOptions.SequentialScan`;
3. parse and validate the complete fixed header;
4. parse exactly the declared metadata entries;
5. validate required metadata and resolve the architecture-specific context;
6. construct `ModelQuickScanResult.CreateSuccess`.

Small helpers will each perform one readable operation:

- read an exact primitive-size buffer;
- decode little-endian `uint32` or `uint64`;
- read a bounded key or retained GGUF string;
- skip a validated byte range without seeking beyond `stream.Length`;
- consume one typed metadata value;
- consume a bounded array recursively;
- normalize context integers;
- map `general.file_type`;
- construct a controlled structural failure with field, offset, actual value, and expectation.

A small scanner-local format exception will carry stable failure details from deeply nested read helpers to the single result-construction boundary. It will be caught by exact type only. It will not catch programming exceptions or cancellation.

All asynchronous helpers receive the same `CancellationToken`. Metadata-entry loops and element-by-element array loops check cancellation explicitly.

## Safety limits

The following constants are part of the scanner’s explicit trust-boundary policy:

| Constant | Value | Kind | Rationale |
|---|---:|---|---|
| `MaxMetadataEntryCount` | `1_000_000` | required project limit | Rejects attacker-controlled top-level loops before iteration while leaving very generous format headroom. |
| `MaxMetadataKeyByteLength` | `65_535` | GGUF specification limit | The GGUF specification limits keys to `2^16 - 1` bytes. |
| `MaxMetadataStringByteLength` | `16 * 1024 * 1024` | application safety limit | A quick-scan display field or individual tokenizer string has no reason to allocate tens of MiB. Sixteen MiB is generous metadata headroom while bounding every `ulong`-declared allocation to a safe `int`. Unknown strings are validated against the same policy and skipped without allocation. |
| `MaxArrayElementCount` | `1_000_000` | application safety limit | Supports large tokenizer vocabularies while rejecting a single attacker-controlled loop larger than one million elements. Fixed-width, non-boolean arrays can be validated and skipped as one byte range. |
| `MaxTotalArrayElementCount` | `4_000_000` | application safety limit | Allows several large standard tokenizer arrays but prevents nested or repeated arrays from multiplying individually acceptable counts into unbounded total work. |
| `MaxArrayNestingDepth` | `8` | application safety limit | Standard metadata arrays are normally one-dimensional; eight levels provide substantial compatibility headroom while bounding recursive stack use. |
| `MaxPendingContextCandidateCount` | `64` | application safety limit | A file describes one architecture and normally one matching context field. Sixty-four candidates preserve unusual ordering and compatibility headroom without allowing a million retained candidate strings. |

Every declared `ulong` is compared to its applicable limit and, where meaningful, to the remaining bytes before narrowing. Allocation lengths use checked conversion only after the limit check.

Unknown fixed-size values are skipped only after validating `payloadLength <= stream.Length - stream.Position`. Unknown strings are not decoded or allocated. The parser never calculates an unchecked `position + length` and never seeks beyond EOF.

The test generator will create limit-plus-one and nested-budget fixtures so tests prove rejection occurs before allocation or uncontrolled iteration.

## Metadata extraction

### Name

`general.name` must be a string when present. A missing, empty, or whitespace-only value uses `Path.GetFileName(modelFilePath)`.

### Architecture

`general.architecture` is required, must be a string, and must be nonblank. Missing or blank architecture returns `missing-required-architecture`; a different value type returns `invalid-architecture-type`.

### Size label

`general.size_label` is optional and must be a string when present. A missing or blank value is normalized to `null`. A different value type returns `invalid-size-label-type`.

### Quantization

`general.file_type` is optional and must be `uint32` when present. Its mapping is isolated in `MapFileTypeToQuantization`.

The map follows the current official llama.cpp `llama_ftype` values, including `15 -> Q4_K_M`, `40 -> Q1_0`, and the current active `41 -> Q2_0`. Historical removed values retain their documented labels when the official GGUF specification names them. A numeric value with no current official label returns `Unknown (file type N)` rather than failing or silently discarding the value. Missing metadata returns `null`. A different metadata type returns `invalid-file-type`.

### Context length

The desired key is `<architecture>.context_length`. It may occur before or after `general.architecture`. A matching value may be `uint32` or `uint64` and is normalized to `ulong`. A missing matching value returns `null`. A matching value of another type returns `invalid-context-type`.

Only bounded context candidates are retained before the architecture becomes known; unrelated metadata values are consumed and discarded.

### Duplicate scanner-relevant keys

Scanner-relevant metadata uses deterministic first-occurrence-wins semantics. The first `general.name`, `general.architecture`, `general.size_label`, `general.file_type`, and exact `<architecture>.context_length` value establishes the result state. A later duplicate never replaces that state, but its complete declared payload is still structurally consumed and validated.

The policy remains bounded: fixed Boolean flags track the four `general.*` fields and the resolved context field; the scanner does not retain a set of arbitrary metadata keys. Before architecture is known, the existing dictionary retains at most 64 distinct context-suffix candidates and preserves the first occurrence of each exact candidate key. Therefore a first matching candidate with the wrong type still returns `invalid-context-type`, while a later duplicate cannot repair or replace it. Ignoring duplicate `general.architecture` values also prevents architecture and context from describing different models.

## Encoding and key handling

Retained strings use `UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)`. Invalid UTF-8 returns a controlled `invalid-metadata-encoding` failure instead of replacement characters.

Metadata keys are decoded strictly and validated against the GGUF hierarchical-key grammar: one or more nonempty `lower_snake_case` segments separated by single dots, equivalently `[a-z0-9_]+(\.[a-z0-9_]+)*`. Empty keys, leading/trailing dots, adjacent dots, uppercase letters, spaces, controls, and non-ASCII characters return `invalid-metadata-key`. Diagnostics report only the entry, byte offset, character index/code, or empty-segment rule and never echo an invalid raw key. The scanner does not enforce a narrower registry of known key names, so valid namespaced unknown keys remain forward-compatible.

## Failure contract

Structural failures return:

- `Outcome == Failure`;
- a stable nonblank kebab-case `FailureCode`;
- a concise nontechnical `UserMessage`;
- a `TechnicalMessage` naming the field or stage, byte offset when available, actual value, and expected value or limit.

Required and additional scanner codes are:

| Code | Meaning |
|---|---|
| `truncated-header` | Fewer than 24 bytes were available for a complete fixed header. |
| `invalid-magic` | Bytes 0–3 were not `GGUF`. |
| `unsupported-version` | The header version was not 3. |
| `excessive-metadata-count` | The declared metadata entry count exceeded one million. |
| `metadata-key-too-long` | A key length exceeded 65,535 bytes. |
| `invalid-metadata-key` | A key did not match the nonempty hierarchical `lower_snake_case` grammar. |
| `unsupported-metadata-type` | A value or array-element type was outside 0–12. |
| `truncated-metadata` | A metadata field or value ended before its declared payload. |
| `missing-required-architecture` | `general.architecture` was absent or blank. |
| `invalid-architecture-type` | `general.architecture` was not a string. |
| `invalid-name-type` | `general.name` was present but not a string. |
| `invalid-size-label-type` | `general.size_label` was present but not a string. |
| `invalid-file-type` | `general.file_type` was present but not `uint32`. |
| `invalid-context-type` | The matching architecture context field was neither `uint32` nor `uint64`. |
| `invalid-boolean-value` | A boolean payload was neither 0 nor 1. |
| `invalid-metadata-encoding` | A retained key/string contained invalid UTF-8. |
| `metadata-string-too-long` | A metadata string exceeded 16 MiB. |
| `excessive-array-count` | One array exceeded one million elements or total declared elements exceeded four million. |
| `excessive-array-depth` | Nested arrays exceeded eight levels. |
| `excessive-context-candidate-count` | More than 64 pre-architecture context candidates would have been retained. |
| `file-not-found` | The selected file disappeared or its directory was unavailable before opening. |
| `file-access-denied` | Windows denied read access to the selected file. |
| `file-read-error` | Another operational `IOException` prevented scanning. |

No partial metadata is returned after a structural failure.

## Exception and cancellation boundary

Path preconditions remain programmer/API contract exceptions:

- null, empty, or whitespace path throws `ArgumentException` (with the runtime’s null specialization);
- a pre-cancelled token throws `OperationCanceledException` from `GgufQuickScanner`.

Expected file-system races are user-operational conditions, so `GgufQuickScanner` converts only `FileNotFoundException`/`DirectoryNotFoundException`, `UnauthorizedAccessException`, and unrelated `IOException` into the stable file codes above. Structural `EndOfStreamException` is translated close to the exact read into `truncated-header` or `truncated-metadata`.

`OperationCanceledException` is never converted by `GgufQuickScanner`. `ModelQuickScanner` retains its narrow catch filter and returns `CreateCancelled` only when the caller’s token was actually cancelled. There is no `catch (Exception)`.

## Fixtures and test strategy

The existing PowerShell generators remain the sole source of binary fixtures. New behavior will be represented by generator changes, regenerated binaries, and expected JSON where appropriate. Binary files will never be hand-edited.

The test project will copy these repository-relative groups with `PreserveNewest`:

- `tests/TestFixtures/GGUF/**/*.gguf`;
- `tests/TestFixtures/Malformed/**/*.gguf`;
- `tests/TestFixtures/ExpectedMetadata/**/*.json`.

The valid fixture set retains `V-001` through `V-008`; adds `V-011` for the current `41 -> Q2_0` mapping and `V-012` for duplicate scanner-relevant metadata; and will add generated `V-009`/`V-010` coverage for every official metadata type, nested arrays, file type 40, and an unknown numeric file type. Malformed additions will cover the string, array-count, total-array-budget, nesting-depth, context-type, candidate-count, strict-encoding, and known-optional-type boundaries. Generated `I-025` through `I-028` specifically cover an empty key, an empty hierarchical segment, a space, and an uppercase character.

Direct tests use the real scanner, explicit fixture-exists assertions, Arrange–Act–Assert structure, `[TestMethod]`, and `[TestCategory("Unit")]`. Successful fixtures are compared with a small typed JSON expectation DTO. Tests observe only `ScanAsync` results and exceptions; private parsing helpers are not tested directly.

Router tests retain all existing format and input cases, remove the temporary `NotImplementedException` expectation, add real valid/invalid GGUF routing, and prove requested cancellation becomes `Cancelled`.

## Packaged execution

Build success alone is not test evidence. Debug and Release x64 verification will:

1. restore the test project for `win-x64`;
2. build the application and packaged test project;
3. confirm the generated `.build.appxrecipe` and deployed fixture tree;
4. locate the latest compatible `vstest.console.exe` with `vswhere`;
5. run direct scanner, router, and full-suite filters through the appxrecipe with `/Platform:x64`;
6. write and inspect TRX files;
7. report exact pass/fail/skip counts and durations.

The test project, not the production app, owns the `GraniteEdgeAI.UnitTests (Package)` launch profile. The production app launch settings will be restored to its packaged and unpackaged application profiles.

CI sparse checkout must include `tests/TestFixtures`; otherwise the wildcard content items reference files that never reach the runner. Runner discovery will select the latest installation so a stale incompatible Visual Studio test platform is not chosen first.

## Review criteria

Before completion, reviews will explicitly check:

- every unchecked `ulong` conversion, multiplication, allocation, seek, and attacker-controlled loop;
- little-endian decoding and exact-read offsets;
- all metadata types, strict booleans, nested arrays, truncation, and ordering;
- cancellation preservation and the absence of broad catches;
- bounded candidate/array state and no duplicate parser in tests;
- fixture deployment paths, generated-recipe use, and launch-profile ownership;
- removal of every temporary `NotImplementedException`;
- documentation accuracy and actual test evidence;
- unrelated working-tree changes.

## Sources and design basis

- The official [GGUF specification](https://github.com/ggml-org/ggml/blob/master/docs/gguf.md) defines the header, strings, value types, nested arrays, key length, boolean validity, metadata fields, and `file_type` values.
- The current official [llama.cpp `llama_ftype` enum](https://github.com/ggml-org/llama.cpp/blob/master/include/llama.h) supplies the broader current quantization labels.
- Microsoft’s `Stream.ReadExactlyAsync` documentation establishes exact-read, `EndOfStreamException`, and cancellation behavior.
- Microsoft’s `BinaryPrimitives`, `UTF8Encoding`, `FileStreamOptions`, MSTest, WinUI testing, and single-project MSIX documentation guide explicit endian conversion, strict decoding, asynchronous sequential I/O, test attributes, packaged hosting, and generated packaging artifacts.
- *Code Complete*, relevant sections in PDF pages 315–322, 353–378, 388–391, 533–535, 559–560, and 669, guides cohesive routines, trust-boundary validation, consistent diagnostics, checked arithmetic, and endpoint tests.
- *Designing Secure Software*, PDF pages 96, 177, 180–181, 189, 192, 197–216, 245–252, and 257, guides fail-closed parsing, untrusted length validation, resource limits, strict encoding, threshold tests, and local exception translation.
- *The Art of Unit Testing*, *Refactoring*, *Why Programs Fail*, and the local `windows-apps.pdf` provide the test, incremental-refactoring, debugging, and WinUI project principles recorded in the implementation plan and beginner guide.
