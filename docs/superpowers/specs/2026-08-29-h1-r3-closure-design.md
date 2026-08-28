# H1 R3 Closure Design

## Purpose

Close the H1-owned portion of the mandatory R3 campaign with production-reachable
hardware behavior, reproducible committed verification evidence, and a fresh
native-phase handoff that M1 and Q1 can consume in order.

The authoritative base is the pushed H1 R2 tip
`e000ee4f7b1ecc68cac79d774d47e659c96b661c` with tree
`dc40d2effa2a0363094a069c118fda486b836260`. R3 work occurs only on
`audit/ucl-h1-hardware-remediation-r3` in the existing isolated
`C:\UCL-H1-R2` worktree. The dirty main checkout is never edited, merged, or
pushed.

## R3 Issue Ownership

### Owned

- **R3-019, H1 phase:** produce a fresh, exact H1 handoff and native receipt,
  with the native receipt bound to the exact handoff bytes, so the M1 and Q1
  phases can continue in sequence.

### Supported

- **R3-021:** make H1 command evidence reproducible from committed files. No
  manifest command hash may refer only to an external TRX or temporary native
  summary.
- **R3-022:** rerun H1 package, native, cleanup, and integrity gates and publish
  truthful H1 acceptance evidence. H1 does not claim the combined C0/E1
  integrated-candidate acceptance.

### Not Owned

- R3-001 through R3-018 belong to M1, F1, Q1, S1, C0, or T1 according to the
  defect named in each ledger row.
- R3-016 is GGUF package closure. H1 may execute its boundary test and report
  its result but must not edit GGUF packaging.
- The M1 and Q1 continuations of R3-019 and the integrated-candidate portion of
  R3-022 remain external until those workers consume H1's fresh receipts.

## Existing Production Reachability

No new application production type is planned. The R2 H1 corrections are
already live and remain under regression verification:

| Production API | Definition | Real production caller | Composition/registration | Regression tests |
| --- | --- | --- | --- | --- |
| `HardwareSnapshotIdentityCanonicalizer.Compute` | `Features/HardwareInspection/Domain/HardwareSnapshotIdentity.cs` | `HardwareSnapshot` constructor | Every production snapshot created by `HardwareEvidenceResolver` | `HardwareInspectionContractTests` |
| `SystemMemoryBudgetCalculator.Calculate` | `shared/.../Application/Presentation/SystemMemoryBudget.cs` | `SafetyPolicy.AvailableMemoryBudgetFor` | Compatibility engine's single safety-policy path | `SystemMemoryBudgetCalculatorTests`, `SafetyPolicyTests` |
| `HardwareInspectionComposition.CreateProduction` | `Features/HardwareInspection/Infrastructure/Orchestration/HardwareInspectionComposition.cs` | x64 `OnboardingShellPage` constructor | One `HARDWARE_INSPECTION_X64` production registration | Hardware journey/composition tests |
| `PublishHardwareInspectionLlamaCppProbeForApplication` | `HardwareInspection.LlamaCppProbePackaging.targets` | application MSBuild graph | One application import; the second import is test-only | Hardware packaging and manifest tests |

The R3 production-reachability gate will enumerate these symbols from the
committed tree and prove one application registration/import. If a fresh
behavioral regression exposes an H1 production defect, that defect receives a
separate RED-GREEN correction before evidence publication. Source-text checks
alone never substitute for executable behavior where execution is available.

## Closure Architecture

### 1. Committed closure validator

Create `scripts/audits/Test-H1R3Closure.ps1`. It accepts explicit repository,
manifest, result-ledger, report, handoff, native-receipt, expected base,
expected subject, expected final tip, and expected remote-ref inputs. It does
not infer private machine paths into its output.

The validator will:

1. parse JSON with duplicate-key rejection and bounded file sizes;
2. enforce `executed = passed + failed + skipped` and
   `discovered >= executed` for every command and aggregate;
3. require every `resultSha256` to match a named committed result-ledger blob;
4. verify report, manifest, and ledger byte counts and SHA-256 values;
5. verify each file's Git blob bytes equal the worktree bytes;
6. verify base ancestry, evidence-subject commit/tree, final tip/tree, pushed
   remote ref, and clean worktree;
7. reject an R2-or-earlier receipt by requiring worker `H1`, campaign `R3`,
   and the current final tip;
8. verify the native receipt's handoff SHA-256 against the exact published
   handoff bytes and require cleanup confirmation;
9. emit only stable pass/fail codes, never usernames, hostnames, paths,
   filenames from provider output, or raw native data.

The validator is audit tooling, not application composition. It introduces no
application production type or registration.

### 2. Behavioral regression tests

Create `tests/PowerShell/H1R3Closure.Tests.ps1`.

The primary regression test runs the real validator against the R2 H1 evidence
shape and demonstrates RED because its command hashes identify external TRX or
temporary files rather than committed result-ledger blobs. The corrected case
runs the same validator against a committed R3 ledger/index and must pass.

Additional executable cases mutate one input at a time and require fail-closed
results for:

