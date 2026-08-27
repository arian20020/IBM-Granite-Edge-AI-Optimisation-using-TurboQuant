# O1 OpenVINO native closure — R2

## Identity and scope

- Branch: `validation/ucl-openvino-native-r2`
- Frozen ancestor: `f599c358181bd4da44087ab0c64d36d02d23316a`
- Candidate/base and tested source commit: `b586cb3855514bdb058ad47b5d3d08475170942e`
- Tested source tree: `92a3863e29cc6f7a1d9314b822f2ea572ab4bb1c`
- Source changes: none. This handoff is the sole R2 O1 commit.

The branch was created directly from the stipulated O1 candidate, not by merging a
moving branch. The only changed path in the resulting handoff commit is this
document. No shared UI, navigation, registration, package project, or C0-owned
path was modified.

## Stage admission

All supplied stages were independently rehashed and passed their repository
validators before native execution:

| Closure | Manifest SHA-256 | Validator result |
|---|---|---|
| Converter | `b975313fccb90250cdf3ba58416b1ea019a93c7889c34a5b425df54df7056983` | `converter_manifest_valid` |
| Official worker A | `db46a1c79a6bd2199eb4d9434ba406a1de51a11b060a4108a100542bdf9e39d3` | `worker_manifest_valid` |
| Official worker B (distinct verified root) | `db46a1c79a6bd2199eb4d9434ba406a1de51a11b060a4108a100542bdf9e39d3` | `worker_manifest_valid` |
| TurboQuant worker | `d4748d69ecacf13b1e1d6756f86df79d3a43e361bc10a0676ca270c9d08b9492` | `turboquant_worker_manifest_valid` |
| TurboQuant runtime closure | `3b26a537ddfedad6d2f75fd1bf4578703a314f2544f4a81a6acfcade8d330c85` | checked through TurboQuant worker validator |
| Controlled fixture | `dc5ef5060a0e8242291d98701ab2b65286169c863a12e573dd1ad7921ac92648` | `fixture_valid` |

All detailed validator output, process diagnostics, and TRX files are held in the
operation-owned coordination evidence directory, not in Git.

The focused A/B test proved distinct root paths and passed both manifest
validators before reaching native launch. The available R2 coordination record
published only one official-worker directory, however, and the focused evidence
does not independently bind B to a second build invocation. This handoff
therefore does not promote the distinct-root result to independent-build
acceptance or rely on a mirrored copy as acceptance evidence.

## Managed readiness

Commands used SDK `10.0.301` and `-p:UseAppHost=false`:

| Suite | Discovered | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| OpenVINO contracts, Release | 204 | 204 | 0 | 0 |
| OpenVINO unit, Release | 405 | 400 | 0 | 5 |
| OpenVINO unit, Release with admitted official stage | 405 | 405 | 0 | 0 |
| OpenVINO worker client, Release | 13 | 13 | 0 | 0 |
| OpenVINO worker-process integration, Release before stages | 69 | 52 | 2 | 15 |

The five unit skips and the 15 integration skips were stage-dependent and were
run before C0 supplied staged closures. The two integration failures in that
pre-stage baseline required the TurboQuant worker stage. They are recorded as
baseline blockers, not native success.

## Authorized native results

With `NATIVE-OWNER` assigned to `O1`, the full worker-process native suite ran
against the admitted converter, official A, TurboQuant, and controlled fixture
closures. Its completed TRX recorded 69 discovered, 59 passed, 8 failed, and 2
not executed; its environment omitted the independently admitted official B
input. The focused independent-A/B closure test then ran with both
verified official closure inputs and recorded 1 discovered, 0 passed, 1 failed,
and 0 skipped.

