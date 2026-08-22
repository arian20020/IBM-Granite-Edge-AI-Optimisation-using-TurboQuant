# Prompt for the UCL Intel laptop Codex worker

You are continuing the IBM Granite TurboQuant/OpenVINO feature on the authorized
UCL Intel Windows x64 laptop. Work inline in the reconstructed repository; do
not dispatch subagents unless the user explicitly changes that instruction.
Persist until every locally and externally available gate is genuinely closed.
When an external authorization or owner-controlled artifact is unavailable,
finish every safe in-scope check, leave the feature fail closed, and report the
exact blocker. Never convert missing evidence into a pass.

## Mission and terminal condition

Finish trusted hardware validation and release closure for a protected-process
OpenVINO GenAI route in the WinUI application, plus one evidence-gated
experimental TurboQuant TBQ4/TBQ4 CPU-SDPA configuration for the pinned Granite
model.

The terminal product has:

- normal WinUI prompting with no terminal interaction;
- a stable verified official OpenVINO CPU route;
- shared route-neutral prompt lifecycle and protected-process infrastructure
  with GGUF, while preserving format-specific workers and evidence;
- sealed offline Granite Safetensors conversion and standard FP16/INT8/INT4
  optimization;
- optional Intel GPU exposure only when exact physical GPU-01 evidence exists;
- TurboQuant labeled Experimental, bound to the exact approved model/build and
  matched campaign, with explicit official/GGUF fallback and no silent switch;
- sanitized immutable hosted/UCL evidence, complete cleanup, and controlled
  requirements traceability.

Success is not “tests exist” or “the worker starts.” Success is one clean
immutable candidate for which `scripts/openvino/Invoke-OpenVinoReleaseGate.ps1`
emits exactly `openvino_release_accepted`, all required external records are
genuine, and no unsupported capability is exposed. Until then the correct
result is `openvino_release_blocked`.

## Repository and payload identities

- Branch: `feature/openvino-route`.
- Baseline implementation commit: `c1e0fe2f` (`test(openvino): add fail-closed
  route release gates`).
- The exact continuation/handoff commit is stored in
  `inventory/payloads.json` and verified by the initializer. Treat that full
  lowercase 40-character identity as authoritative for the reconstructed
  checkout.
- Official input archive was created from the verified Stage 16 official
  closure. Its worker-manifest SHA-256 is
  `db46a1c79a6bd2199eb4d9434ba406a1de51a11b060a4108a100542bdf9e39d3`.
- TurboQuant input archive was created from the final Task 16 worker Stage F.
- Converter input archive was created from final Task 13 converter Stage P,
  containing 23,758 files in the transfer input (the earlier measured scan
  described 23,756 payload files before its enclosing manifest accounting).
- `handoff-state.json` is operational reconstruction state only. It is not
  trusted UCL evidence.

Before doing anything else:

```powershell
git status --short
git branch --show-current
git rev-parse HEAD
git log -8 --oneline
```

Require an empty status, `feature/openvino-route`, and the handoff commit from
the payload inventory. Read these files completely:

- `docs/superpowers/plans/2026-08-20-openvino-route.md`
- `docs/superpowers/specs/2026-08-23-openvino-ucl-handoff-bundle-design.md`
- `docs/superpowers/plans/2026-08-23-openvino-ucl-handoff-bundle.md`
- `.superpowers/sdd/2026-08-20-openvino-route/progress.md`
- `.superpowers/sdd/2026-08-20-openvino-route/task-15-report.md`
- `.superpowers/sdd/2026-08-20-openvino-route/task-16-report.md`
- `.superpowers/sdd/2026-08-20-openvino-route/task-17-report.md`
- `.superpowers/sdd/2026-08-20-openvino-route/task-18-report.md`
- `docs/evidence/openvino/README.md`
- `scripts/openvino/Invoke-OpenVinoReleaseGate.ps1`
- `.github/workflows/openvino-ucl-intel.yml`
- `.github/workflows/openvino-turboquant-ucl.yml`

## End product and non-negotiable architecture

Preserve all of these boundaries:

1. WinUI owns presentation and route-neutral prompt lifecycle, not native
   loading, conversion, or worker protocol implementation.
2. Official, converter, and TurboQuant closures have separate roots and exact
   manifests. Do not merge them or rely on ambient DLL/Python resolution.
3. Worker startup stays protected, handle-first, environment-closed,
   process-tree-contained, manifest-bound, and path-private.
