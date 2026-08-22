# Hardware Inspection Foundation

This project contains candidate-neutral Windows foundations for Hardware Inspection. It is referenced by the x64 app, but it does not activate production hardware collection. Production composition continues to use `UnavailableHardwareInspectionService` until the later provider, policy, and orchestrator gates are complete.

## Boundaries

- `WindowsSystemSnapshotProvider` captures physically installed, OS-usable, and currently available memory as distinct byte values, plus a UTC capture time and bounded OS facts.
- `TrustedToolPackageVerifier` accepts only an exact flat manifest under an approved root. It rejects path escape, reparse points, inventory drift, hash mismatch, and non-AMD64 PE images.
- Verification retains no-follow directory and member handles with deny-write/delete sharing. Hashing uses the retained executable handle, closing the path-inspection/open race.
- `VerifiedTrustedTool` is `IDisposable`. Callers must retain it for every execution and dispose it when the approved package is no longer needed. Execution takes a reference-counted custody lease, so concurrent owner disposal cannot release package custody early.
- `ExternalProcessRunner` accepts only a manifest-declared command identity. It has no shell or arbitrary command-string API.
- The runner creates the process suspended, restricts inherited handles to three standard-stream pipes, assigns and verifies the process in a private kill-on-close Windows Job Object, then resumes it. Timeout, cancellation, output limits, and normal parent exit all perform bounded descendant cleanup.

## Deliberate non-features

The foundation contains no model data, GGUF/OpenVINO interpretation, compatibility calculation, provider-resolution policy, product coordinator, candidate download, network access, or production LLM Fit package. LLM Fit v1.1.9 remains an unsigned Gate 1 candidate with `FunctionalPassWithPackagingConcern`; redistribution remains blocked pending an approved signed/package provenance and transitive-license closure.

Gate 3 may build an infrastructure-only LLM Fit command/parser/evidence adapter on this boundary. It must not bypass trusted-package custody, activate product composition, or infer model fit.
