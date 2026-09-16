# OpenVINO chat worker compatibility

The shared chat route sends `initialHistory` when reopening a conversation and
`isTransientTitle` for automatic titles. Older TurboQuant workers reject these
fields even when their file manifests are valid. A fresh, empty chat can still
work with those workers, hiding the mismatch until a conversation is reopened.

Both the worker build and application packaging now run
`scripts/openvino/Test-OpenVinoTurboQuantChatProtocol.ps1`. It verifies the worker
manifest and checks history parsing through the real executable. An invalid
device stops the probe before model loading. An unsupported history role must
still be rejected. This check is not evidence of successful inference.

The native regression test
`SealedWorkerReopensHistoryAndContinuesAfterTransientTitle` separately checks
TBQ3 and TBQ4 using the protected worker client and a supplied real model. Each
case opens two sessions with previous messages, generates an automatic title,
then generates a normal response and closes the session. Activation evidence
must match the requested cache format.

## Build inputs

Use the chat-compatible worker stage for `OpenVinoTurboQuantWorkerStageDirectory`
and its exact lowercase manifest hash for `OpenVinoTurboQuantWorkerManifestSha256`.
Do not bypass either manifest verification or the new protocol check.

Rebuild the application with that hash; replacing only the worker files leaves
the old hash in the application assembly and disables the TurboQuant route.
`PackagedApplicationBindsItsTurboQuantWorkerManifest` checks that the compiled
application and its packaged worker agree. Set `GRANITE_APP_LAYOUT` to the
complete app folder when running this test.

The existing verified chat-compatible stage has worker-manifest SHA-256:

```text
39a4eccc05d4677b59f7f882cc50f875591ee3d3f90f1037e61e17fe3a34c35e
```

This identity is already admitted by the application. The worker source already
includes history and title support; the repair selects the matching worker
artifact and prevents packaging a legacy artifact. Existing runtime identities,
optimisation evidence, model files and integrity requirements are unchanged.

For the native test, provide `OPENVINO_TURBOQUANT_WORKER_STAGE`,
`OPENVINO_TURBOQUANT_PACKAGE_ROOT`, `OPENVINO_TURBOQUANT_PACKAGE_MANIFEST_SHA256`,
`OPENVINO_TURBOQUANT_MODEL_SHA256` and `OPENVINO_TURBOQUANT_MODEL_LENGTH`.
Run the integration project with the test-name filter above. The tiny synthetic
fixture cannot cover this real conversation test.

## Local app check

On 16 September 2026, the rebuilt, separately registered test app passed the
app/worker manifest-binding test. The raw Granite 4.1 3B OpenVINO package passed
inspection and hardware evaluation. The configuration slider showed TurboQuant
TBQ3 at preference 0 and TBQ4 at preference 25, with Start optimisation enabled
for both. This checks selection availability, not a new end-to-end optimisation
run. The previous test window, main branch and shared ZIP were left unchanged.