4. Selected packages remain snapshot/lease bound. Do not weaken retained
   handles, final-handle identity, topology, mutation, ADS, or module closure.
5. STOP, CANCEL, completion, failure, and disposal share exact session/turn
   ownership. Do not reintroduce cross-turn races or fire-and-forget teardown.
6. Requested and actual device identity must agree. CPU fallback cannot satisfy
   an explicit GPU request.
7. Persistent FP16/INT8/INT4 artifacts, runtime KV-cache policy, and compiled
   cache are distinct concepts and evidence. Never relabel one as another.
8. TurboQuant uses protocol `openvino.turboquant/1`, route
   `openvino.turboquant`, configuration `openvino.turboquant.cpu.tbq4`, CPU,
   TBQ4/TBQ4, and Experimental maturity.
9. TurboQuant activation must originate from a valid single-use package lease
   and exact approved campaign/build tuple. Synthetic fixtures cannot activate
   it.
10. Official OpenVINO remains stable and independently usable if the entire
    experimental closure is absent.
11. Fallback is inert until explicit user confirmation, tries verified official
    OpenVINO first and applicable verified GGUF second, and never retains
    TurboQuant branding after selection.
12. Central registration, shared prompt-page exposure, and application
    TurboQuant packaging stay absent until every mandatory activation and
    governance gate closes.

## Full Task 1–18 execution history

The master plan is authoritative. This summary explains why the current state
looks the way it does.

- Tasks 1–6 established contracts, dependency closure, strict package
  inspection, route handoff, protocol rules, and initial native feasibility.
  The inspection path became bounded and mutation-resistant; transport remained
  typed and path-private.
- Task 7 implemented the real official worker and corrected the previously
  unpublished protocol atomically so arbitrary selected package identity,
  native/runtime evidence, progress, close-session, and authoritative turn
  completion are carried on the protected channel. Protocol remains
  `openvino.official/1` because there was no released compatibility consumer.
- Task 8 integrated the stable official route with WinUI, exact STOP/CANCEL and
  navigation cleanup. Five review rounds exposed terminal-operation races. The
  final residual was deliberately carried into Task 9 rather than hidden.
- Task 9 first fixed terminal arbitration so STOP, exact-ID CANCEL, completion,
  and failure share one async exact-turn gate. It added hosted and manual-only
  UCL evidence workflows, route-neutral `cpu.json`, strict JSON/privacy,
  external run-record binding, and one retained trusted-root campaign owner.
  No workflow was dispatched locally.
- Task 10 added explicit CPU/GPU device grammar and physical GPU-01 gating. This
  development host had no eligible Intel GPU; one physical test remained
  explicitly skipped and GPU stayed hidden.
- Task 11 completed dense Granite source inspection and an offline Windows x64
  converter closure pinned to `optimum==2.3.0` and the locked dependency set.
- Task 12 implemented sealed offline conversion with source preservation,
  official CPU smoke, atomic publication, provenance, and shared UI action.
- Task 13 added independently published FP16/INT8/INT4 standard optimization
  candidates and evidence separation.
- Task 14 added the stable official-route acceptance filter and hosted/UCL
  integration without dispatching external workflows. Commit: `9cb832c2`.
- Task 15 audited and recovered the exact released TurboQuant upstream, kept an
  empty patch ledger where no patch was justified, sealed the MSVC x64 runtime
  closure, and proved codec/stateful two-turn CPU-SDPA behavior. Commit:
  `1f57e352`.
- Task 16 created the distinct TurboQuant worker, protocol, manifest, typed
  build/activation evidence, forced-negative gates, and manual UCL workflow.
  Commit: `64d82726`. Real pinned-Granite matched evidence and external reviews
  intentionally remained open.
- Task 17 added the gated Experimental adapter, exact tuple/campaign policy,
  bounded evidence, and explicit single-use fallback. Commit: `ff13289c`.
  Production construction is lease-only. Central registration and packaging
  remain absent.
- Task 18 added the exact ordered release gate, architecture/packaging and
  accessibility contracts, recursive artifact privacy scanner, exact-owned-root
  cleanup scanner, cleanup-ledger reconciliation, and evidence catalogue.
  Commit: `c1e0fe2f`. With external evidence absent it returns
  `openvino_release_blocked`, exit 1.

## Exact completed verification evidence

Local evidence is useful regression information but is not trusted UCL
evidence. At the Task 18 implementation boundary:

