# GGUF quick scanner: a beginner's guide

This guide explains the GGUF quick scanner as it exists at local implementation
commit `c67d33f36ec323d8537245a536bb818098dd6a7e`. The commands and test totals in
this guide were observed on 25 July 2026. They are a Task 9 documentation
snapshot, including explicit standalone application builds. Task 10 will repeat
the application/test matrix after independent reviews for final verification.

## 1. Purpose, non-goals, and current workflow boundary

The quick scanner inspects a selected `.gguf` file before the application tries
to use it as a model. A model file is untrusted binary input, so the scanner
checks the container structure before returning display metadata.

On success, `ModelQuickScanResult` can contain:

- model name;
- architecture;
- parameter-size label;
- quantization label;
- file size;
- context length; and
- GGUF version.

The scanner deliberately does **not**:

- load or run the model;
- read tensor descriptors or tensor data;
- read the complete file into memory;
- create a general-purpose in-memory GGUF object model;
- modify, repair, or rewrite the selected file;
- scan OpenVINO models; or
- decide how the UI should display a result.

### Designed responsibilities

The approved responsibility flow is:

```text
ModelImportPage
    -> ModelQuickScanner
        -> GgufQuickScanner
            -> ModelQuickScanResult
```

This is a responsibility diagram, not a claim that the entire UI flow is wired.
The current `ModelImportPage` selects a path and changes the import card to
`Scanning`, but it does not call `ModelQuickScanner`. It also does not set
`HasValidatedModel`. Page-to-router invocation, result presentation, and
validated-state wiring are outside this scanner-only task.

The implemented and packaged-tested path begins at `ModelQuickScanner` or
`GgufQuickScanner`:

```text
packaged test or future caller
    -> ModelQuickScanner.ScanAsync
        -> GgufQuickScanner.ScanAsync
            -> ModelQuickScanResult
```

### Class responsibilities

| Class | Responsibility |
|---|---|
| `ModelImportPage` | Selects the format and file, and owns page state. It must not parse GGUF bytes. Its call to the quick-scan router is not yet wired. |
| `ModelQuickScanner` | Routes GGUF requests to `GgufQuickScanner`, returns controlled results for `None`, OpenVINO, and unknown formats, and converts only requested cancellation into `Cancelled`. |
| `GgufQuickScanner` | Opens the file read-only, validates the fixed header and metadata, extracts bounded display fields, and returns stable structural or opening failures. |
| `ModelQuickScanResult` | Carries one completed `Success`, `Failure`, or `Cancelled` outcome. It validates factory inputs but does not parse bytes. Keeping it as a data carrier separates parsing rules from result rules. |
| `ModelQuickScanOutcome` | Names the three possible outcomes. |

## 2. Binary basics and the 24-byte header

A binary file is a sequence of bytes. A stream has a `Position`: reading four
bytes advances that position by four. The scanner starts at byte offset zero
and moves forwards through the header and metadata.

The supported GGUF v3 fixed header is exactly 24 bytes:

| Offset | Bytes | Field | C# value | Meaning |
|---:|---:|---|---|---|
| 0 | 4 | magic | `byte[]` | Must be `47 47 55 46`, the ASCII bytes for `GGUF`. |
| 4 | 4 | version | `uint` | Must be version `3`. |
| 8 | 8 | tensor count | `ulong` | Number of tensor descriptors. It is read, but this quick scanner does not visit them. |
| 16 | 8 | metadata entry count | `ulong` | Number of key/value entries that immediately follow. |

### What the C# binary types mean

| Term | Beginner meaning |
|---|---|
| `byte` | One unsigned 8-bit value, from 0 through 255. |
| `byte[]` | An allocated array of bytes. The scanner uses small fixed arrays and bounded text arrays. |
| `uint` | An unsigned 32-bit integer. It occupies four bytes. |
| `ulong` | An unsigned 64-bit integer. It occupies eight bytes and can represent file-declared counts larger than `int`. |
| `Memory<byte>` | A safe view over writable bytes that can be passed to an asynchronous method. |
| `Span<byte>` | A short-lived view over bytes used for efficient local comparison or decoding without copying. |

### Little-endian decoding

Little-endian means the least significant byte is stored first. In the
scanner's supported little-endian form, version 3 is stored as:

```text
03 00 00 00
```

Reading those bytes as a big-endian integer would give the wrong number.
`BinaryPrimitives.ReadUInt32LittleEndian` explicitly turns them into `3`.
The scanner uses the corresponding 64-bit method for tensor and metadata
counts. Explicit decoding documents the file format and does not depend on the
computer's native byte order.

## 3. Opening, exact reads, and cancellation

`ScanAsync` creates a `FileStream` with these options:

| Option | Value | Why |
|---|---|---|
| `Mode` | `FileMode.Open` | The selected file must already exist; the scanner never creates it. |
| `Access` | `FileAccess.Read` | The scanner cannot write through this stream. |
| `Share` | `FileShare.Read` | Another reader may inspect the same model at the same time. |
| `Options` | `FileOptions.Asynchronous \| FileOptions.SequentialScan` | The code awaits reads and moves through the relevant bytes in order. |

`await using` disposes the stream even when scanning returns a failure or an
exception leaves the parsing block.

### Why `ReadExactlyAsync` is used

A normal stream read is allowed to return fewer bytes than requested. That is
not enough for a fixed-width binary field. `ReadExactlyAsync` keeps reading
until the supplied buffer is full, advances the stream position, observes the
token, and throws `EndOfStreamException` if the file ends first.

Header helpers translate that exact exception into `truncated-header`.
Metadata helpers translate it into `truncated-metadata`. They keep the field,
stage, starting offset, expected byte count, and available/read byte count in
the technical message.

### How cancellation travels

```text
caller token
  -> ModelQuickScanner.ScanAsync
      -> GgufQuickScanner.ScanAsync
          -> header, key, value, string, and array helpers
```

`GgufQuickScanner` checks a pre-cancelled token before opening the file. Every
asynchronous read receives the same token. Metadata-entry and semantic-array
loops also call `ThrowIfCancellationRequested`.

`GgufQuickScanner` does not catch `OperationCanceledException`.
`ModelQuickScanner` catches it only when
`cancellationToken.IsCancellationRequested` is true. The router then returns a
normal `Cancelled` result. This narrow filter avoids disguising an unrelated
programming error as user cancellation.

## 4. GGUF metadata encoding

Each metadata entry contains, in order:

1. a GGUF string containing the key;
2. a little-endian `uint32` value-type ID; and
3. a payload encoded according to that type.

All official metadata type IDs are:

| ID | Scanner enum | Payload |
|---:|---|---|
| 0 | `UInt8` | 1-byte unsigned integer |
| 1 | `Int8` | 1-byte signed integer |
| 2 | `UInt16` | 2-byte unsigned integer |
| 3 | `Int16` | 2-byte signed integer |
| 4 | `UInt32` | 4-byte unsigned integer |
| 5 | `Int32` | 4-byte signed integer |
| 6 | `Float32` | 4-byte IEEE 754 number |
| 7 | `Boolean` | 1 byte; only `0` and `1` are valid |
| 8 | `String` | `uint64` byte length, then UTF-8 bytes |
| 9 | `Array` | element type, element count, then elements |
| 10 | `UInt64` | 8-byte unsigned integer |
| 11 | `Int64` | 8-byte signed integer |
| 12 | `Float64` | 8-byte IEEE 754 number |

A raw type outside 0 through 12 is structurally unsupported. It cannot be
safely skipped because the scanner cannot know its width.

### GGUF strings and keys

A GGUF string is:

```text
uint64 byte length
exactly that many UTF-8 bytes
no null terminator
```

Length counts bytes, not C# characters. Retained values such as
`general.name` and `general.architecture` use strict UTF-8 decoding. Invalid
bytes fail instead of becoming replacement characters.

Metadata keys have additional GGUF rules:

- at most 65,535 bytes;
- ASCII only;
- one or more nonempty `lower_snake_case` segments;
- segments separated by one dot.

The implemented grammar is `[a-z0-9_]+(\.[a-z0-9_]+)*`. It rejects an empty
key, leading/trailing/adjacent dots, spaces, uppercase letters, controls, and
non-ASCII text.

### Arrays and nested arrays

An array payload begins with:

```text
uint32 element type
uint64 element count
encoded element 0
encoded element 1
...
```

