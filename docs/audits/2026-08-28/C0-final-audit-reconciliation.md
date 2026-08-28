# C0 final audit and reconciliation

Date: 2026-08-28

Branch: `audit/ucl-c0-audit-integration-v1`

Frozen source: `4748fe04f19afdf6b27c4c12502b84db325e7294`

Frozen tree: `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`

## Disposition

**CHANGES REQUIRED**

The selectively integrated C0 branch improves native-process isolation, GGUF
Chat lifetime safety, cross-feature contract coverage, and guarded packaged E2E
infrastructure. It is not a release-ready or native-acceptance branch. Five of
eight specialist handoffs have no valid receipt, the full Model Inspection
contract gate is red, the packaged solution lacks the required OpenVINO worker
stage, native acceptance prerequisites are absent, and source-confirmed product
defects remain in OpenVINO result binding, download, and export journeys.

No merge or push to `main` is authorized or performed by this audit.

## Identity and import ledger

Receipt validation used the published JSON schema and independently checked
hashes, byte counts, Git object types, branch tips, trees, clean claims, frozen
ancestry, and merge base. Candidate worktrees and reports without receipts were
review context only and were not treated as authoritative handoffs.

| Worker | Receipt and identity | C0 action | Final status |
| --- | --- | --- | --- |
| A1 | Valid. Tip `294fc9674e37663da43a3adfa1fb6c978be17f38`; tree `822e64564f4116956357a25177ca0f95ec1a4276`; report SHA-256 `3621a55df8be8a23513048b25dd4b0c3597b4fab4a799c39a724ec23f87ceefb`, 39,702 bytes | Imported the validated report only; independently reconciled its findings | Accepted evidence; no A1 production commit existed |
| T1 | Valid. Tip `1a50b6cf560a1cf47bc180079f1212fd4dd4416a`; tree `f4e3761aec5833f601591b5e3ac3ced49343895e`; report SHA-256 `c3dd2100923a7f4e39267c0383de7c1b484f55211e8faec18ec70704ad40465a`, 6,717 bytes | Imported three test-only commits and report; registered the suite; extended it with GGUF lifecycle coverage | Integrated and reconciled |
| E1 | Valid. Tip `e3e660a62cd9b247c6980b32a768ab5540d1e1a1`; tree `cd0a324ac06e5299675b373b411ee313beeddfe7`; report SHA-256 `a6706dcb0e32c95a8fe9d5d58dab5214b5d7e4308178fe46b3ee1973d9a32d82`, 9,160 bytes | Imported three implementation commits and report; hardened source binding, evidence parsing, file handling, discovery, freshness, locking, and cleanup | Deterministic layer integrated; native layer blocked |
| H1 | `H1.json` absent | Imported nothing | Hardware-owned follow-up and evidence subject unavailable |
| M1 | `M1.json` absent | Imported nothing | Model Inspection-owned workflows/manifests unavailable |
| Q1 | `Q1.json` absent | Imported nothing | OpenVINO optimization/result binding and producer evidence unavailable |
| F1 | `F1.json` absent | Imported nothing | Frontend fixes/report not authoritative |
| S1 | `S1.json` absent | Imported nothing; C0 independently reproduced and fixed the quantizer environment issue | Security handoff not authoritative |

The detailed receipt gate is retained in
`docs/audits/2026-08-28/C0-pre-integration-ledger.md`. Absence means no
authoritative import was available; it is not rewritten as a worker test failure.

## Reconciled Git graph

The branch is a linear selective-integration history rooted at the frozen commit:

```text
4748fe04 frozen audit source
  92e32323 T1 test start
  30bf503a T1 planning identity coverage
  d26c4e92 T1 execution/output recovery coverage
  5b233fb4 C0 solution registration
  56ada60a C0 receipt ledger
  8131e110 C0 quantizer environment isolation
  55e93952 C0 GGUF Chat lifetime coordination
  bc236c9f C0 E1 receipt validation
  492a3870 E1 automation project
  4404ded5 E1 guarded native journeys
  0458c580 E1 nested host binding
  f860d5b2 C0 E1 evidence hardening
  8e8e2e33 A1 validated report
  fefb815f T1 validated report
  2b9ca575 E1 validated report
  <final C0 report commit>
```

The final report commit, branch tip, and tree are computed after this document is
committed and are emitted in the external C0 handoff. No specialist branch was
merged wholesale and no merge commit obscures the selected provenance.

## Integrated and C0-authored changes