- OpenVINO contracts: 187/187 passed.
- Model Inspection contracts: 357/357 passed.
- Model Inspection cleanup inventory: 3/3 passed at 638/638.
- OpenVINO application unit suite: 272/272 passed.
- OpenVINO WorkerClient: 13/13 passed.
- Model Inspection WorkerClient: 119/119 passed.
- Model Inspection process integration: 32/32 passed.
- OpenVINO process integration on final converter Stage P: 61 passed, zero
  failed, one explicit physical GPU skip, 62 total.
- Correct-stage converter focus: 5 applicable tests passed.
- Packaged Release/x64 affected WinUI slice: 580/580 passed.
- Packaged canonical official route E2E: 1/1 passed.
- Release/x64 app build passed with `worker_manifest_valid` and
  `worker_manifest_digest_valid`.
- The no-evidence release gate returned exactly `openvino_release_blocked`.

Re-run relevant suites on the laptop rather than assuming transfer preserves
hardware conclusions.

## Problems encountered and their resolutions

Preserve these lessons:

1. **Stale converter stage:** Stage L predated the optimization protocol and
   produced protocol rejections. The correct converter is Stage P. Do not
   diagnose Stage L failures as current product defects.
2. **Large converter cold scan:** the converter closure contains roughly 23.7k
   files. A legitimate isolation run exceeded the old five-minute MSTest
   timeout during a cold scan. Only the test timeout was raised to ten minutes;
   production timeouts/security bounds were not weakened.
3. **Packaged UI stale deployment/assertion:** an initially empty execution
   evidence field was caused by an old deployed package. Rebuilding exposed the
   real issue: the E2E expected an obsolete worker manifest digest. The test now
   derives the displayed digest from the packaged manifest already pinned by
   build/runtime verification.
4. **Solution-level RID:** `dotnet build` rejects a solution-wide
   `RuntimeIdentifier`, while ReadyToRun Release publishing needs `win-x64`.
   The release-package gate now builds the application project directly with
   `--runtime win-x64`; do not revert it to the `.slnx` invocation.
5. **ZIP metadata trust:** merely bounding a ZIP entry’s declared length was
   insufficient. Actual decompression reads are now byte-bounded and checked
   against the declared entry length.
6. **Cleanup scope:** machine-wide temp is not operation-owned. The cleanup
   scanner now requires an explicit operation root and receives exact closure
   roots separately.
7. **Long gate mutation window:** candidate commit/clean status is checked both
   before and after the ordered gate. Do not remove the final revalidation.
8. **Shared stale oracles:** the Model Inspection architecture allowlist now
   contains exactly the reviewed OpenVINO conversion and optimization pipeline
   consumers. The visual-source parser recognizes only
   `@(_OpenVinoOfficialWorkerFile)` in the reviewed packaging target. Do not
   broaden either exception.
9. **Mirrored Stage B:** a temporary copied official stage was used only to
   prove distinct-root behavior. It was not an independent build and is not an
   acceptance input.
10. **Encoding/output:** console rendering occasionally displayed UTF-8
    punctuation as mojibake, but tracked source remained UTF-8. Do not rewrite
    entire documents merely because a legacy console renders punctuation
    incorrectly.

## Security and correctness rulings to preserve

- The official and TurboQuant protocols are intentionally distinct.
- Worker/runtime evidence is caller-known and manifest-bound, not worker
  self-assertion alone.
- Package and runtime namespaces are continuously monitored across success and
  exceptional paths.
- System modules are accepted only through final-handle System32/WinSxS
  classification; ambient/user DLL paths are forbidden.
- UCL evidence is route-neutral. A separate external run record supplies the
  GitHub/protected-environment trust binding; local JSON cannot self-declare UCL.
- Trusted-root leases are acquired before consumers and retained through all
  native, managed, measurement, and post-integrity work.
- Evidence schemas are closed, duplicate-key safe, bounded, and path-private.
- Artifact cleanup and privacy execute even on failure in the workflows.
- A skipped physical GPU test is inconclusive, not passed.
- Security/license booleans must come from authorized external decisions, not
  be typed into a locally authored campaign file to make the gate green.

## Prohibited shortcuts and false-completion traps

Do not:

- edit evidence to say `passed`, copy local samples into UCL roots, or fabricate
  GitHub run metadata;
- use the synthetic TinySyntheticV1 fixture as pinned-Granite acceptance;
- expose GPU based on enumeration alone or accept CPU fallback for a GPU request;
- silently select official/GGUF after TurboQuant failure;
- register/package TurboQuant before matched campaign and approvals close;
- weaken manifest, DLL search, retained-handle, mutation, environment, process,
  timeout, cleanup, privacy, or final-candidate checks to make a test pass;