The count is the number of elements, not the number of bytes. GGUF permits the
element type itself to be `Array`, so nested arrays are legal. The scanner uses
bounded recursion. Fixed-width numeric arrays can be skipped as one validated
byte range. Booleans, strings, and nested arrays must be consumed
semantically, because their validity or width cannot be inferred from one
fixed multiplication.

## 5. One-pass extraction, skipping, ordering, and duplicates

The scanner makes one sequential pass through the declared metadata. It keeps
only the fields needed by `ModelQuickScanResult`.

Known keys are handled as follows:

| Key | Required type | Result rule |
|---|---|---|
| `general.name` | string | Missing or blank uses the selected filename. |
| `general.architecture` | string | Required and nonblank. |
| `general.size_label` | string | Missing or blank becomes `null`. |
| `general.file_type` | `uint32` | Missing becomes `null`; present value is mapped to a label. |
| `<architecture>.context_length` | `uint32` or `uint64` | Normalized to `ulong`; missing becomes `null`. |

### Skipping unknown metadata

Unknown **keys** are forward-compatible when their declared type is official:

- fixed numeric values are skipped only after the remaining byte range is
  checked;
- booleans are read and checked for `0` or `1`;
- string length is checked against the 16 MiB policy and remaining bytes, then
  the bytes are skipped without allocation or decoding;
- arrays use the same count, total, depth, boolean, string, and nested-array
  validation as known data.

Unknown **type IDs** cannot be skipped and return
`unsupported-metadata-type`.

### Context before architecture

The desired context key depends on the architecture, but metadata order is not
guaranteed. Before `general.architecture` is known, a key ending in
`.context_length` is a possible candidate. The scanner:

1. retains at most 64 distinct exact candidate keys;
2. stores only the declared type and optional normalized integer;
3. preserves the first occurrence of each candidate;
4. resolves the exact `<architecture>.context_length` candidate when the
   architecture is read; and
5. handles a later exact context key immediately.

The exact comparison checks length, suffix, and ordinal prefix without
allocating `architecture + ".context_length"` for each entry.

### Duplicate policy

Scanner-relevant metadata uses bounded first-occurrence-wins behavior.
Boolean flags cover the four `general.*` fields and the resolved context.
Later duplicates are still completely consumed and structurally validated,
but they cannot replace the retained value.

The scanner does **not** retain or reject a global set of arbitrary duplicate
keys. Only pre-architecture context candidates use a dictionary, and that
dictionary is capped at 64 distinct keys.

## 6. Quantization mapping

`general.file_type` is a numeric file-type classification. The method
`MapFileTypeToQuantization` converts its `uint32` value into display text.
The current map is 0 through 41:

| ID | Label | Status in the upstream enum |
|---:|---|---|
| 0 | `F32` | current |
| 1 | `F16` | current |
| 2 | `Q4_0` | current |
| 3 | `Q4_1` | current |
| 4 | `Q4_1_SOME_F16` | historical compatibility slot |
| 5 | `Q4_2` | historical compatibility slot |
| 6 | `Q4_3` | historical compatibility slot |
| 7 | `Q8_0` | current |
| 8 | `Q5_0` | current |
| 9 | `Q5_1` | current |
| 10 | `Q2_K` | current |
| 11 | `Q3_K_S` | current |
| 12 | `Q3_K_M` | current |
| 13 | `Q3_K_L` | current |
| 14 | `Q4_K_S` | current |
| 15 | `Q4_K_M` | current |
| 16 | `Q5_K_S` | current |
| 17 | `Q5_K_M` | current |
| 18 | `Q6_K` | current |
| 19 | `IQ2_XXS` | current |
| 20 | `IQ2_XS` | current |
| 21 | `Q2_K_S` | current |
| 22 | `IQ3_XS` | current |
| 23 | `IQ3_XXS` | current |
| 24 | `IQ1_S` | current |
| 25 | `IQ4_NL` | current |
| 26 | `IQ3_S` | current |
| 27 | `IQ3_M` | current |
| 28 | `IQ2_S` | current |
| 29 | `IQ2_M` | current |
| 30 | `IQ4_XS` | current |
| 31 | `IQ1_M` | current |
| 32 | `BF16` | current |
| 33 | `Q4_0_4_4` | historical compatibility slot |
| 34 | `Q4_0_4_8` | historical compatibility slot |
| 35 | `Q4_0_8_8` | historical compatibility slot |
| 36 | `TQ1_0` | current |
| 37 | `TQ2_0` | current |
| 38 | `MXFP4_MOE` | current |
| 39 | `NVFP4` | current |
| 40 | `Q1_0` | current |
| 41 | `Q2_0` | current |

An unassigned number is not a malformed GGUF structure, so it becomes
`Unknown (file type N)`. A missing key becomes `null`. A present key with a
type other than `UInt32` returns `invalid-file-type`.

Coverage is representative, not an exhaustive test for every numeric value:
V-001 checks 15, V-009 checks 40, V-011 checks 41, and V-010 checks unknown
value 999. The mapping table is isolated so upstream enum changes can be
reviewed and updated in one method.

## 7. Safety limits

These limits are trust-boundary policy. They are deliberately checked before
narrowing, allocation, recursion, or an attacker-controlled loop.

| Limit | Value | Source/kind | Why |
|---|---:|---|---|
| metadata entries | 1,000,000 | project top-level loop guard | Prevents an unbounded metadata loop. This is not a GGUF format maximum. |
| metadata key bytes | 65,535 | GGUF specification | Enforces the format's `2^16 - 1` key limit before allocation. |
| one metadata string | 16 MiB | application safety limit | Bounds retained allocation and unknown-string work. |
| one array | 1,000,000 elements | application safety limit | Allows large tokenizer arrays but bounds one loop. |
| all arrays in one scan | 4,000,000 declared elements | application safety limit | Stops many individually valid arrays or nested declarations from multiplying total work. |
| array nesting | 8 levels | application safety limit | Bounds recursive stack depth while leaving compatibility headroom. |
| pending context candidates | 64 distinct keys | application safety limit | Supports unusual ordering without retaining up to one million strings. |

Additional arithmetic and range rules matter:

- a `ulong` is compared with its limit before converting to `int` or `long`;
- remaining bytes are calculated as `stream.Length - stream.Position`;
- the code compares requested bytes with the remaining count instead of using
  unchecked `position + length`;
- fixed-width array multiplication is `checked`;
- aggregate array addition is guarded by subtraction before adding; and
- `SkipValidatedBytes` never seeks beyond end of file.

## 8. Outcomes and every failure code

A structural or expected opening failure returns `Outcome == Failure`, a stable
kebab-case code, a short user message, and a technical message. Successful
metadata is not returned with a failure.

### GGUF scanner codes

| Code | Trigger | Direct evidence |
|---|---|---|
| `truncated-header` | Any fixed-header field ends before offset 24. | I-000, I-003, I-024 |
| `invalid-magic` | A complete header does not begin with `GGUF`. | I-001 |
| `unsupported-version` | Complete header version is not 3. | I-002 |
| `excessive-metadata-count` | More than 1,000,000 entries are declared. | I-007 |
| `metadata-key-too-long` | A key declares more than 65,535 bytes. | I-005 |
| `invalid-metadata-key` | Key encoding/grammar is not valid hierarchical lower snake case. | I-019, I-025 through I-028 |
| `unsupported-metadata-type` | Value or array element type is outside 0 through 12. | I-006 |
| `truncated-metadata` | A key, type, scalar, string, or array payload ends early. | I-004, I-011 |
| `missing-required-architecture` | Architecture is absent, empty, or whitespace. | H-001, I-008, I-023 |
| `invalid-architecture-type` | `general.architecture` is not a string. | I-009 |
| `invalid-name-type` | `general.name` is present but not a string. | I-020 |
| `invalid-size-label-type` | `general.size_label` is present but not a string. | I-021 |
| `invalid-file-type` | `general.file_type` is present but not `UInt32`. | I-022 |
| `invalid-context-type` | The exact architecture context key is neither `UInt32` nor `UInt64`. | I-016 |
| `invalid-boolean-value` | A Boolean payload is not byte 0 or 1. | I-010 |
| `invalid-metadata-encoding` | A retained key or string is not valid strict UTF-8. | I-018 |
| `metadata-string-too-long` | A string exceeds 16 MiB. | I-012 |
| `excessive-array-count` | One array exceeds 1,000,000 or aggregate declarations exceed 4,000,000. | I-013, I-014 |
| `excessive-array-depth` | Nested arrays reach level 9. | I-015 |
| `excessive-context-candidate-count` | A 65th distinct pre-architecture candidate would be retained. | I-017 |
| `file-not-found` | Opening finds no file or directory. | Unique missing path test |
| `file-access-denied` | `FileStream` construction throws `UnauthorizedAccessException`. | Narrow production mapping; no portable forced packaged case |
| `file-read-error` | Another opening-time `IOException` occurs. | Narrow production mapping; no portable forced packaged case |

