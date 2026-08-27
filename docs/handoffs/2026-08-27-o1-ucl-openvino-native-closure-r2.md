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
| Official worker B | `db46a1c79a6bd2199eb4d9434ba406a1de51a11b060a4108a100542bdf9e39d3` | `worker_manifest_valid` |
| TurboQuant worker | `d4748d69ecacf13b1e1d6756f86df79d3a43e361bc10a0676ca270c9d08b9492` | `turboquant_worker_manifest_valid` |
| TurboQuant runtime closure | `3b26a537ddfedad6d2f75fd1bf4578703a314f2544f4a81a6acfcade8d330c85` | checked through TurboQuant worker validator |
| Controlled fixture | `dc5ef5060a0e8242291d98701ab2b65286169c863a12e573dd1ad7921ac92648` | `fixture_valid` |

All detailed validator output, process diagnostics, and TRX files are held in the
operation-owned coordination evidence directory, not in Git.

## Managed readiness

Commands used SDK `10.0.301` and `-p:UseAppHost=false`:

| Suite | Discovered | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| OpenVINO contracts, Release | 204 | 204 | 0 | 0 |
| OpenVINO unit, Release | 405 | 400 | 0 | 5 |
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
| TurboQuant activation | Evidence produced | The stage-backed activation receipt was emitted; it is not a claim of TurboQuant optimisation or end-user model acceptance. |

The failure occurs after closed-manifest admission and before a usable native
worker session. The available evidence therefore supports an external native
protected-boundary/closure blocker. The focused conversion result is consistent
with that boundary but does not expose an underlying converter diagnostic, so it
is not evidence for a route-local source repair. Native policy event channels
could not be queried from this terminal, so this handoff does not attribute the
blocker specifically to Windows Application Control or claim an executable/DLL
policy event.

## Product-journey nonclaims and C0 request

O1 did not register or launch the desktop application, build a shared package,
or use an unapproved real model. The C0 stage publication contained no
C0-registered application output, so picker/drag-and-drop, shared inspection,
Hardware Inspection, compatibility presentation, preference UI, seven-stage
animation, Chat keyboard behaviour, and export were not exercised here. The
controlled fixture is only native component evidence and is not final model or
product acceptance.

C0 action requested: diagnose the host protected-boundary native worker failure
with the platform owner, then supply/confirm its registered shared application
output for C0-owned end-to-end validation. No shared-code correction is proposed
by O1.

## Cleanup and final checks

A duplicate later O1 test tree was detected while the canonical diagnostic/TRX
run was active; it was stopped at coordinator direction without touching the
canonical tree. The canonical run completed. Final process inspection found no
O1 worker-process test host, official worker, TurboQuant worker, or converter
process. No application process was launched by O1.

Before this handoff commit, the source worktree was clean and `git diff --check`
was empty. The committed handoff is followed by a repeat `git diff --check`,
clean-status check, and remote-tip comparison before publishing native completion.