- leak prompt/generated text, model/tokenizer bytes, raw stdout/stderr, local
  paths, identity, environment, or credentials into retained artifacts;
- manually modify generated RTM derivatives instead of using the controlled
  workbook/regeneration owner;
- claim hosted/UCL acceptance from workflow syntax tests, a queued run, local
  execution, transferred closures, or sanitized sample documents;
- use destructive Git cleanup/reset commands against user work;
- treat the transferred converter/worker archives as independent builds.

## Laptop discovery and prerequisite audit

Perform read-only discovery first:

```powershell
git status --short
git rev-parse HEAD
dotnet --info
Get-CimInstance Win32_Processor | Select-Object Manufacturer,Name
Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion
Get-PSDrive -PSProvider FileSystem | Select-Object Name,Free
```

Confirm the expected self-hosted runner labels when workflow execution is in
scope:

```text
self-hosted, Windows, X64, workbook05, intel-target
```

Do not record the processor model, hostname, username, serial numbers, absolute
paths, or runner identity in sanitized evidence. The admitted UCL vendor is the
coarse value `Intel`.

Verify transferred closures again using the reconstructed relative paths from
the initializer:

```powershell
powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
  -File scripts/openvino/Test-OpenVinoOfficialWorkerManifest.ps1 `
  -StageDirectory ..\closures\official
powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
  -File scripts/openvino/Test-OpenVinoTurboQuantWorkerManifest.ps1 `
  -StageDirectory ..\closures\turboquant
powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
  -File scripts/openvino/Test-OpenVinoConverterWorkerManifest.ps1 `
  -StageDirectory ..\closures\converter