An invalid API path is a caller-contract exception, not a failure code.
Direct `GgufQuickScanner` cancellation also remains an exception.

### Router-only codes

| Code | Meaning |
|---|---|
| `openvino-scan-not-implemented` | OpenVINO was selected, but that scanner is outside this task. |
| `unsupported-model-format` | The enum value is unknown or future data the router does not support. |

## 9. Generated fixtures

Binary fixtures are never edited by hand. The only sources are:

- `tests/TestFixtures/Generate-GgufHeaderFixtures.ps1`; and
- `tests/TestFixtures/Generate-GgufMetadataFixtures.ps1`.

The header generator owns H-001, I-000 through I-003, and I-024. The metadata
generator owns V-001 through V-012 and the other malformed metadata fixtures.
The metadata generator also writes the 12 expectation JSON records and
`fixture-manifest.json`.

The scripts:

- write typed little-endian fields with `BinaryWriter`;
- create no tensor data;
- use tiny declared-limit fixtures so unsafe payloads are rejected before a
  large allocation;
- make complete metadata fixtures 32-byte aligned;
- define expected values independently instead of asking the production
  scanner what to expect; and
- record byte lengths and SHA-256 hashes for deterministic integrity checks.

To change a fixture safely, change its script, run both scripts, review every
generated difference, parse all JSON, and verify the manifest hashes and
lengths. Do not hex-edit a `.gguf` file.

### Complete binary fixture inventory

There are 42 generated binaries: 13 under `GGUF` and 29 under `Malformed`.
V-001 through V-012 each have a same-stem JSON expectation.

| ID | File | Bytes | Behavior/result |
|---|---|---:|---|
| H-001 | `GGUF/H-001-valid-v3-header.gguf` | 24 | Complete v3 header; no architecture, so controlled failure |
| V-001 | `GGUF/V-001-complete-metadata-v3.gguf` | 320 | All display fields; file type 15 is `Q4_K_M` |
| V-002 | `GGUF/V-002-missing-name.gguf` | 288 | Filename fallback |
| V-003 | `GGUF/V-003-missing-context.gguf` | 288 | Successful `null` context |
| V-004 | `GGUF/V-004-missing-size-label.gguf` | 288 | Successful `null` size label |
| V-005 | `GGUF/V-005-unknown-metadata.gguf` | 544 | Unknown string, Boolean, numeric array, and string array are consumed |
| V-006 | `GGUF/V-006-unusual-metadata-order.gguf` | 320 | Context occurs before architecture |
| V-007 | `GGUF/V-007-context-uint32.gguf` | 320 | `UInt32` context is normalized to `ulong` |
| V-008 | `GGUF/V-008-missing-file-type.gguf` | 288 | Successful `null` quantization |
| V-009 | `GGUF/V-009-all-official-metadata-types.gguf` | 800 | Types 0 through 12, nested array, and file type 40 (`Q1_0`) |
| V-010 | `GGUF/V-010-unknown-file-type.gguf` | 320 | File type 999 displays as `Unknown (file type 999)` |
| V-011 | `GGUF/V-011-current-q2_0-file-type.gguf` | 320 | Current file type 41 displays as `Q2_0` |
| V-012 | `GGUF/V-012-duplicate-relevant-metadata.gguf` | 544 | Bounded first-occurrence-wins behavior |
| I-000 | `Malformed/I-000-empty-file.gguf` | 0 | `truncated-header` at magic |
| I-001 | `Malformed/I-001-invalid-magic.gguf` | 24 | `invalid-magic` |
| I-002 | `Malformed/I-002-unsupported-version.gguf` | 24 | `unsupported-version` |
| I-003 | `Malformed/I-003-truncated-header.gguf` | 12 | `truncated-header` in tensor count |
| I-004 | `Malformed/I-004-truncated-metadata-value.gguf` | 59 | `truncated-metadata` for a declared 20-byte string with three bytes |
| I-005 | `Malformed/I-005-oversized-key-length.gguf` | 32 | `metadata-key-too-long` at 65,536 |
| I-006 | `Malformed/I-006-unknown-value-type.gguf` | 51 | `unsupported-metadata-type` for 99 |
| I-007 | `Malformed/I-007-excessive-metadata-count.gguf` | 24 | `excessive-metadata-count` at 1,000,001 |
| I-008 | `Malformed/I-008-missing-required-architecture.gguf` | 224 | `missing-required-architecture` |
| I-009 | `Malformed/I-009-wrong-architecture-type.gguf` | 64 | `invalid-architecture-type` |
| I-010 | `Malformed/I-010-invalid-boolean-value.gguf` | 52 | `invalid-boolean-value` for byte 2 |
| I-011 | `Malformed/I-011-truncated-array.gguf` | 66 | `truncated-metadata` for a short array payload |
| I-012 | `Malformed/I-012-oversized-metadata-string.gguf` | 68 | `metadata-string-too-long` at 16 MiB + 1 |
| I-013 | `Malformed/I-013-excessive-array-count.gguf` | 71 | `excessive-array-count` at 1,000,001 |
| I-014 | `Malformed/I-014-excessive-total-array-count.gguf` | 119 | `excessive-array-count` when nested declarations exceed 4,000,000 total |
| I-015 | `Malformed/I-015-excessive-array-depth.gguf` | 160 | `excessive-array-depth` at level 9 |
| I-016 | `Malformed/I-016-invalid-context-type.gguf` | 128 | `invalid-context-type` for the exact candidate |
| I-017 | `Malformed/I-017-excessive-context-candidates.gguf` | 2,816 | `excessive-context-candidate-count` at candidate 65 |
| I-018 | `Malformed/I-018-invalid-utf8-architecture.gguf` | 66 | `invalid-metadata-encoding` |
| I-019 | `Malformed/I-019-non-ascii-key.gguf` | 64 | `invalid-metadata-key` for non-ASCII |
| I-020 | `Malformed/I-020-wrong-name-type.gguf` | 128 | `invalid-name-type` |
| I-021 | `Malformed/I-021-wrong-size-label-type.gguf` | 128 | `invalid-size-label-type` |
| I-022 | `Malformed/I-022-wrong-file-type.gguf` | 128 | `invalid-file-type` |
| I-023 | `Malformed/I-023-blank-architecture.gguf` | 96 | `missing-required-architecture` for whitespace |
| I-024 | `Malformed/I-024-short-invalid-magic.gguf` | 12 | `truncated-header` takes precedence over bad magic |
| I-025 | `Malformed/I-025-empty-metadata-key.gguf` | 37 | `invalid-metadata-key` for an empty key |
| I-026 | `Malformed/I-026-empty-key-segment.gguf` | 50 | `invalid-metadata-key` for `general..name` |
| I-027 | `Malformed/I-027-key-with-space.gguf` | 49 | `invalid-metadata-key` for U+0020 |
| I-028 | `Malformed/I-028-uppercase-key.gguf` | 49 | `invalid-metadata-key` for uppercase `G` |

## 10. Complete quick-scan test inventory

The `Unit` category is a repository label, not a claim that every case is a
pure in-memory unit test. Fixture-backed scanner and router cases deploy real
files, open them with the real `FileStream`, and cross the package/content
boundary. They are component/integration-style tests hosted in the packaged
MSTest project.

### `GgufQuickScannerTests`: 47 methods and 47 executions