### Imported, validated work

- T1: `92e32323`, `30bf503a`, `d26c4e92`.
- E1: `492a3870`, `4404ded5`, `0458c580`.
- Validated reports: A1, T1, and E1 only.

### C0 reconciliation work

- `5b233fb4` minimally registers the cross-feature suite.
- `8131e110` replaces parent-environment inheritance for the native GGUF
  quantizer with a closed operational allowlist and forced-off .NET diagnostics.
- `55e93952` coordinates GGUF prepare/generate/stop/dispose lifetime and makes
  Chat controller event cancellation and teardown awaitable and bounded.
- `f860d5b2` binds E1 candidates to the exact integrated commit/tree, preserves
  the frozen-ancestor proof, streams asset hashes, rejects reparse points and
  oversized/non-regular inputs, validates evidence arithmetic and stable kinds,
  enforces fresh builds and authoritative discovery, and protects native lock
  release with candidate-process cleanup.

### Deliberately omitted or blocked

- No H1, M1, Q1, F1, or S1 commit or report was imported without a valid receipt.
- No native worker, model tool, conversion, quantization, Chat, package
  activation, or UI Automation acceptance run was started without all producer
  evidence and the native lock.
- No candidate branch was merged wholesale. No history was rewritten.

## Finding reconciliation

| Finding | Evidence and C0 disposition |
| --- | --- |
| Onboarding shell is an oversized composition root | A1 source review confirmed the concentration. Open: structural refactor requires separately scoped product work. |
| GGUF Chat can dispose a session during active generation and controller events can outlive teardown | Independently reproduced with a red lifecycle test, fixed by C0, and covered by two passing integration tests. |
| GGUF quantizer inherits the full parent environment | Independently reproduced by sentinel leakage, fixed with a closed environment policy, and covered by eight passing focused tests. |
| OpenVINO activation collapses typed failure to a boolean/catch-all path | Source-confirmed and open. Q1 is unreceipted; C0 did not invent an incompatible contract. |
| OpenVINO optimization performs redundant staging and does not reliably bind Chat/export to the exact selected result | Source and candidate-review confirmed. Open; Q1 producer/result work is unreceipted and the full package stage is unavailable. |
| Recommended-model download action is inert | Source-confirmed from the unreceipted frontend review context. Open. |
| Export is not demonstrably bounded and identity/integrity-bound to the exact optimization result | Source-confirmed. Open; native E2E export acceptance remains guarded. |
| Manifest/runtime authority is duplicated across seams | A1 source-confirmed. Partially reduced in E1 verification only; product authority remains open. |
| System clock and duplicated fallback logic reduce deterministic testability | A1 source-confirmed. Open; outside the safe C0 reconciliation scope. |
| Warnings are not treated as errors | Confirmed by the final solution build producing nine nullable warnings. Open. |

## Journey and route parity

| Journey | Picker / drag-drop parity | Managed evidence | Native/package status |
| --- | --- | --- | --- |
| Import and ingress privacy | T1 covers both ingress identities and path redaction contracts | Cross-feature suite passes | Package launch/automation guarded |
| Hardware inspection and compatibility | Shared hardware snapshot/identity boundaries covered | Foundation 202/202; probe 22/22; compatibility 1050/1050 | H1 evidence handoff absent |
| Model inspection and handoff | T1 covers handoff identity and stale/mismatch rejection | Transport 27/27 and client 119/119; contract gate 335/357 | M1 receipt absent; worker/process execution partly blocked by Application Control |
| Optimization planning/execution/recovery | T1 covers plan identity, cancellation, publication failure, and output recovery | Cross-feature suite passes | Exact Q1 OpenVINO producer/result handoff absent |
| GGUF Chat | Picker/drag-drop plan seams covered; C0 adds active-generation disposal and stop concurrency | All focused GGUF managed suites pass | Controlled local model checks skipped by declared guards |
| OpenVINO Chat | Planning seams covered, but exact optimized configuration is not proven at runtime | Worker client 13/13 | Contracts/unit rerun blocked by Application Control; native stage absent |
| Export/restart | T1 covers persistent output recovery contracts | Managed recovery checks pass | Exact-result export integrity and packaged restart remain unaccepted |

Managed contracts demonstrate identity propagation at selected seams; they do not
substitute for the guarded package/native journeys.

## Verification ledger

All counts below are from the C0 worktree unless explicitly marked as a guarded
or environmental outcome. `P/F/S` means passed, failed, skipped.

