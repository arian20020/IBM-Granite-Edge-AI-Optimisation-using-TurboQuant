<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# Test Fixtures

## Purpose

Small safe model headers, metadata, fake process outputs and other deterministic test data.

## What belongs here

- Fixture provenance and purpose.
- Expected parser/validator result.
- No copyrighted or large model content.
- Checksum for binary fixtures.

## Related IDs

None assigned

## Evidence rules

- Folder creation is only preparation; it is not proof of completion.
- Use stable requirement, work-package, test/evidence and research-question IDs.
- Preserve raw evidence; derive processed results with version-controlled scripts.
- Record dates, versions, hashes, units, actual device/backend state and failures where relevant.
- Do not commit secrets, API keys, private personal data, large model weights or unlicensed material.
- Prefer relative repository links so evidence remains usable after cloning.

## GGUF generation and integrity

Run `Generate-GgufFixtures.ps1`; never hand-edit its generated `.gguf` outputs.
The orchestrator removes the complete generated set before running both leaf
generators, so deleted or renamed writer calls cannot leave stale fixtures.
The metadata generator then refreshes `fixture-manifest.json` with the exact
byte length and SHA-256 of every current GGUF fixture. Expected successful
scanner results remain in `ExpectedMetadata` and are not derived from
production code.

## Model Inspection scenario catalogue

[ModelInspectionScenarios](ModelInspectionScenarios/README.md) contains the
authoritative synthetic catalogue used by the Debug x64 fixture gallery. It is
the exact contiguous `MI-001` through `MI-050` set. Descriptor filenames follow
`MI-NNN-<target-condition>[-<variant>].fixture.json`, where the target and
optional variant are lowercase hyphenated slugs and the prefix exactly matches
the descriptor ID. IDs and filenames are stable review contracts, not values
to renumber or infer from production output.

`MI-050` records the secure-start surface before any worker progress exists: one
service attempt is active at an unreleased terminal checkpoint, all five stage
rows remain Waiting, and only the startup status is active. It remains wholly
synthetic and exercises the same loaded gallery, Cancel, Reset, and retirement
paths as the rest of the catalogue.

The packaged gallery reads only the allowlisted schema, coverage policy, and
descriptor names below the fixed
`ms-appx:///Fixtures/` root. These fixtures contain
synthetic requests, progress, terminal results, failures, and lifecycle events;
they contain no model bytes and do not invoke the worker or external I/O. The
[generated catalogue report](../../docs/evidence/testing/Model-Inspection-Fixture-Catalog.md)
is the exact inventory and records the only approved external linkage: N-001
real-worker/page evidence for the matching Ready collapsed/expanded screens.
That linkage does not turn either gallery row into real-worker output.

The final local Task 10 gate passed the synthetic Debug fixture category
220/220 and the separately filtered real packaged N-001 journey 1/1. Release
isolation then scanned 112 files, confirmed a ReadyToRun main assembly, and
found zero forbidden fixture/gallery path, token, or metadata hits. These are
local hosted-equivalent results; hosted GitHub exact-head evidence is still
pending.

## Source

Repository evidence structure