| Test name | Arrange | Act | Assert | Failure/regression caught |
|---|---|---|---|---|
| `ScanAsync_NullPath_ThrowsArgumentException` | Real scanner; null path | Call `ScanAsync` | `ArgumentException` | Null API input reaching I/O |
| `ScanAsync_EmptyPath_ThrowsArgumentException` | Real scanner; empty path | Call scanner | `ArgumentException` | Empty API input |
| `ScanAsync_WhitespacePath_ThrowsArgumentException` | Real scanner; spaces | Call scanner | `ArgumentException` | Whitespace API input |
| `ScanAsync_PreCancelledToken_ThrowsOperationCanceledException` | Cancel token first | Call harmless path | cancellation exception | File opening after cancellation |
| `ScanAsync_EmptyFile_ReturnsTruncatedHeaderFailure` | Deploy I-000 | Scan file | failure, `truncated-header`, magic/offset detail | Escaped EOF |
| `ScanAsync_ShortInvalidMagic_ReturnsTruncatedHeaderFailure` | Deploy I-024 | Scan short `TEST` header | tensor-count truncation | Classifying bad magic before full header |
| `ScanAsync_TruncatedHeader_ReturnsTruncatedHeaderFailure` | Deploy I-003 | Scan partial tensor count | exact field/offset failure | Inaccurate short-header diagnostics |
| `ScanAsync_ValidHeaderWithoutArchitecture_ReturnsMissingArchitectureFailure` | Deploy H-001 | Scan zero metadata | missing architecture | Temporary unfinished header path |
| `ScanAsync_InvalidMagic_ReturnsInvalidMagicFailure` | Deploy I-001 | Scan complete bad signature | expected/actual bytes and `invalid-magic` | Accepting non-GGUF data |
| `ScanAsync_UnsupportedVersion_ReturnsUnsupportedVersionFailure` | Deploy I-002 | Scan version 99 | `unsupported-version` | Parsing an unknown layout |
| `ScanAsync_ExcessiveMetadataCount_ReturnsExcessiveMetadataCountFailure` | Deploy I-007 | Scan count 1,000,001 | actual/limit code | Attacker-controlled top loop |
| `ScanAsync_OversizedKeyLength_ReturnsMetadataKeyTooLongFailure` | Deploy I-005 | Scan length 65,536 | code, values, offset | Allocation from oversized key |
| `ScanAsync_UnknownMetadataType_ReturnsUnsupportedMetadataTypeFailure` | Deploy I-006 | Scan type 99 | key/type/range diagnostics | Unsafe unknown-width skip |
| `ScanAsync_TruncatedMetadataValue_ReturnsTruncatedMetadataFailure` | Deploy I-004 | Scan short string | key/stage/offset/3-versus-20 | Escaped or misclassified EOF |
| `ScanAsync_InvalidBooleanValue_ReturnsInvalidBooleanFailure` | Deploy I-010 | Scan Boolean 2 | `invalid-boolean-value` | Treating any nonzero byte as true |
| `ScanAsync_TruncatedArray_ReturnsTruncatedMetadataFailure` | Deploy I-011 | Scan 1 of 3 `UInt32`s | 4 available versus 12 required | Seeking past EOF |
| `ScanAsync_EmptyMetadataKey_ReturnsInvalidMetadataKeyFailure` | Deploy I-025 | Scan empty key | safe entry/offset/rule detail | Accepting empty key |
| `ScanAsync_EmptyMetadataKeySegment_ReturnsInvalidMetadataKeyFailure` | Deploy I-026 | Scan adjacent dots | empty segment at index 8 | Partial grammar validation |
| `ScanAsync_MetadataKeyWithSpace_ReturnsInvalidMetadataKeyFailure` | Deploy I-027 | Scan key with space | U+0020/index without raw-key echo | ASCII-only validation |
| `ScanAsync_UppercaseMetadataKey_ReturnsInvalidMetadataKeyFailure` | Deploy I-028 | Scan uppercase key | U+0047/index | Accepting non-lowercase key |
| `ScanAsync_MissingArchitecture_ReturnsMissingArchitectureFailure` | Deploy I-008 | Scan other metadata | required-field failure | Success without architecture |
| `ScanAsync_ArchitectureWithWrongType_ReturnsInvalidArchitectureTypeFailure` | Deploy I-009 | Scan `UInt32` architecture | actual/expected types | Reporting wrong type as merely missing |
| `ScanAsync_OversizedMetadataString_ReturnsMetadataStringTooLongFailure` | Deploy I-012 | Scan 16 MiB + 1 declaration | `metadata-string-too-long` | Large allocation/read |
| `ScanAsync_ExcessiveArrayCount_ReturnsExcessiveArrayCountFailure` | Deploy I-013 | Scan 1,000,001 count | `excessive-array-count` | Oversized element loop |
| `ScanAsync_ExcessiveTotalArrayCount_ReturnsExcessiveArrayCountFailure` | Deploy I-014 | Scan nested declarations | aggregate-limit failure | Individually valid arrays multiplying work |
| `ScanAsync_ExcessiveArrayDepth_ReturnsExcessiveArrayDepthFailure` | Deploy I-015 | Scan level 9 | `excessive-array-depth` | Unbounded recursion |
| `ScanAsync_ContextWithWrongType_ReturnsInvalidContextTypeFailure` | Deploy I-016 | Resolve exact string candidate | `invalid-context-type` | Silently ignoring exact wrong type |
| `ScanAsync_ExcessiveContextCandidates_ReturnsControlledFailure` | Deploy I-017 | Scan 65 distinct candidates | candidate-limit failure | Unbounded retained dictionary |
| `ScanAsync_InvalidUtf8Architecture_ReturnsInvalidEncodingFailure` | Deploy I-018 | Decode retained architecture | encoding failure | Replacement-character decoding |
| `ScanAsync_NonAsciiKey_ReturnsInvalidMetadataKeyFailure` | Deploy I-019 | Decode/validate key | key failure | Host/source encoding loophole |
| `ScanAsync_NameWithWrongType_ReturnsInvalidNameTypeFailure` | Deploy I-020 | Scan numeric name | `invalid-name-type` | Silently skipping a known bad field |
| `ScanAsync_SizeLabelWithWrongType_ReturnsInvalidSizeLabelTypeFailure` | Deploy I-021 | Scan numeric label | `invalid-size-label-type` | Same for size label |
| `ScanAsync_FileTypeWithWrongType_ReturnsInvalidFileTypeFailure` | Deploy I-022 | Scan string file type | `invalid-file-type` | Same for quantization input |
| `ScanAsync_BlankArchitecture_ReturnsMissingArchitectureFailure` | Deploy I-023 | Scan spaces | missing architecture | Passing blank into success factory |
| `ScanAsync_AllOfficialMetadataTypes_ReturnsExpectedSuccessResult` | Deploy V-009 + JSON | Scan real file | every typed field; success | Missing scalar/nested-array path; file type 40 |
| `ScanAsync_UnknownFileType_ReturnsDocumentedLabel` | Deploy V-010 + JSON | Scan 999 | numeric fallback label | Throwing or losing unknown enum value |
| `ScanAsync_MissingFile_ReturnsFileNotFoundFailure` | Unique absent path | Scan it | controlled `file-not-found` | Escaped `FileNotFoundException` |
| `ScanAsync_MissingName_UsesFileNameFallback` | Deploy V-002 + JSON | Scan | filename is model name | Treating name as required |
| `ScanAsync_MissingContext_ReturnsSuccessWithNullContext` | Deploy V-003 + JSON | Scan | successful null context | Treating context as required |
| `ScanAsync_MissingSizeLabel_ReturnsSuccessWithNullSizeLabel` | Deploy V-004 + JSON | Scan | successful null label | Treating size as required |
| `ScanAsync_UnknownMetadata_SkipsValuesAndReturnsExpectedResult` | Deploy V-005 + JSON | Scan | all known fields survive | Unknown values disrupting extraction |
| `ScanAsync_UnusualMetadataOrder_ReturnsExpectedResult` | Deploy V-006 + JSON | Scan | context 131,072 | Order-dependent context loss |
| `ScanAsync_ContextEncodedAsUInt32_NormalisesToUInt64` | Deploy V-007 + JSON | Scan | `ulong` 131,072 | Accepting only `UInt64` |
| `ScanAsync_MissingFileType_ReturnsSuccessWithNullQuantization` | Deploy V-008 + JSON | Scan | successful null quantization | Treating file type as required |
| `ScanAsync_CurrentQ2_0FileType_ReturnsExpectedResult` | Deploy V-011 + JSON | Scan file type 41 | `Q2_0` | Stale upstream mapping |
| `ScanAsync_DuplicateRelevantMetadata_RetainsFirstOccurrences` | Deploy V-012 + JSON | Scan duplicates | coherent first values | Last-wins architecture/context mismatch |
| `ScanAsync_CompleteMetadata_ReturnsExpectedSuccessResult` | Deploy V-001 + JSON | Scan | every field and null failure data | Incomplete success extraction |

