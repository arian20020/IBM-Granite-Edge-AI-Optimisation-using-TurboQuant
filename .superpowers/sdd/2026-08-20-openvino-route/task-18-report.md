# Task 18 report: fail-closed release acceptance gate

## Outcome

Task 18's local release-gate implementation is complete. The repository now
has ordered packaging, architecture, accessibility, artifact-privacy, cleanup,
and evidence-admission checks. The gate deliberately returns
`openvino_release_blocked` when the required hosted/UCL artifacts and external
security/license decisions are absent.

This task does **not** claim release acceptance. Central TurboQuant
registration and packaging remain closed until one immutable candidate has a
real pinned-Granite matched campaign, trusted normal-WinUI UCL evidence, and
external security/license approval. GPU-01 and the controlled 398-atom RTM
regeneration also remain external I0 acceptance work.

## Implemented boundary

- Added one exact 15-stage release orchestrator: dependency locks, contracts,
  inspection, official native, converter, optimization, TurboQuant,
  app/accessibility, GGUF regression, Release package, hosted evidence, UCL
  evidence, privacy, cleanup, and traceability.
- Bound official, converter, and TurboQuant closures to separate roots and
  exact manifests. Evidence must descend the supplied roots and match the
  candidate commit, model digest/length, package digest, CPU device identity,
  fixed rubric, two-turn campaign, and external approval dispositions.
- Added bounded recursive privacy scanning for JSON, TRX, typed text/log, and
  ZIP artifacts, including duplicate-key, DTD, traversal, nested-archive,
  path/identity, environment, prompt/output, credential, and raw-stream
  rejection.
- Added cleanup scanning for route/converter/TurboQuant/fixture processes and
  descendants, owned staging/partial/lock residue, locked files, and named
  pipes.
- Added route-neutral architecture, explicit packaging, ambient-DLL,
  malformed/privacy, cleanup, gate-order, and accessibility/lifecycle
  contracts.
- Reconciled the shared cleanup ledger at 638/638 and narrowly updated two
  stale Model Inspection test oracles for the reviewed OpenVINO consumers and
  packaging item expression.
- Made the packaged UI build-evidence assertion derive the digest from the
  packaged manifest that the application build and runtime already pin and
  verify, avoiding an obsolete test-only digest constant.

## Verification

- OpenVINO contracts: 187/187 passed.
- Model Inspection contracts: 357/357 passed; cleanup inventory: 3/3.
- OpenVINO app tests: 272/272 passed.
- OpenVINO WorkerClient: 13/13; Model Inspection WorkerClient: 119/119.
- Model Inspection process integration: 32/32.
- OpenVINO process integration against final converter Stage P: 61 passed,
  zero failed, one explicit physical-GPU skip (62 total).
- Converter focused Stage P: 5 applicable tests passed after raising only the
  isolation test timeout to ten minutes for a 23,756-file cold scan.
- Packaged Release/x64 affected WinUI slice: 580/580 passed; canonical official
  route E2E also passed alone 1/1.
- Release/x64 packaged build emitted `worker_manifest_valid` and
  `worker_manifest_digest_valid` for
  `db46a1c79a6bd2199eb4d9434ba406a1de51a11b060a4108a100542bdf9e39d3`.
- Dependency locks, GenAI fixture, official/TurboQuant/converter manifests,
  privacy/cleanup contract matrices, and diff hygiene passed locally.
- With no external evidence environment, the release gate returned exactly
  `openvino_release_blocked` with exit code 1.

## Open acceptance inputs

- Hosted exact-candidate evidence and trusted UCL CPU evidence.
- Trusted UCL normal-WinUI TurboQuant activation plus a real pinned-Granite
  matched quality/memory/performance campaign.
- External security and license approval.
- Physical GPU-01 evidence if that optional tuple is to be exposed.
- I0-owned controlled RTM workbook update and regeneration for all 398 P1 atoms
  and the scoped seam/finding/feature/decision identities.

Until those inputs agree on one clean immutable commit, the truthful terminal
state is blocked and no experimental capability is exposed.