```

Adjust only the relative traversal if the repository/destination layout differs;
do not merge roots.

## Authorized pinned-Granite acquisition and identity binding

The model is deliberately absent from the transfer bundle. Obtain it only from
the project-approved source and under the applicable license/authorization.
Use the exact model identity required by the Task 15–18 contracts and workflows.

Before any campaign:

1. Place the model in a dedicated trusted input root outside repository,
   evidence, build, and operation roots.
2. Reject reparse roots, ADS, unexpected files, incomplete download state, and
   writable aliasing with output roots.
3. Compute the exact package manifest digest, model SHA-256, and model length.
4. Compare them with authorized configuration values. Do not infer or normalize
   a mismatch.
5. Acquire/retain the trusted-root lease through the entire campaign owner.
6. Never copy model/tokenizer bytes into evidence or test results.

If the authorized model ID, digest, length, or license is unavailable, stop the
acceptance path and report that exact external blocker.

## Official CPU trusted-UCL campaign

The primary workflow is `.github/workflows/openvino-ucl-intel.yml`. It is
manual-only and requires protected-environment authorization `UCL-01-approved`
plus dispatch input `UCL-01`. Do not dispatch it without the user/owner’s
explicit authority.

The campaign owner is `scripts/openvino/Invoke-OpenVinoUclCampaign.ps1`. Preserve
its single retained-input ownership. The CPU path must demonstrate:

- exact clean candidate commit and controlled fixture/model identity;
- official worker manifest and dependency closure;
- real CPU requested/actual equality;
- deterministic one-turn MVP and at least two sequential ChatHistory turns;
- typed startup/build/native/device evidence;
- streaming and authoritative token completion;
- STOP and same-session reuse;
- active CANCEL with no late output/completion;
- reset/close, timeout, app close, and parent-exit process-tree cleanup;
- model/package integrity unchanged after execution;
- no listeners, residue, locks, pipes, or compiled-cache ownership escape;
- sanitized `cpu.json` and externally bound workflow/run metadata.

The hosted workflow `.github/workflows/openvino-official-ci.yml` must also
complete on the exact same candidate commit and produce the admitted hosted CPU
artifact. A local hosted-shaped run is regression evidence only.

## TurboQuant matched campaign

The TurboQuant workflow is `.github/workflows/openvino-turboquant-ucl.yml` and
requires its protected authorization values. Use the exact pinned Granite model
and transferred/rebuilt verified TurboQuant closure. The campaign must compare
TurboQuant against the matched verified official baseline on the same candidate,
model identity, CPU class, prompt/rubric, context, and measurement policy.

The final closed campaign evidence consumed by the release gate requires:

- schema version 1 and exact candidate/model/package identities;
- requested `CPU` and exactly one actual execution device `CPU`;
- external `securityReviewApproved` and `licenseReviewApproved` dispositions;
- quality rubric `GTQ-QUALITY-RUBRIC-v1`;
- matched official baseline;
- deterministic smoke;
- actual TBQ4/TBQ4 activation and positive model SDPA node count;
- memory reduction, quality, and performance passing the approved thresholds;
- repeatability and context scaling;
- cancellation and cleanup;
- corruption/forced-negative rejection;
- streaming;
- at least two completed sequential turns.

Confirm activation through
`scripts/openvino/Test-OpenVinoTurboQuantActivation.ps1`. Inspect the worker’s
typed evidence rather than inferring activation from filenames or labels.

If a metric fails, retain the valid failure disposition, diagnose with focused
tests, and make a code change only when evidence shows an implementation defect.
Do not adjust the rubric or threshold after observing results.

## Normal WinUI and accessibility acceptance

Hardware CLI success alone is insufficient. Exercise the packaged Release/x64
application through the real Model Import → Model Inspection → Prompt journey.
Require:

- no terminal interaction;
- visible requested/actual device and exact build evidence;
- shared prompt template, keyboard access, focus visuals, accessible names, and
  bounded polite announcements without token spam;
- completed prompt, STOP/reuse, active CANCEL/no-late-output, reset, navigation,
  app close, and process cleanup;
- Experimental TurboQuant labeling and exact bounded evidence rows;
- explicit fallback confirmation with stable official first and applicable GGUF
  second;
- no silent backend/cache/device switch;
- stable official route when experimental closure is removed.

Use the established AppContainer path for packaged tests:

1. Build the WinUI unit project with Visual Studio MSBuild, `Release`, `x64`,
   `RuntimeIdentifier=win-x64`, and the official stage/digest properties.
2. Run the generated `.build.appxrecipe` with Visual Studio
   `vstest.console.exe`; direct `dotnet test` is not authoritative for this
   legacy packaged WinUI project under the repository test platform.
3. Start with the focused OpenVINO E2E, then run the exact affected filter:
   `FullyQualifiedName~ModelImport|FullyQualifiedName~ModelInspection|FullyQualifiedName~OpenVino`.

## Optional physical Intel GPU / GPU-01 path

GPU is optional for release unless product scope explicitly requires exposing
it. Run this path only if the laptop has an eligible Intel adapter and the owner
provides `GPU-01`, exact `GPU` or `GPU.N` device identity, approved adapter name,
and driver version through the protected workflow configuration.

Require native enumeration, compile probe, real execution, requested/actual
equality, negative unavailable/mismatch cases, cancellation, cleanup, plugin and
runtime identity, and no CPU fallback. If the hardware or authorization is not
present, keep GPU hidden and report GPU-01 as inconclusive/open—not failed and
not passed.

## Artifact privacy and terminal cleanup

Use dedicated, initially empty hosted evidence, UCL evidence, and operation
roots. Do not reuse source, closure, build, model, or machine temp roots.

Run `scripts/openvino/Test-OpenVinoArtifactPrivacy.ps1` over every retained
JSON/TRX/typed log/text/ZIP artifact. It must reject unknown extensions,
duplicate JSON names, DTDs, traversal, nested archives, size bombs, reparse
points, absolute paths, identity, environment, prompts/output, model/tokenizer
bytes, credentials, and raw streams.

Run `scripts/openvino/Test-OpenVinoCleanupInventory.ps1` with the exact operation
root and official/converter/TurboQuant roots. It must find zero owned worker,
converter, TurboQuant, Python, or fixture process descendants; zero residue,
locks, or named pipes; and no late events after terminal paths.

Never point cleanup at the machine-wide temporary directory or delete broadly.
Cleanup only exact operation-owned roots after resolving and validating them.

## External security/license records

The Task 15 audit and local closure are not a substitute for the external
decisions required by activation policy. Obtain the authorized security review
and license review records for the exact audited upstream/source build and
packaged closure. Bind them to the exact candidate and package identity through
the approved evidence workflow.

Do not create an approval document yourself, set approval booleans based on
local judgment, or proceed if the external record is ambiguous or covers a
different commit/build.

## Controlled RTM regeneration and traceability

The evidence catalogue at `docs/evidence/openvino/README.md` lists the scoped
identities. I0/the controlled workbook owner must update and regenerate direct
evidence for:

- all 398 P1 atoms;
- `MI-SEAM-001..028`;
- applicable P2/P3 findings;
- `F-M18`, `F-M20`, `F-M21`, `F-M22`;
- `N-M02`, `N-M11`;
- `DR-WF-008`, `DR-WF-010`, `DR-WF-011`, `DR-WF-013`, `DR-WF-014`, and
  `DR-WF-015`.

Each requirement must link to concrete test/evidence identity and the immutable
candidate. Do not manually edit generated views. If you lack workbook ownership
or the regeneration tool, report the exact I0 blocker after completing all
hardware work.

## Ordered final release gate

The release gate requires these environment variables, all pointing to exact
separate roots/files and one candidate:

```text
GRANITE_OPENVINO_OFFICIAL_WORKER_STAGE
GRANITE_OPENVINO_OFFICIAL_WORKER_MANIFEST_SHA256
GRANITE_OPENVINO_TURBOQUANT_WORKER_STAGE
GRANITE_OPENVINO_CONVERTER_STAGE
GRANITE_OPENVINO_HOSTED_EVIDENCE_ROOT
GRANITE_OPENVINO_UCL_EVIDENCE_ROOT
GRANITE_OPENVINO_OPERATION_ROOT
GRANITE_OPENVINO_EVIDENCE_COMMIT
GRANITE_OPENVINO_MODEL_SHA256
GRANITE_OPENVINO_MODEL_LENGTH
GRANITE_OPENVINO_TURBOQUANT_PACKAGE_MANIFEST_SHA256
GRANITE_OPENVINO_HOSTED_CPU_EVIDENCE_PATH
GRANITE_OPENVINO_UCL_CPU_EVIDENCE_PATH
GRANITE_OPENVINO_UCL_TURBOQUANT_EVIDENCE_PATH
GRANITE_OPENVINO_TURBOQUANT_CAMPAIGN_EVIDENCE_PATH
```

Before setting them, require `git status --short` to be empty and all evidence
to bind `git rev-parse HEAD`. The gate order is fixed:

1. dependency/license locks;
2. contracts;
3. static/malformed inspection;
4. official native;
5. converter isolation/transaction;
6. optimization;
7. TurboQuant conformance/activation;
8. app unit/visual/accessibility;
9. GGUF regression;
10. Release x64 package;
11. hosted evidence;
12. trusted UCL evidence;
13. artifact privacy;
14. cleanup inventory;
15. traceability.

Invoke:

```powershell
powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
  -File scripts/openvino/Invoke-OpenVinoReleaseGate.ps1