### `ModelQuickScannerTests`: 10 methods and 11 executions

| Test name | Arrange | Act | Assert | Failure/regression caught |
|---|---|---|---|---|
| `ScanAsync_None_ReturnsCancelledResult` | Router; `None` | Route harmless path | `Cancelled` | Treating dialog cancellation as failure |
| `ScanAsync_None_WithEmptyPath_ReturnsCancelledResult` | `None`; empty unused path | Route | `Cancelled` | Validating an unused path first |
| `ScanAsync_OpenVino_ReturnsNotImplementedFailure` | OpenVINO selection | Route | stable OpenVINO failure | Accidental GGUF routing |
| `ScanAsync_UnknownFormat_ReturnsUnsupportedFormatFailure` | Enum 999 | Route | stable unsupported-format detail | Uncontrolled future/corrupt enum |
| `ScanAsync_GgufWithNullPath_ThrowsArgumentNullException` | GGUF; null | Route | exact null exception | Missing router precondition |
| `ScanAsync_GgufWithEmptyOrWhitespacePath_ThrowsArgumentException` | Two rows: empty and spaces | Route each | exact argument exception | Blank path accepted; two executions |
| `ScanAsync_GgufWithValidFixture_ReturnsSuccessResult` | Deploy V-001 | Route through real scanner | complete Granite result | Router bypass/duplicate parser |
| `ScanAsync_GgufWithInvalidFixture_ReturnsScannerFailure` | Deploy I-001 | Route | `invalid-magic` preserved | Router swallowing diagnostics |
| `ScanAsync_GgufWithPreCancelledToken_ReturnsCancelledResult` | Cancel token | Route GGUF | `Cancelled` result | Cancellation escaping router |
| `Constructor_NullGgufQuickScanner_ThrowsArgumentNullException` | Null dependency | Construct router | exact null exception | Invalid router state |

### `ModelQuickScanResultTests`: 17 methods and 27 executions

| Test name | Arrange | Act | Assert | Failure/regression caught |
|---|---|---|---|---|
| `CreateCancelled_SetsCancelledOutcome` | None | Create cancelled | outcome | Wrong cancellation state |
| `CreateCancelled_LeavesAllOtherPropertiesEmpty` | None | Create cancelled | all data null | Misleading partial data |
| `CreateSuccess_StoresOutcomeAndMetadata` | Complete sample | Create success | every value retained | Data-carrier loss |
| `CreateSuccess_LeavesFailureDetailsEmpty` | Valid success values | Create success | all failure fields null | Mixed success/failure state |
| `CreateFailure_StoresOutcomeAndFailureDetails` | Three messages | Create failure | all retained | Diagnostic loss |
| `CreateFailure_LeavesMetadataEmpty` | Failure values | Create failure | all metadata null | Mixed failure/success state |
| `CreateFailure_BlankUserMessage_UsesDefaultMessage` | Null, empty, spaces | Create three failures | safe default | Blank UI message |
| `CreateFailure_BlankTechnicalMessage_UsesDefaultMessage` | Null, empty, spaces | Create three failures | technical default | Blank diagnostic |
| `CreateFailure_BlankFailureCode_ThrowsArgumentException` | Null, empty, spaces | Create three failures | argument exception | Unstable blank code |
| `CreateSuccess_BlankModelName_ThrowsArgumentException` | Null, empty, spaces | Create three successes | argument exception | Unusable successful name |
| `CreateSuccess_BlankArchitecture_ThrowsArgumentException` | Null, empty, spaces | Create three successes | argument exception | Unusable successful architecture |
| `CreateSuccess_ZeroFileSizeBytes_ThrowsArgumentOutOfRangeException` | File size 0 | Create success | range exception | Empty successful model |
| `CreateSuccess_NegativeFileSizeBytes_ThrowsArgumentOutOfRangeException` | File size -1 | Create success | range exception | Invalid negative size |
| `CreateSuccess_ZeroGgufVersion_ThrowsArgumentOutOfRangeException` | Version 0 | Create success | range exception | Missing version |
| `CreateSuccess_NullParameterSizeLabel_IsStoredAsNull` | Optional label absent | Create success | null retained | Making optional data required |
| `CreateSuccess_NullQuantization_IsStoredAsNull` | Optional quantization absent | Create success | null retained | Same |
| `CreateSuccess_NullContextLength_IsStoredAsNull` | Optional context absent | Create success | null retained | Same |

These three classes total 74 test methods and 85 executions. The full packaged
suite adds nine model-file-picker executions and one testing-foundation
smoke execution, producing 95.

## 11. Production code walkthroughs

The following tables follow source order. Each row explains one statement or a
tightly coupled statement group without copying the large source file.

### `ScanAsync`

| Source-order statement | Meaning |
|---|---|
| Method signature | Accepts one path and one token and asynchronously returns a result. |
| `ArgumentException.ThrowIfNullOrWhiteSpace` | Enforces the caller contract before I/O. |
| `ThrowIfCancellationRequested` | Preserves cancellation before the path is opened. |
| `FileStream stream;` | Declares the resource so construction can have a narrow exception boundary. |
| `try` plus `new FileStream(...FileStreamOptions...)` | Opens only an existing file, read-only, shared for reading, asynchronous and sequential. |
| file/directory-not-found catches | Return `file-not-found`; only expected open-time races are converted. |
| unauthorized catch | Returns `file-access-denied`. |
| opening `IOException` catch | Returns `file-read-error`; it does not catch every exception. |
| `await using (stream)` | Guarantees asynchronous disposal. |
| inner `try` and awaited `ScanOpenedFileAsync` | Keeps orchestration separate from resource/opening policy. |
| exact `GgufFormatException` catch | Converts only scanner-known structural input failures. Cancellation, programming errors, and unrelated parse-time I/O are not broadly swallowed. |

### `ScanOpenedFileAsync`

| Source-order statement | Meaning |
|---|---|
| `ReadHeaderAsync` | Consumes all 24 fixed bytes. |
| version comparison | Returns `unsupported-version` before applying v3 metadata rules to another layout. |
| metadata-count comparison | Rejects a top-level loop above 1,000,000 before entry parsing. |
| `ReadMetadataAsync` | Performs one bounded pass and returns only small retained state. |
| blank architecture check | Converts absent/empty/whitespace architecture into the required-field failure. |
| conditional `modelName` | Uses the filename only when the retained name is unavailable. |
| `CreateSuccessResult` | Constructs the result only after required validation completes. |

### Header reading: `ReadHeaderAsync`, primitive helpers, and `ReadHeaderFieldAsync`

| Source-order statement | Meaning |
|---|---|
| Allocate four magic bytes | The size is fixed by the format, not file input. |
| `ReadHeaderFieldAsync(... "magic", 0)` | Exact-read magic with field-specific truncation detail. |
| `ReadUInt32Async(... "version", 4)` | Exact-read and little-endian-decode the version. |
| `ReadUInt64Async(... "tensor count", 8)` | Reads the complete next eight bytes. |
| `ReadUInt64Async(... "metadata entry count", 16)` | Finishes the 24-byte header. |
| compare magic after all reads | A short header is `truncated-header`, even if its available signature is wrong. |
| create `GgufHeader` | Carries only the three numeric values needed later. |
| primitive helper fixed buffers | Allocate four or eight bytes, exact-read, then call `BinaryPrimitives`. |
| exact-read `try` | Lets cancellation pass naturally. |
| `EndOfStreamException` catch | Calculates bytes read from `Position - fieldOffset`, clamps it to the field width, and creates `truncated-header`. |

### Metadata loop: `ReadMetadataAsync`

| Source-order statement | Meaning |
|---|---|
| `new GgufScanState()` | Starts small per-scan retained state. |
| `totalArrayElementCount = 0` | Makes the aggregate budget local to one file. |
| `for (ulong entryIndex...)` | Visits exactly the already-bounded declared entry count. |
| per-entry cancellation check | Makes a long metadata list interruptible. |
| `ReadMetadataKeyAsync` | Reads, bounds, strictly decodes, and validates the key. |
| `ReadMetadataTypeAsync` | Reads and validates type 0 through 12. |
| `ReadKnownMetadataValueAsync` | Either retains a first known field or structurally consumes the value. |
| assignment of returned total | Carries the aggregate array work budget across entries. |
| `return scanState` | Returns no arbitrary metadata object graph. |