| Native boundary | Outcome | Evidence-bound result |
|---|---|---|
| A/B fixture inspection and generation | Failed | Focused test reached `StartSessionAsync` then received bounded `OpenVinoWorkerClientException`: worker could not run inside the protected boundary. |
| CPU KV-cache, cancellation, termination | Failed | Worker start either exceeded its approved time limit or returned the same protected-boundary failure. |
| Converter-backed conversion and persistent validation | Failed | Terminal status was `conversion_failed` after preflight/conversion/output-validation stages. |
| Persistent optimisation and projected C1 V2 pipeline | Failed | Terminal optimisation/execution status was failed rather than published/persistent success. |
| Explicit unavailable GPU | Failed assertion | Expected `RuntimeDeviceUnavailable`, observed `RuntimeIntegrityFailed`; no CPU fallback success is claimed. |
| Physical GPU execution | Not executed | Approved GPU device input was not supplied. |
| Original canonical full-run B-closure row | Not executed | The canonical invocation omitted the B environment input. This was closed by the subsequent one-test A/B run above, which reached the protected boundary. |
| Focused converter-backed persistent conversion | Failed | One discovered test returned bounded `conversion_failed` after `Preflight`, `Converting`, `ValidatingOutput`, and `SmokeTesting`; it did not publish output. |
| TurboQuant component execution | Evidence produced | The stage-backed synthetic fixture recorded CPU, TBQ4/TBQ4, SDPA, 2 runtime dispatches, 22 encoded records, two completed turns, and cleanup. Its model SDPA node count was zero, so `Test-OpenVinoTurboQuantActivation.ps1` correctly returned `turboquant_activation_unverified`. This is not TurboQuant activation, optimisation, or end-user model acceptance. |

The failure occurs after closed-manifest admission and a valid
`openvino.official/1` hello, during the CPU `startSession` boundary and before a
usable session. A debugger image-load capture reproduced the failure and showed
the staged GPU plugin loading `intelocl64.dll` from `Program Files (x86)`. The
loaded DLL was Intel-signed with a valid signature, file version `2026.6.0.0.0617`,
length `80290432`, and SHA-256
`e9f7e229ef83ab27b1902629179c5c121e7ba3c699511c13605260123fd51bcf`.
That location is outside the deliberately accepted sealed-stage,
final-handle-System32, and WinSxS roots, so the worker correctly emitted
`runtime_integrity_failed` rather than accepting ambient driver code.

No contemporaneous OpenVINO Code Integrity event was present. This is therefore
not attributed to Windows Application Control: it is an official-worker closure
and device-activation incompatibility on this Intel laptop. The security boundary
must not be broadened to include ambient Program Files paths. The focused
conversion and optimisation failures are downstream consequences of the same
official smoke-test boundary and are not evidence for an O1 route-local source
repair. The relevant native worker sources are also outside the R2 O1 write
allowlist.

## Product-journey nonclaims and C0 request

O1 did not register or launch the desktop application, build a shared package,
or use an unapproved real model. The C0 stage publication contained no
C0-registered application output, so picker/drag-and-drop, shared inspection,
Hardware Inspection, compatibility presentation, preference UI, seven-stage
animation, Chat keyboard behaviour, and export were not exercised here. The
controlled fixture is only native component evidence and is not final model or
product acceptance.

C0/platform action requested: route the official-worker finding to the worker
owner so CPU activation does not eagerly admit the GPU plugin's ambient Intel OCL
module, while preserving the sealed stage/System32/WinSxS rule. Then
supply/confirm the exact R2 registered shared application output for C0-owned
end-to-end validation. No shared-code correction is proposed by O1.

## Cleanup and final checks

A concurrent O1 execution was detected while the canonical diagnostic/TRX run
was active. C0 advanced the ownership file after consuming the first O1 record;
the later execution ceased native activity as soon as the changed owner was
observed. Final `Test-OpenVinoCleanupInventory.ps1` returned
`openvino_cleanup_valid`: no O1 worker-process test host, official worker,
TurboQuant worker, converter process, named pipe, lock, partial file, or staging
residue remained. All four manifests revalidated with their original hashes.
No application process was launched by O1.

Before this handoff commit, the source worktree was clean and `git diff --check`
was empty. The committed handoff is followed by a repeat `git diff --check`,
clean-status check, and remote-tip comparison before publishing native completion.