```

Only exact output `openvino_release_accepted` with exit 0 closes the gate. The
script revalidates commit and tracked-clean state after all stages.

## Failure-handling protocol

For any failure:

1. Record the exact command, exit code, typed disposition, test count, and
   candidate commit without retaining sensitive raw output.
2. Determine whether it is environment/authorization, transferred-input
   corruption, hardware incompatibility, test defect, or product defect.
3. Use systematic debugging. Reproduce with the smallest safe focused test.
4. For product/test defects, write a failing regression first, observe RED,
   implement the narrow fix, observe GREEN, then rerun the affected and full
   gates proportionate to risk.
5. Preserve user changes and avoid destructive Git/file cleanup.
6. If the same external blocker persists, do not mutate acceptance rules. Mark
   it explicitly open and continue other independent safe work.
7. Never expose TurboQuant or GPU merely because most gates passed.

## Required final report and commits

Keep commits narrow and evidence truthful. Before each completion claim run
`git diff --check`, review the full diff, and verify the exact tests again.

The final report must include:

- exact candidate commit and clean status;
- laptop hardware disposition using sanitized allowed identity;
- official CPU results and two-turn stateful proof;
- TurboQuant exact activation, TBQ4/TBQ4, SDPA, matched memory/quality/
  performance, cancellation, corruption, streaming, and cleanup results;
- normal WinUI/accessibility/package results;
- hosted and UCL workflow/run binding identities and artifact hashes without
  leaking runner/user/path information;
- optional GPU-01 disposition;
- security/license record identities;
- privacy and cleanup results;
- RTM regeneration result;
- final ordered gate output;
- every remaining open blocker, if any.

If all mandatory gates genuinely pass, enable only the plan-authorized central
TurboQuant registration/packaging and rerun the complete release gate on the
new exact commit; evidence for the pre-activation commit cannot automatically
authorize a changed activation commit. Follow the repository’s candidate
binding process so final evidence and code converge on one immutable commit.

If any mandatory input remains unavailable, leave central registration and
packaging absent and finish with `openvino_release_blocked`. That is a correct,
safe result—not an incomplete implementation claim.