### Keys: `ReadMetadataKeyAsync` and `ValidateMetadataKey`

| Source-order statement | Meaning |
|---|---|
| remember `lengthOffset`; allocate eight bytes | Gives the key length an exact diagnostic location and fixed buffer. |
| exact-read length with EOF translation | A short length is `truncated-metadata`. |
| little-endian `ulong` decode | Reads the declared byte count. |
| compare with 65,535 | Applies the GGUF key limit before narrowing. |
| calculate `remainingBytes` | Avoids unchecked end-position arithmetic. |
| compare declared length with remaining | Rejects truncation before allocation/read. |
| checked `int` conversion and allocation | Safe because 65,535 already fits. |
| exact-read key bytes | Consumes precisely the declared range. |
| strict UTF-8 decode | Rejects malformed encoding. |
| `ValidateMetadataKey` | Applies the narrower ASCII hierarchical grammar. |
| empty-key branch | Rejects zero characters with a safe rule message. |
| character loop and `segmentLength` | Tracks whether each dot-delimited segment is nonempty. |
| dot branch | Ends one segment; adjacent/leading dot fails. |
| allowed-character expression | Accepts only lowercase ASCII, digits, and underscore inside a segment. |
| invalid-character branch | Reports character code and index, not unsafe raw key text. |
| final segment check | Rejects a trailing dot. |

### Type and bounded retained-string reading

`ReadMetadataTypeAsync` exact-reads four bytes, decodes a little-endian
`uint32`, rejects anything above `Float64`/12, and casts only after validation.

`ReadRetainedStringAsync` follows this source order:

| Statement | Meaning |
|---|---|
| `ReadMetadataUInt64Async(... "string length")` | Reads the byte length exactly. |
| compare with 16 MiB | Rejects excessive input before allocation. |
| calculate remaining bytes | Uses subtraction at the current stream position. |
| remaining-range check | Returns truncation before allocating or seeking past EOF. |
| checked allocation | Converts to `int` only after the 16 MiB bound. |
| `ReadExactlyAsync` | Fills the bounded retained byte array. |
| EOF translation | Keeps key, stage, offset, expected, and actual counts. |
| `StrictUtf8.GetString` | Decodes required/display text without replacement characters. |
| decoder catch | Converts only invalid encoding into `invalid-metadata-encoding`. |

Unknown strings take a different path in `SkipMetadataValueAsync`: their
length and remaining bytes are validated, but their payload is skipped without
allocation or decoding.

### Known values: `ReadKnownMetadataValueAsync`

| Branch in source order | Meaning |
|---|---|
| `general.architecture` duplicate check | Later occurrences use the normal structural skip path. |
| architecture type requirement/read/flag | The first occurrence must be a string and becomes authoritative. |
| `ResolvePendingContextCandidate` | Connects a context that appeared earlier. |
| `general.name` branch | First string wins; duplicates are consumed; blank fallback is applied later. |
| `general.size_label` branch | First string wins and blank is normalized to `null`. |
| `general.file_type` branch | First `UInt32` wins. |
| architecture-known exact-context condition | Processes only the retained architecture's key. |
| context duplicate check | Preserves the first resolved value. |
| `ReadContextLengthAsync` | Accepts and normalizes `UInt32` or reads `UInt64`; other types fail. |
| architecture-unknown suffix condition | Sends a possible context to bounded pending storage. |
| final skip | Safely consumes every other official value. |

`ReadPendingContextCandidateAsync` first skips a duplicate exact candidate,
then enforces the 64-distinct-key cap. It reads supported integer widths,
otherwise structurally skips the payload while retaining its wrong type.
Finally it stores only type and optional normalized value.

`ResolvePendingContextCandidate` does nothing for blank architecture, walks at
most 64 entries, uses `IsArchitectureContextLengthKey`, rejects a matching
wrong type, and sets the first exact candidate. The exact-key helper compares
length, ordinal suffix, and ordinal prefix without a matching-size temporary
string.

### Metadata value consumption: `SkipMetadataValueAsync`

| Switch arm | Consumption |
|---|---|
| `UInt8`, `Int8` | Validate and seek 1 byte. |
| `Boolean` | Exact-read 1 byte; accept only 0 or 1. |
| `UInt16`, `Int16` | Validate and seek 2 bytes. |
| `UInt32`, `Int32`, `Float32` | Validate and seek 4 bytes. |
| `UInt64`, `Int64`, `Float64` | Validate and seek 8 bytes. |
| `String` | Exact-read length, enforce 16 MiB, validate remaining bytes, seek payload. |
| `Array` | Call `ConsumeArrayAsync` at depth 1. |
| terminal `InvalidOperationException` | Documents an internal impossible state after type validation; it is not converted into a format failure. |

### Arrays: `ConsumeArrayAsync`

| Source-order statement | Meaning |
|---|---|
| depth check | Rejects level 9 before descending. |
| cancellation check | Makes recursion interruptible. |
| read element type and count | Both use validated exact-read helpers. |
| per-array comparison | Rejects more than 1,000,000 before iteration. |
| subtraction-based total check | Prevents overflow while enforcing 4,000,000 total. |
| add element count | Updates the local scan budget only after checks. |
| element-width switch | Gives fixed numeric types widths 1, 2, 4, or 8; semantic types receive 0. |
| checked multiplication | Calculates fixed payload bytes without overflow. |
| `SkipValidatedBytes` | Seeks once only when the complete payload exists. |
| semantic `for` loop | Handles only already-bounded Boolean, string, or nested-array elements. |
| per-element cancellation | Keeps a large legal array cancellable. |
| nested-array branch | Recurse with `nestingDepth + 1` and the shared total. |
| other semantic branch | Reuse the normal value-consumption rules. |
| return total | Carries all nested work back to the caller. |

### Primitive metadata reads and validated skipping

`ReadMetadataUInt32Async` and `ReadMetadataUInt64Async` remember the current
offset, exact-read a fixed four/eight-byte array, translate only early EOF, and
decode little-endian.

`SkipValidatedBytes` calculates `stream.Length - Position`, compares the
requested `ulong`, converts to `long` only after it fits, and seeks from the
current position. `CreateTruncatedMetadataException` centralizes the stable
code and stage-rich diagnostic.

### Small result, validation, and state helpers

The remaining focused helpers keep policy out of the byte-reading code:

| Helper/type | Source-order behavior and reason |
|---|---|
| `CreateFileOpenFailure` | Receives an already-classified opening code/message, includes only the exception type in the technical detail, and creates a failure result without echoing the path. |
| `CreateFormatFailure` | Copies the three fields from the scanner-local `GgufFormatException` into the public result. It does not reinterpret the code. |
| `CreateInvalidKeySegmentException` | Builds the shared safe diagnostic for a leading, trailing, or adjacent dot without echoing the untrusted key. |
| `ReadContextLengthAsync` | Switches on `UInt32` or `UInt64`, normalizes either to `ulong`, and delegates every other type to the context-type failure. |
| `CreateInvalidContextTypeException` | Produces one consistent `invalid-context-type` result contract for both early and late exact context keys. |
| `IsArchitectureContextLengthKey` | Checks total length, then the ordinal `.context_length` suffix and architecture prefix; no culture-sensitive comparison is used. |
| `RequireMetadataType` | Returns for the required type and otherwise throws the caller-supplied stable known-field code. |
| `CreateTruncatedMetadataException` | Formats the key, stage, offset, actual bytes, and expected bytes in one place and optionally retains the original EOF exception. |
| `GgufHeader` | Immutable three-number record for version, tensor count, and metadata count. |
| `GgufScanState` | Per-scan bounded retained fields, first-occurrence flags, and the maximum-64 pending-context dictionary. |
| `PendingContextValue` | Immutable candidate type plus optional normalized integer; it never holds an arbitrary payload. |
| `GgufMetadataValueType` | Names the validated IDs 0 through 12 so switches are readable. |
| `GgufFormatException` | Scanner-local carrier for one stable code and two messages. Only this expected structural exception is translated by the parse boundary. |

### Quantization mapping line by line

`MapFileTypeToQuantization` is one switch expression. Each `N => "LABEL"` arm
is the exact mapping shown in section 6. The final discard arm interpolates the
original number into `Unknown (file type N)`. It does not throw and it does not
return `null`; `null` is reserved for an absent `general.file_type`.

`CreateSuccessResult` calls the result factory with retained fields, maps file
type only when it is present, uses the real `stream.Length`, and includes the
header version.