| Scope | P/F/S | Result |
| --- | ---: | --- |
| Model/hardware compatibility | 1050/0/0 | Pass |
| Hardware Foundation | 202/0/0 | Pass |
| Hardware llama.cpp probe | 22/0/0 | Pass |
| Model Inspection contracts | 335/22/0 | Fail: missing unreceipted H1/M1-owned workflows/fixture boundaries plus a pre-existing unresolved package-item expression |
| Model Inspection transport | 27/0/0 | Pass |
| Model Inspection worker | not executed | Application Control blocked the worker dependency assembly |
| Model Inspection worker client | 119/0/0 | Pass |
| Model Inspection worker process | 29/3/0 | Fail: production worker launch blocked by Application Control |
| OpenVINO contracts | 205/0/0 on earlier frozen rerun; final rerun blocked | No current package acceptance claim |
| OpenVINO unit | 402/0/7 on earlier frozen rerun; final rerun blocked | Seven declared guard skips; no current package acceptance claim |
| OpenVINO worker client | 13/0/0 | Pass |
| OpenVINO worker process | 20/34/15 | Required worker stages/environment unavailable; native result not accepted |
| GGUF runtime contracts | 12/0/0 | Pass |
| GGUF runtime transport | 15/0/0 | Pass |
| GGUF runtime capabilities | 13/0/0 | Pass |
| GGUF runtime worker | 21/0/0 | Pass |
| GGUF runtime worker client | 9/0/0 | Pass |
| GGUF runtime worker process | 24/0/1 | Pass with controlled runtime/model guard |
| GGUF native adapter | 40/0/4 | Pass with controlled model guards |
| GGUF quantization worker client | 8/0/0 | Pass, including environment isolation regression |
| Cross-feature integration | 52/0/0 | Pass, including two C0 GGUF lifecycle tests |
| E1 deterministic/guard diagnostic | 18/0/17 | Pass; all 17 native/package journeys skipped by declared guards |

The Debug/x64 solution build reached the packaging graph but failed with one
error because `OpenVinoOfficialWorkerStageDirectory` was not supplied. It also
reported nine existing nullable warnings. Supplying an unvalidated directory to
turn that fail-closed gate green would invalidate the audit.

The E1 project itself built successfully during reconciliation. The authoritative
VSTest/package runner remains blocked by absent producer receipts/stages and the
machine's package/Application Control prerequisites. The diagnostic `dotnet test`
run is reported only as managed test evidence, never as native acceptance.

## Security, privacy, packaging, and environment

- Native GGUF quantization no longer receives arbitrary parent variables such as
  credentials, tokens, proxy settings, or caller `PATH`; only required Windows
  and temporary-directory values are copied, with .NET diagnostics disabled.
- E1 JSON inputs are regular, bounded files; executable and asset bytes are
  exact-hash checked; asset hashing is streamed; file and directory reparse
  points are rejected.
- E1 diagnostic capture redacts local paths. No raw TRX, model weights, local
  model paths, usernames, machine names, or secrets are committed by C0.
- The app/package gate fails closed when validated OpenVINO worker staging is
  absent. C0 did not weaken it, synthesize producer evidence, or mutate the
  machine-wide runtime environment.
- Candidate-review context indicated whole-file OpenVINO hashing and weak export
  bounds in unreceipted work. Those candidates were not imported; the underlying
  product risks remain release blockers.

## Required follow-up before acceptance

1. Publish and validate H1, M1, Q1, F1, and S1 receipts against the agreed schema,
   then selectively review/import only required commits.
2. Bind OpenVINO Chat and export to the exact selected optimization result,
   including persistent/runtime configuration identity, bounded streaming hashes,
   cancellation, stale-result rejection, and cleanup.
3. Implement and test the recommended-model download action and bounded,
   integrity-checked export journey.
4. Repair the Model Inspection contract failures and package-item expression
   without packaging audit-only fixtures or private evidence.
5. Produce validated H1/M1/Q1 evidence manifests and worker stages; then run the
   serialized E1 native smoke, failure, acceptance, and restart journeys under the
   audit lock on a package-capable host.
6. Re-run the full Debug/x64 solution and production package gates with no errors,
   reconcile warnings, scan package contents, and verify no orphan processes or
   environment mutations remain.

## Final report identity

The authoritative byte count and SHA-256 of this report are computed after the
final commit and emitted in the C0 handoff. They cannot be embedded in the file
being hashed without creating a self-referential digest.
