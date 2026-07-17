# EP-018 CI Evidence Closure Design

## Goal

Preserve the successful GitHub-hosted Windows CI result, update repository documentation so it no longer describes hosted validation as pending, and record the approved transfer of fixture and manifest work to existing engineering practice EP-023.

## Scope

This closure covers the narrowed EP-018 implementation boundary:

> Establish executable test-project structure, testing-platform configuration, Windows CI, test-result generation and preserved CI evidence.

The repository evidence will record that this implementation boundary is complete. It will not directly change the controlled RTM status or generated traceability snapshots. Formal RTM closure remains dependent on updating the authoritative workbook and regenerating its repository outputs through the controlled process.

## Authoritative hosted-run evidence

The preserved evidence derives from GitHub Actions artifact `unit-test-results-29463973146-1`:

| Field | Value |
|---|---|
| Workflow run ID | `29463973146` |
| Workflow run number | `6` |
| Job ID | `87513108129` |
| Head commit | `30b311f5576bfdfacf2928891bc7c7adeadb1b90` |
| Result | Success |
| Application build | Passed |
| Unit-test project build | Passed |
| Unit tests | Passed: 1; failed: 0; skipped: 0 |
| Artifact | `unit-test-results-29463973146-1` |
| Artifact archive SHA-256 | `8e6e55d72e49be2814ef651bda6bebdced5e1ae5ff7aea8fd63e868cff97274b` |

The supplied ZIP contains exactly one file, `GraniteEdgeAI.UnitTests.trx`. The implementation will extract that exact entry, calculate and record its independent SHA-256, and preserve it without editing its XML content.

## Repository changes

### Documentation updates

`docs/architecture/diagrams/CI-Build-and-Test-Workflow.md` will change its status from hosted validation pending to hosted validation confirmed. Its source-of-truth relationship to the workflow and its engineering-process claim boundary will remain unchanged.

`tests/README.md` will state that a clean GitHub-hosted Windows runner successfully completed application build, unit-test project build, Microsoft Testing Platform execution, TRX generation and artifact upload. It will not claim application behavior coverage beyond the infrastructure smoke test.

### Permanent evidence pack

The following files will be added:

```text
docs/evidence/engineering-practices/EP-018/
├── README.md
├── CI-Validation-Summary.md
└── GraniteEdgeAI.UnitTests.trx
```

`README.md` will be the human-readable EP-018 evidence record. It will define the narrowed implementation boundary, map completed criteria to authoritative repository and hosted-run evidence, state limitations, and distinguish repository evidence completion from formal RTM validation.

`CI-Validation-Summary.md` will record the workflow, job, commit, result, artifact identity, archive checksum, extracted TRX checksum, and test totals. It will explain that the committed TRX remains available after GitHub's temporary artifact expires.

`GraniteEdgeAI.UnitTests.trx` will be copied byte-for-byte from the verified GitHub artifact. It is authoritative raw test evidence and will not be reformatted.

## Approved deferral and traceability

Fixture files, the fixture manifest and fixture-manifest integrity validation are deferred from the narrowed EP-018 boundary to:

| Relationship | Stable ID and responsibility |
|---|---|
| Primary engineering-practice owner | `EP:EP-023` — Implement unit tests for pure logic |
| Primary requirement | `REQ:F-M03` — Validate a selected input before using it |
| Primary work package | `WP:IM-04` — Format detector and GGUF header validation |
| OpenVINO classification extension | `WP:IM-06` — Other-format recognition and classification |
| Later runtime integration only | `EP:EP-024` |

The evidence record will use this wording:

> Fixture files, the fixture manifest and fixture-manifest integrity validation are deferred from EP-018 to EP-023. The first implementation will support REQ:F-M03 and WP:IM-04 during development of deterministic model-input validation. OpenVINO-specific fixture scenarios will additionally trace to WP:IM-06 and, where runtime integration is involved, EP-024.

No new stable task ID will be invented.

## Controlled traceability boundary

The implementation will not manually edit:

- `docs/traceability/data/task-catalogue.json`;
- `docs/traceability/generated/Active-Task-Catalogue.md`;
- any other generated traceability document.

Those files remain outputs of the controlled RTM workbook. The EP-018 evidence record will visibly state that the workbook must adopt the narrowed wording, transfer the deferred scope to EP-023, update statuses as approved, and regenerate the repository snapshots before formal RTM closure can be claimed.

## Validation

The implementation will verify:

1. the supplied artifact archive SHA-256 matches the recorded digest;
2. the archive contains exactly one TRX with the expected filename;
3. the committed TRX hash matches the extracted artifact entry;
4. the TRX records MSTest SDK 4.0.2, one executed test, one passed test and zero failures;
5. documentation no longer says GitHub-hosted validation is pending;
6. the evidence summary contains the supplied run, job, commit and artifact identities;
7. deferral wording names EP-023, F-M03, IM-04, IM-06 and EP-024 with the approved boundaries;
8. no generated traceability file changed;
9. `git diff --check` passes for every task-owned file.

## Claim boundary

This evidence proves that the executable MSTest and Windows CI foundation operated successfully on the recorded clean GitHub-hosted Windows run and that its raw TRX evidence is permanently preserved in the repository.

It does not prove model-input validation behavior, GGUF/OpenVINO fixture correctness, contract testing, integration testing, end-to-end testing, code coverage, runtime adapter behavior, model inference, performance or release readiness.