## 12. Representative test walkthroughs

### Malformed I-004: a truncated metadata string

1. The generator writes a complete v3 header declaring one entry.
2. It writes key `general.name` and type `String`.
3. It declares a 20-byte string but writes only `abc` (three bytes).
4. MSBuild deploys I-004 beneath
   `AppX\TestFixtures\Malformed`.
5. The test asserts the deployed file exists, so a packaging defect cannot
   masquerade as a parser defect.
6. The real scanner reads the header, key, type, and declared string length.
7. `ReadRetainedStringAsync` sees only three remaining bytes.
8. The scanner returns `Failure`/`truncated-metadata`; no EOF exception escapes.
9. The test asserts the key, `string payload` stage, offset 56, three available
   bytes, and 20 expected bytes.

This catches both an unsafe read and a vague diagnostic.

### Successful V-001 plus typed JSON

1. The generator writes representative Granite metadata and independently
   writes `V-001-complete-metadata-v3.json`.
2. The JSON names fixture ID V-001 and file
   `V-001-complete-metadata-v3.gguf`.
3. The test asserts both deployed files exist.
4. `JsonSerializer` creates typed `ExpectedFixture` and `ExpectedMetadata`
   records; a null record is a failed assertion.
5. The test independently checks fixture ID and filename before scanning.
6. The real scanner opens and parses the binary.
7. The test compares outcome, name, architecture, size label, quantization,
   exact file length, context, and version.
8. It asserts all failure fields are null.

This catches a wrong fixture/expectation pairing as well as incorrect parsing.

## 13. Fixture copying and the packaged WinUI runner

The test project has three wildcard `Content` groups:

```text
tests/TestFixtures/GGUF/**/*.gguf
tests/TestFixtures/Malformed/**/*.gguf
tests/TestFixtures/ExpectedMetadata/**/*.json
```

Each item uses `Link` to place it under
`TestFixtures\{GGUF,Malformed,ExpectedMetadata}` and
`CopyToOutputDirectory=PreserveNewest`. The paths are repository-relative, so
they do not depend on one developer's computer.

Building the WinUI MSTest project creates
`GraniteEdgeAI.UnitTests.build.appxrecipe`. Visual Studio's
`vstest.console.exe` reads that recipe, refreshes/deploys the `AppX` layout,
registers the test package, launches its executable, applies the optional test
filter, and writes TRX evidence.

This package host matters because the project is a WinUI/MSIX test app. A
successful build is not test execution, and a plain `dotnet test` run is not
the completion evidence used here.

## 14. Exact Task 9 commands and observed totals

The following commands were run from the repository root. The result
directories were created before `Resolve-Path`.

```powershell
$appProject =
  'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj'
$testProject =
  'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
$resultsRoot = 'TestResults\GGUF-Quick-Scanner'
$vswhere =
  "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere `
  -latest `
  -products * `
  -find '**\MSBuild\Current\Bin\amd64\MSBuild.exe' |
  Select-Object -First 1
$vstest = & $vswhere `
  -latest `
  -products * `
  -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' |
  Select-Object -First 1

New-Item -ItemType Directory -Force -Path `
  "$resultsRoot\Debug", "$resultsRoot\Release" | Out-Null

dotnet restore $testProject `
  --runtime win-x64 `
  -p:Platform=x64
```

### Debug

```powershell
& $msbuild $appProject `
  /target:Restore `
  /property:Configuration=Debug `
  /property:Platform=x64 `
  /property:RuntimeIdentifier=win-x64

& $msbuild $appProject `
  /target:Build `
  /maxCpuCount `
  /verbosity:minimal `
  /property:Configuration=Debug `
  /property:Platform=x64 `
  /property:RuntimeIdentifier=win-x64 `
  /property:PublishProfile= `
  /property:PublishTrimmed=false `
  /property:PublishReadyToRun=false `
  /property:AppxPackageSigningEnabled=false `
  /property:GenerateAppxPackageOnBuild=false

dotnet build $testProject `
  --configuration Debug `
  --no-restore `
  --runtime win-x64 `
  -p:Platform=x64

$debugRecipe = (Resolve-Path -LiteralPath `
  'tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe').Path
$debugResults = (Resolve-Path -LiteralPath `
  "$resultsRoot\Debug").Path

& $vstest $debugRecipe `
  /Platform:x64 `
  /TestCaseFilter:'FullyQualifiedName~GraniteEdgeAI.UnitTests.GgufQuickScannerTests' `
  /Logger:'trx;LogFileName=task-09-docs-debug-direct.trx' `
  "/ResultsDirectory:$debugResults"

& $vstest $debugRecipe `
  /Platform:x64 `
  /TestCaseFilter:'FullyQualifiedName~GraniteEdgeAI.UnitTests.ModelQuickScannerTests' `
  /Logger:'trx;LogFileName=task-09-docs-debug-router.trx' `
  "/ResultsDirectory:$debugResults"

& $vstest $debugRecipe `
  /Platform:x64 `
  /Logger:'trx;LogFileName=task-09-docs-debug-full.trx' `
  "/ResultsDirectory:$debugResults"
```

### Release

```powershell
& $msbuild $appProject `
  /target:Restore `
  /property:Configuration=Release `
  /property:Platform=x64 `
  /property:RuntimeIdentifier=win-x64

& $msbuild $appProject `
  /target:Build `
  /maxCpuCount `
  /verbosity:minimal `
  /property:Configuration=Release `
  /property:Platform=x64 `
  /property:RuntimeIdentifier=win-x64 `
  /property:PublishProfile= `
  /property:PublishTrimmed=false `
  /property:PublishReadyToRun=false `
  /property:AppxPackageSigningEnabled=false `
  /property:GenerateAppxPackageOnBuild=false

dotnet build $testProject `
  --configuration Release `
  --no-restore `
  --runtime win-x64 `
  -p:Platform=x64

$releaseRecipe = (Resolve-Path -LiteralPath `
  'tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe').Path