- invalid command arithmetic;
- a missing or mismatched committed result blob;
- stale campaign/receipt data;
- wrong evidence-subject tree;
- a remote tip mismatch;
- a dirty-worktree assertion;
- a native receipt bound to different handoff bytes;
- cleanup not verified;
- duplicate JSON members or oversized JSON.

Fixtures contain only synthetic stable identifiers. Tests invoke validator
behavior and assert exit/result codes; they do not grep implementation text.

### 3. Committed verification ledgers

Fresh command outcomes are reduced to bounded, privacy-safe JSON ledgers under
`docs/audits/2026-08-29/evidence/`:

- `H1-managed-verification-results-v1.json` records test/build/static/package
  rows and hashes of committed inputs without raw TRX content.
- `H1-native-verification-results-v1.json` records the two approved probe
  commands, exit codes, byte counts, timeout bound, pre/post owned-process
  counts, cleanup disposition, and packaged manifest hash. It never stores
  provider JSON.

Each manifest command names one of these committed ledgers and uses that
ledger's exact SHA-256. The closure validator locates the ledger by committed
path and rechecks its bytes, so evidence can be reconstructed from Git rather
than an external results directory.

### 4. Evidence history

Use distinct history phases:

1. **Closure implementation subject:** validator, regression tests, and the
   managed ledger are committed. All managed tests are rerun on this exact
   commit/tree. This becomes `evidenceSubjectCommit` and
   `evidenceSubjectTree`.
2. **Native/evidence publication:** acquire the native slot, run the committed
   subject's packaged probe, verify cleanup, then commit the sanitized native
   ledger, R3 report, and R3 evidence manifest. The final tip/tree therefore
   remain distinct from the evidence subject.

The R3 report is
`docs/audits/2026-08-29/H1-hardware-remediation-r3.md`. The manifest is
`docs/audits/2026-08-29/evidence/H1-hardware-evidence-v2.json`. Required stable
output kinds remain `hardwareSnapshot`, `availableMemory`, and `safetyBudget`.

### 5. External publication

After the final evidence commit is pushed, atomically publish:

- `C:\UCL-AUDIT-HANDOFFS\H1.json`;
- `C:\UCL-AUDIT-NATIVE-RECEIPTS\H1.json`.

Temporary files are written in the same destination directory, fully
validated, and renamed only when the destination does not exist. Existing
destinations are never silently overwritten. The native receipt contains the
SHA-256 of the exact published handoff receipt bytes. Both identify campaign
R3 and the pushed R3 final tip/tree; stale R2 receipts are rejected.

## Verification Matrix

The exact committed subject receives fresh non-zero execution of:

- Hardware Inspection Foundation tests;
- llama.cpp probe/tool/fake-tool tests;
- model/hardware compatibility boundary tests;
- H1 closure-validator Pester tests;
- existing Hardware Inspection packaging/trust Pester tests;
- Debug x64 application and unit-test project builds;
- Debug x64, Release x64, Debug x86, and Debug AnyCPU static packaging
  evaluation;
- production probe manifest, package-content, duplication, audit/reference,
  privacy, and `git diff --check` scans;
- timeout, cancellation, and owned-child cleanup tests already present in the
  Foundation/probe suites;
- the relevant Model Inspection package boundary, reported truthfully if a
  non-H1 defect prevents green.

The native phase uses only the already-built, manifest-verified packaged H1
probe. It atomically acquires the shared lock, confirms zero conflicting
Granite Edge AI processes, runs `identity --format json-v1` and
`capabilities --format json-v1` with a 30-second bound, records only byte
counts and hashes, confirms zero owned descendants, and releases the lock.

## Security and Privacy

The closure preserves exact digest/length checks, bounded reads and output,
cancellation, timeouts, child cleanup, manifest verification, and fail-closed
behavior. It does not download tools/assets, traverse model directories,
alter trust roots, overwrite destinations, suppress cleanup failures, disable
App Control, install certificates, weaken signing/hashes, or change firewall
configuration.

No committed report, manifest, ledger, or receipt contains a username,
hostname, local path, provider filename, raw provider output, credential,
token, prompt, or model data. Synthetic privacy fixtures remain clearly
test-only and are excluded from packages.

## Environment and Space Boundary

Preflight found the required .NET 10.0.301 SDK and .NET 8 runtime, existing H1
test executables/build inputs, no native lock, and a clean isolated H1
worktree. The volume has approximately 6.15 GiB free. Work stays in the
existing worktree to avoid a duplicate checkout; no broad build tree,
unrelated worktree, evidence directory, or user data is deleted to obtain
space. Before each build/native phase, free space and lock/process state are
rechecked. An actual capacity failure is reported precisely rather than worked
around destructively.

## Completion Boundary

H1 may claim its R3 phase only when all owned rows have fresh supporting
evidence, both JSON publications bind exact committed artifacts, the remote
matches the clean final tip, and native cleanup is verified. H1 must still
list these external blockers:

- M1 has not yet consumed the fresh H1 receipt and published its own R3 phase;
- Q1 has not yet consumed the H1/M1 chain;
- C0/E1 integrated package, App Control, native model, cleanup, and end-to-end
  acceptance remain outside the H1 branch.

Those blockers prevent a claim that combined R3 or the user journey is closed;
they do not permit falsifying or weakening an H1 result.
