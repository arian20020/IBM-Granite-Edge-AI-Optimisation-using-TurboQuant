# LLM Fit Gate 1 trusted integration tests

This Microsoft Testing Platform project contains four manual operational gates
for the integrity-pinned LLM Fit v1.1.9 Windows x64 candidate and three local
deterministic boundary checks. The operational gates are separate from the
main deterministic suite because three require the named Windows Intel target
and one requires a controlled offline run.

The tests are black-box gates. They do not reference internal spike runner
types and they do not rerun the candidate during trusted-capture validation.

## Categories and prerequisites

`TrustedWindowsIntel` contains exactly:

- `TrustedCandidate_IdentityVersionAndCpuRamSchemaPass`
- `TrustedCandidate_CpuAndRamAgreeWithNearSimultaneousWindowsReference`
- `TrustedCandidate_LeavesNoDashboardListenerOrProcess`

It requires all three variables:

```text
GRANITE_LLMFIT_CANDIDATE_ROOT
GRANITE_LLMFIT_WINDOWS_REFERENCE
GRANITE_LLMFIT_GATE1_OUTPUT
```

`TrustedOffline` contains exactly:

- `OfflineCandidate_ProducesCpuRamWithoutAnyListenerOrResidualProcess`

It requires:

```text
GRANITE_LLMFIT_CANDIDATE_ROOT
GRANITE_LLMFIT_OFFLINE_OUTPUT
```

`Task8Deterministic` contains three environment-independent guards for
privacy-safe artifact strings, the exact 30-second boundary, and stable file
identity across a temporary hard-link alias. Both link names and the temporary
directory are removed before the test reports its result; no candidate is
executed and no global drive mapping is changed.

A missing variable produces `Assert.Inconclusive` with the missing variable
names. Once every variable for a category is present, an invalid path,
candidate, artifact, comparison or environment is a failure and is never
converted to a skip. `--minimum-expected-tests` checks discovery only, so the
operator must also confirm 3/3 or 1/1 passed and zero skipped in the console and
TRX.

## Trusted capture contract

The three trusted tests read the already completed output created by
`Capture-HardwareInspectionWindowsReference.ps1`. They require the reference
variable to identify exactly `windows-reference.json` inside the configured
Gate 1 output. That output must contain exactly:

```text
llmfit-gate1.evidence.json
llmfit-system.raw.json
windows-reference.json
```

The tests independently enforce:

- the full pinned manifest, archive/executable hashes, reported version and
  AMD64 PE identity;
- a fresh candidate verification through the public verifier;
- strict UTF-8 and exact JSON property allowlists;
- raw JSON SHA-256 binding before any raw facts are compared;
- Windows x64 with an Intel CPU and Intel graphics;
- normalized CPU identity, logical processor count and RAM tolerances;
- a capture bracket no longer than 30 seconds;
- valid CPU/RAM schema evidence;
- no candidate-owned socket, dashboard port or residual process;
- Intel GPU identity as either `Matched` or a non-fatal `FieldLevelGap`;
- no dedicated/shared Intel GPU memory claim; and
- Intel NPU state exactly `DetectionUnavailable`.

## Offline contract

The offline test evaluates both network preconditions before it reads,
verifies or launches the candidate. If either precondition fails, it creates a
fresh output and atomically writes only this minimal envelope to
`llmfit-gate1.evidence.json`:

```json
{
  "schemaVersion": "1.0",
  "disposition": "Blocked",
  "diagnosticCodes": ["HI-GATE1-OFFLINE-PRECONDITION-FAILED"]
}
```

That deliberate failure proves only that the environment was online. It does
not fabricate candidate, signature or hardware facts. When both preconditions
pass, the test invokes only the already-built fixed Gate 1 project with the
fixed read-only commands, then requires full production evidence with valid
CPU/RAM and no candidate-owned listener, established connection, port 8787 or
residual process.

Neither the tests nor the capture script change network configuration. Follow
the [Gate 1 runbook](../../docs/testing/runbooks/Hardware-Inspection-LLM-Fit-Gate-1-Runbook.md)
for acquisition, capture, manual network isolation and exact test commands.

## Privacy and non-claims

Candidate and output paths are operator input and never enter retained JSON.
The Windows reference is strictly allowlisted. It can retain the approved
Windows CPU/GPU names and RAM counters needed to audit the comparison, plus
derived booleans, counts, deltas, hashes and fixed diagnostic values. It never
retains the candidate path, repository path, host/user identity, device IDs,
serials, UUIDs, MAC/IP data, raw LLM Fit output or raw-derived LLM Fit names.

These tests do not approve redistribution, resolve the unsigned-binary claim,
complete the transitive dependency-notice inventory, establish Intel GPU
memory semantics, detect an Intel NPU, or implement production Hardware
Inspection. Those decisions remain explicit later gates.