$releaseResults = (Resolve-Path -LiteralPath `
  "$resultsRoot\Release").Path

& $vstest $releaseRecipe `
  /Platform:x64 `
  /TestCaseFilter:'FullyQualifiedName~GraniteEdgeAI.UnitTests.GgufQuickScannerTests' `
  /Logger:'trx;LogFileName=task-09-docs-release-direct.trx' `
  "/ResultsDirectory:$releaseResults"

& $vstest $releaseRecipe `
  /Platform:x64 `
  /TestCaseFilter:'FullyQualifiedName~GraniteEdgeAI.UnitTests.ModelQuickScannerTests' `
  /Logger:'trx;LogFileName=task-09-docs-release-router.trx' `
  "/ResultsDirectory:$releaseResults"

& $vstest $releaseRecipe `
  /Platform:x64 `
  /Logger:'trx;LogFileName=task-09-docs-release-full.trx' `
  "/ResultsDirectory:$releaseResults"
```

The explicit standalone application restores/builds all exited 0. Debug restore
and build took 5.08 s and 13.42 s; Release restore and build took 3.56 s and
17.37 s. The restore logs reported zero warnings and errors; the minimal app
build logger emitted no warning or error diagnostics. Their relative logs are:

- `Debug\task-09-app-debug-restore.log`;
- `Debug\task-09-app-debug-build.log`;
- `Release\task-09-app-release-restore.log`; and
- `Release\task-09-app-release-build.log`,

all below `TestResults\GGUF-Quick-Scanner`.

Both packaged test-project builds also exited 0 with zero warnings and zero
errors. Durations below are the TRX `finish - start` interval, not a guessed
wall-clock value:

| Configuration | Scope | Total/executed/passed | Failed/error/inconclusive/not executed | TRX duration | Exit |
|---|---|---:|---:|---:|---:|
| Debug | direct scanner | 47/47/47 | 0/0/0/0 | 3.4979773 s | 0 |
| Debug | router | 11/11/11 | 0/0/0/0 | 2.7285634 s | 0 |
| Debug | full package | 95/95/95 | 0/0/0/0 | 2.7635008 s | 0 |
| Release | direct scanner | 47/47/47 | 0/0/0/0 | 9.6642398 s | 0 |
| Release | router | 11/11/11 | 0/0/0/0 | 2.3760642 s | 0 |
| Release | full package | 95/95/95 | 0/0/0/0 | 2.4790299 s | 0 |

The test-project builds additionally compiled their production-project
reference. Task 10 is responsible for repeating the standalone application and
packaged-test matrix after independent reviews and recording the authoritative
final results.

## 15. Limitations and how to extend this safely later

### Current limitations

- Only GGUF container version 3 is supported.
- Multi-byte fields are interpreted only as the little-endian encoding used by
  this implementation; it does not detect or byte-swap a big-endian variant.
- The scanner stops after metadata. It does not validate tensor descriptors,
  offsets, shapes, or data.
- `ModelImportPage` is not wired to call the router or display/set the result.
- OpenVINO quick scanning is not implemented.
- The file-type mapping is a reviewed snapshot of upstream values 0 through
  41 and can change upstream.
- Mapping tests are representative, not value-by-value exhaustive.
- `file-access-denied` and `file-read-error` have narrow production mappings
  but no deterministic packaged test that forces those host conditions.
- The scanner's limits are application policy. A valid but unusually large
  metadata section may be rejected intentionally.
- Duplicate handling is defined only for scanner-relevant fields and bounded
  pending context candidates, not every arbitrary key.
- `general.architecture` must be a nonblank strict UTF-8 string here, but the
  scanner does not additionally enforce the specification's lowercase ASCII
  architecture-value grammar.
- The test fixture files are structural examples with no model tensors; they
  do not prove inference compatibility.

### How to extend this safely later

1. Begin with the official current GGUF/llama.cpp source, not a copied blog.
2. Decide whether the change alters format support, application policy, or
   only display mapping.
3. Add or change a fixture in the appropriate PowerShell generator. Never
   hand-edit the binary.
4. Write a public-observable test with explicit Arrange, Act, and Assert.
5. For a defect, capture a genuine failing packaged run before changing code.
6. Check all file-declared counts before allocation, conversion, iteration,
   multiplication, addition, recursion, or seek.
7. Pass the caller token through every new asynchronous helper and check it in
   long semantic loops.
8. Catch only an exception that is expected at the boundary that can translate
   it accurately. Do not add `catch (Exception)`.
9. Keep unknown keys forward-compatible but reject unknown-width type IDs.
10. Preserve first-occurrence-wins behavior if adding another retained field,
    using fixed state rather than an unbounded global key set.
11. Regenerate, verify JSON/hashes/sizes, build both configurations, and run
    the packaged direct, router, and full matrices.
12. Update this guide, the evidence report, and the isolated mapping/limit
    tables when the contract changes.

## 16. Sources and textbook principles

### Primary technical sources

- The official [GGUF specification](https://github.com/ggml-org/ggml/blob/master/docs/gguf.md)
  defines the header, version 3 layout, metadata types 0 through 12, UTF-8
  strings, strict Boolean values, nested arrays, key grammar/length, and
  architecture-dependent metadata.
- The official current
  [llama.cpp `llama_ftype` enum](https://github.com/ggml-org/llama.cpp/blob/master/include/llama.h)
  is the source for active file-type labels through 41 and identifies removed
  historical slots.
- Microsoft Learn documents
  [`Stream.ReadExactlyAsync`](https://learn.microsoft.com/en-us/dotnet/api/system.io.stream.readexactlyasync?view=net-8.0),
  [`BinaryPrimitives`](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.binary.binaryprimitives?view=net-8.0),
  [`FileStreamOptions`](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestreamoptions?view=net-8.0),
  and the
  [`UTF8Encoding` constructor](https://learn.microsoft.com/en-us/dotnet/api/system.text.utf8encoding.-ctor?view=net-8.0).
- Microsoft Learn's
  [MSTest writing guide](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-writing-tests),
  [WinUI testing guide](https://learn.microsoft.com/en-us/windows/apps/develop/testing/),
  [VSTest console options](https://learn.microsoft.com/en-us/visualstudio/test/vstest-console-options?view=visualstudio),
  and
  [single-project MSIX guide](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/single-project-msix)
  support the attributes, packaged host, command filters/logger, and generated
  package model used here.

### Textbook principles applied

- *Code Complete* (local PDF pages 315-322, 353-378, 388-391, 533-535,
  559-560, and 669): use cohesive, precisely named routines; make
  trust-boundary checks and preconditions explicit; use consistent
  diagnostics, checked arithmetic, and endpoint tests.
- *Designing Secure Software* (local PDF pages 96, 177, 180-181, 189, 192,
  197-216, 245-252, and 257): treat the model as hostile input, validate
  lengths before use, cap resources, decode strictly, test thresholds, and
  translate exceptions near the boundary that understands them.
- *The Art of Unit Testing* (PDF pages 43-45/printed pages 15-17, 52-53/24-25,
  66-67/38-39, 181-184/153-156, 186-188/158-160, 197-202/169-174,
  205-210/177-182, 219-220/191-192, and 263-264/235-236): distinguish
  integration-style tests, keep tests readable and self-checking, use AAA and
  useful names, avoid test logic/shared state/private-method tests and
  overspecification, and establish coverage before refactoring.
- *Refactoring*, second edition (physical PDF pages 9-14, 54-55, 118-124,
  135-137, 176-178, 295-300, and 342-345): begin with a self-checking
  baseline, make small behavior-preserving transformations, extract only
  meaningful functions/variables, split phases, use guard clauses, and keep
  queries separate from modification.
- *Why Programs Fail*: reproduce first, capture the exact symptom, identify
  the first divergence, test one hypothesis with a discriminating experiment,
  correct the smallest root cause, and preserve an environment/debugging
  record. No page number is claimed because the retained research notes did
  not preserve a reliable page reference.
- The local `windows-apps.pdf` informed WinUI project orientation. Current
  technical claims in this guide are linked to the official Microsoft Learn
  pages above.

## Glossary

| Term | Meaning |
|---|---|
| allocation | Reserving memory, such as creating a byte array. |
| appxrecipe | Generated build instructions that tell Visual Studio how to lay out and deploy the packaged test app. |
| Arrange-Act-Assert (AAA) | Prepare input, perform one behavior, then check the result. |
| architecture | The model family named by `general.architecture`, such as `granite`. |
| array budget | The maximum declared elements the scanner will process in one scan. |
| asynchronous | Work that can await I/O without blocking the calling thread. |
| cancellation token | A value passed through operations so the caller can request that work stop. |
| checked conversion/arithmetic | A conversion or calculation that throws instead of silently overflowing. |
| component test | A test that exercises several real pieces together, such as parser plus filesystem and packaged fixture. |
| context length | The model's architecture-specific token context metadata. |
| controlled failure | A stable `ModelQuickScanResult` rather than a crash for expected bad input. |
| endianness | The order in which a multi-byte number stores its bytes. |
| fixture | A small deterministic input created for a test. |
| GGUF | A binary file format used to store models and metadata for GGML-based software. |
| little-endian | Least significant byte first. |
| magic bytes | A file signature at offset zero; GGUF uses `47 47 55 46`. |
| metadata | Typed key/value information that describes a model. |
| MSIX | The Windows package technology used by this WinUI test app. |
| quantization | A weight encoding/file-type label such as `Q4_K_M`; the scanner reports the label but does not inspect tensor encoding. |
| recursion | A method calling itself, used here only for bounded nested arrays. |
| retained string | Known text the scanner decodes and keeps for the result. |
| scanner | Code that validates and extracts a small bounded subset without loading the model. |
| seek | Move a stream position; this scanner seeks only over a byte range already proven to exist. |
| strict UTF-8 | Decoding that fails on invalid bytes instead of inserting replacement characters. |
| stream | An ordered interface to file bytes with a current position. |
| TRX | Visual Studio test-result XML containing counters, results, and times. |
| trust boundary | The point where untrusted file data enters application logic and must be validated. |
| VSTest | Visual Studio's test runner; here it deploys and launches the packaged WinUI test app. |

## Requirement map

This final map makes the 32 requested topics auditable.

| Items | Where covered |
|---|---|
| 1-3 | Section 1 |
| 4-5 | Section 2 |
| 6, 10-11 | Section 4 |
| 7-9 | Section 3 |
| 12-13 | Section 5 |
| 14 | Section 6 |
| 15 | Section 7 |
| 16 | Section 8 |
| 17-18 | Section 9 |
| 19 | Section 10 |
| 20-25 | Section 11 |
| 26-27 | Section 12 |
| 28-29 | Section 13 |
| 30 | Section 14 |
| 31 | Section 15 |
| 32 | Section 16 and Glossary |
