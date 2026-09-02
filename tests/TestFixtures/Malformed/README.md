# Malformed GGUF Fixtures

This directory contains deliberately invalid or damaged GGUF files used to
verify that the quick scanner rejects unsafe input correctly.

Run `../Generate-GgufFixtures.ps1` to clean and recreate every binary through
the header and metadata leaf generators; do not edit these files by hand.

| ID | File | Bytes | Failure condition |
|---|---|---:|---|
| I-000 | `I-000-empty-file.gguf` | 0 | Empty fixed header |
| I-001 | `I-001-invalid-magic.gguf` | 24 | Invalid GGUF magic |
| I-002 | `I-002-unsupported-version.gguf` | 24 | Unsupported version |
| I-003 | `I-003-truncated-header.gguf` | 12 | Truncated tensor-count field |
| I-004 | `I-004-truncated-metadata-value.gguf` | 59 | Truncated metadata string |
| I-005 | `I-005-oversized-key-length.gguf` | 32 | Key length above 65,535 bytes |
| I-006 | `I-006-unknown-value-type.gguf` | 51 | Unsupported metadata type 99 |
| I-007 | `I-007-excessive-metadata-count.gguf` | 24 | More than 1,000,000 entries |
| I-008 | `I-008-missing-required-architecture.gguf` | 224 | Missing required architecture |
| I-009 | `I-009-wrong-architecture-type.gguf` | 64 | Architecture has the wrong type |
| I-010 | `I-010-invalid-boolean-value.gguf` | 52 | Boolean byte is neither 0 nor 1 |
| I-011 | `I-011-truncated-array.gguf` | 66 | Array payload is truncated |
| I-012 | `I-012-oversized-metadata-string.gguf` | 68 | String length above 16 MiB |
| I-013 | `I-013-excessive-array-count.gguf` | 71 | More than 1,000,000 elements in one array |
| I-014 | `I-014-excessive-total-array-count.gguf` | 119 | Nested declarations exceed 4,000,000 total elements |
| I-015 | `I-015-excessive-array-depth.gguf` | 160 | Nine nested array levels |
| I-016 | `I-016-invalid-context-type.gguf` | 128 | Exact context candidate has the wrong type |
| I-017 | `I-017-excessive-context-candidates.gguf` | 2,816 | Sixty-five distinct context candidates |
| I-018 | `I-018-invalid-utf8-architecture.gguf` | 66 | Architecture contains malformed UTF-8 |
| I-019 | `I-019-non-ascii-key.gguf` | 64 | Metadata key contains a non-ASCII character |
| I-020 | `I-020-wrong-name-type.gguf` | 128 | Name has the wrong type |
| I-021 | `I-021-wrong-size-label-type.gguf` | 128 | Size label has the wrong type |
| I-022 | `I-022-wrong-file-type.gguf` | 128 | File type has the wrong type |
| I-023 | `I-023-blank-architecture.gguf` | 96 | Architecture is whitespace-only |
| I-024 | `I-024-short-invalid-magic.gguf` | 12 | Invalid magic in an incomplete header |
| I-025 | `I-025-empty-metadata-key.gguf` | 37 | Empty metadata key |
| I-026 | `I-026-empty-key-segment.gguf` | 50 | Empty hierarchical key segment |
| I-027 | `I-027-key-with-space.gguf` | 49 | Key contains a space |
| I-028 | `I-028-uppercase-key.gguf` | 49 | Key contains uppercase ASCII |
| I-029 | `I-029-excessive-total-key-bytes.gguf` | 46 | Individually valid key lengths exceed the 65,535-byte scan budget |
| I-030 | `I-030-invalid-boolean-array-value.gguf` | 4,166 | Invalid Boolean just beyond one 4 KiB validation chunk |
| I-031 | `I-031-oversized-string-array-value.gguf` | 32,844 | Oversized string after 4,096 empty string-array elements |
| I-032 | `I-032-truncated-nested-array.gguf` | 49,229 | Truncated nested child after 4,096 empty parent elements |
| I-033 | `I-033-oversized-retained-string.gguf` | 64 | Retained architecture string length above 16 MiB |
| I-034 | `I-034-cross-entry-array-total.gguf` | 4,000,219 | Separate top-level arrays exceed 4,000,000 total elements |
| I-035 | `I-035-truncated-key-length.gguf` | 28 | Key-length field stops after four of eight bytes |
| I-036 | `I-036-truncated-key-bytes.gguf` | 34 | Key payload stops after two of five declared bytes |
| I-037 | `I-037-truncated-value-type.gguf` | 35 | Value-type field stops after two of four bytes |
| I-038 | `I-038-invalid-utf8-key.gguf` | 34 | Metadata key contains malformed UTF-8 |

Exact SHA-256 values and byte lengths are regenerated into
`../fixture-manifest.json`.
