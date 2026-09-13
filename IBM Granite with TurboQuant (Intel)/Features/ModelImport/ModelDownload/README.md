# Direct verified model downloads

This folder implements the recommended Granite GGUF download card on Model Import.

The slider selects one of five immutable IBM Granite 4.0 H-Micro artifacts:

| Preference | Artifact |
|---|---|
| Maximum efficiency | Q2_K |
| Efficient | Q3_K_M |
| Balanced | Q4_K_M |
| High capability | Q5_K_M |
| Maximum capability | Q8_0 |

Every entry pins the official repository, immutable revision, filename, byte length and SHA-256. The application downloads directly over HTTPS from the approved Hugging Face distribution domains; no Granite Edge cloud service is involved.

## Transaction boundary

`HttpModelDownloadTransport` streams response bodies, follows at most five validated HTTPS redirects and never supplies cookies or credentials. `ResumableVerifiedModelDownloadService` checkpoints partial transfers, validates range responses, rejects oversized, truncated or inconsistent content, and publishes only after exact length and SHA-256 verification. `AppModelLibrary` keeps partial and completed artifacts beneath the app's managed local model directory.

Interrupted transfers remain resumable across application restarts. Explicit discard removes only the selected catalogue entry's partial artifact. Metered, roaming, over-limit and unknown-cost connections require a second explicit action; offline state never opens the transport.

## UI and handoff

`ModelDownloadCoordinator` snapshots the selected slider band, owns the active operation and exposes path-free presentation events. `ModelDownloadCard` renders selection, progress, verification, interruption, retry and discard states without containing network or file-system logic.

After verification, `ModelImportPage` atomically claims the private local artifact and sends it through the existing `SubmitInputAsync` classifier and GGUF quick-scan path. Model Inspection is requested only after that existing validation succeeds. Picker and Explorer drop remain competing entry points and retire any pending automatic handoff.

## Non-claims

- Downloaded GGUF files are user data and are not packaged in the MSIX.
- This feature does not download OpenVINO models or arbitrary URLs.
- It does not bypass Model Import, Model Inspection, hardware inspection or compatibility checks.
- A completed file is not trusted merely because a server returned success; the pinned local digest is authoritative.
