# M1 Model Inspection remediation R2

## Disposition

M1's managed Model Inspection lane is green. The implementation commit is
`58f5bb475f0ac97be157b5adf8481e57516c722f` with tree
`661cfb59a5263d47e7c6d2f85f46bfe0d9690ee7`. It is based directly on the
authoritative C0 integration commit
`a5ef3558334e50587889140dafba194853938765` (tree
`90c34ab009b744d7b00866fb93e8dbc86363f1b2`).

Native Model Inspection was not attempted. Both
`C:\UCL-AUDIT-HANDOFFS\H1.json` and
`C:\UCL-AUDIT-NATIVE-RECEIPTS\H1.json` were absent at the initial gate and
again after managed verification. Therefore M1 did not enter the native lock
or phase protocol and did not publish an M1 native phase receipt.

## Implemented remediation

- Added one strict, bounded, canonical schema-v2 projection shared by GGUF and
  OpenVINO. It exposes only model type, route, byte length, lowercase SHA-256,
  terminal outcome, and UUIDv4 run/handoff identities; it has no path field or
  free-form model metadata.
- Kept GGUF and OpenVINO route validation separate. The infrastructure factory
  accepts output only after the existing GGUF handoff projector or OpenVINO
  inspection handoff factory has validated route-specific evidence.
- Enforced exact source/result/handoff binding and rejected stale runs,
  changed digests or lengths, route/model-type mismatches, malformed or
  non-canonical JSON, unsupported types, unknown fields, reordered fields,
  trailing content, and oversized input.
- Hardened nested worker evidence for file digests, configuration ranges,
  tokenizer consistency, special-token bounds, and chat-template
  presence/digest consistency while retaining legacy protocol-v1 empty-template
  parsing compatibility.
- Restored the seven Model Inspection/LLamaSharp controlled workflows required
  by the live build contracts and repaired the Tier-1 sparse checkout so the
  continuity fixture is an explicit input.
- Repaired fixture ownership checks so only Model Inspection and approved
  onboarding fixture roots enter Debug x64 evaluation. Unrelated Hardware
  Inspection debug fixtures are no longer misclassified as M1 assets, while
  imported packaging expressions are accepted only by exact target and exact
  item expression.
- Reconciled the cleanup source list and ledger to 674 exact, sorted, unique,
  existing paths.

No shared solution, project-reference, package, onboarding/navigation,
composition, or Q1 optimization behavior was changed. C0 therefore needs no
proposed shared composition diff beyond importing this branch.

## Verification

| Gate | Result |
| --- | ---: |
| Model Inspection contracts (complete, including privacy, package boundary, duplication, Debug x64 and non-target fixture evaluation) | 387/387 passed |
| Model Inspection transport | 27/27 passed |
| Model Inspection worker | 78/78 passed |
| Model Inspection worker client | 119/119 passed |
| Model Inspection worker-process integration | 32/32 passed |
| Model/hardware compatibility handoff | 1050/1050 passed |
| Hardware Inspection foundation cross-feature | 202/202 passed |
| Deterministic LLamaSharp/runtime contracts | 191/191 passed |
| OpenVINO managed route | 402 passed, 7 controlled-stage tests skipped |
| Debug x64 application/unit build | succeeded, 0 errors; 9 pre-existing nullable warnings outside M1 production changes |
| `git diff --check` | passed |
| Cleanup source/inventory reconciliation | 674/674, unique and sorted |

The seven OpenVINO skips require the controlled converter or official-worker
stage environment variables and do not represent executed failures.

Two focused `GraniteEdgeAI.UnitTests` projection-factory tests compile, but a
loose VSTest launch cannot initialize the Windows App SDK on this host. Both
terminate before test logic with `REGDB_E_CLASSNOTREG` from
`DeploymentManagerCS.AutoInitialize`. This is independently separated as a
host-registration failure: the same application project and factory compile
successfully, the shared projection tests pass in the headless contract host,
the existing GGUF projector tests compile, and the OpenVINO handoff factory
executes in the 402-test managed route gate. This was not an Application
Control block, and no policy bypass was attempted.

The contract privacy scripts require Windows PowerShell's explicit
`-ExecutionPolicy Bypass` when launched by the test host. That setting only
permits the checked-in scripts to run; it does not bypass Application Control.
The complete contract gate then passed 387/387.

## Evidence and non-claims

The evidence manifest is
`docs/audits/2026-08-28/evidence/M1-model-inspection-evidence-v1.json`. Its
subject is the implementation commit/tree above, not the later report commit.
The stable evidence kinds collectively include `modelSource`,
`modelInspectionResult`, and `modelInspectionHandoff`.

No native GGUF load, native OpenVINO validation, controlled visual capture,
hosted CI execution, Application Control pass/fail, or H1 completion is
claimed. No local model path, user identity, model filename, or unapproved
metadata is present in the schema-v2 projection or evidence manifest.
