# Hardware Inspection Foundation

This project contains candidate-neutral Windows foundations for Hardware Inspection. It is referenced by the x64 app, but it does not activate production hardware collection. Production composition continues to use `UnavailableHardwareInspectionService` until the later provider, policy, and orchestrator gates are complete.

## Boundaries

- `WindowsSystemSnapshotProvider` captures physically installed, OS-usable, and currently available memory as distinct byte values, plus a UTC capture time and bounded OS facts.
- `TrustedToolPackageVerifier` accepts only an exact flat manifest under an approved root. It rejects path escape, reparse points, inventory drift, hash mismatch, and non-AMD64 PE images.
- Verification retains no-follow directory and member handles with deny-write/delete sharing. Hashing uses the retained executable handle, closing the path-inspection/open race.
- `VerifiedTrustedTool` is `IDisposable`. Callers must retain it for every execution and dispose it when the approved package is no longer needed. Execution takes a reference-counted custody lease, so concurrent owner disposal cannot release package custody early.
- `ExternalProcessRunner` accepts only a manifest-declared command identity. It has no shell or arbitrary command-string API.
- The runner creates the process suspended, restricts inherited handles to three standard-stream pipes, assigns and verifies the process in a private kill-on-close Windows Job Object, then resumes it. Timeout, cancellation, output limits, and normal parent exit all perform bounded descendant cleanup.
- `LlmFitHardwareEvidenceProvider` accepts only a live verified `llmfit` v1.1.9 package whose manifest contains exactly `version --version` and `system --no-dashboard --json system`. It does not accept caller-supplied arguments or dispose caller-owned custody.
- Each capture checks the exact `llmfit 1.1.9` version output with a 5-second timeout and independent 4 KiB stream limits, then invokes system inspection once with a 15-second timeout and independent 256 KiB stream limits. There is no retry, fallback, shell, PATH search, dashboard, listener, or network operation.
- System JSON parsing uses `System.Text.Json` with comments and trailing commas disabled and maximum depth 16. Unknown properties are tolerated, while duplicate properties, case drift, unsafe names, invalid CPU/RAM ranges, and contradictory GPU shapes fail closed through the bounded `LlmFitDiagnosticCode` enum.
- Provider evidence is immutable and noncanonical. It retains validated source GiB values, bounded CPU/GPU names, UTC capture time, and a lowercase SHA-256 of bounded system stdout; it never retains raw stdout, stderr, exit messages, paths, or exception text. Windows authority, unit normalization, and cross-provider resolution belong to later gates.

## Deliberate non-features

The foundation contains no model data, GGUF/OpenVINO interpretation, compatibility calculation, provider-resolution policy, product coordinator, candidate download, network access, or production LLM Fit package. LLM Fit v1.1.9 remains an unsigned Gate 1 candidate with `FunctionalPassWithPackagingConcern`; redistribution remains blocked pending an approved signed/package provenance and transitive-license closure.

Gate 3 supplies the infrastructure-only LLM Fit command/parser/evidence adapter behind this boundary. It does not activate product composition or infer model fit. Gate 4 is next: add Windows processor, memory, OS, DXGI graphics, storage, and provisional NPU enrichment while preserving the provider-specific evidence boundary.
