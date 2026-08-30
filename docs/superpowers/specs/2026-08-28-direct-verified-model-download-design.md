# Direct Verified Model Download Design

Status: Approved architecture, pending written-spec review

Date: 2026-08-28

Implementation base: `4748fe04f19afdf6b27c4c12502b84db325e7294`

Implementation tree: `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`

## 1. Outcome

The existing recommended-model card on Model Import will become a real downloader for five official IBM Granite 4.0 H-Micro GGUF artifacts. The continuous preference slider will select the actual quantisation downloaded. The application will download directly from IBM's official Hugging Face repository, verify the pinned byte length and SHA-256, store the verified model in an app-managed per-user model library, and feed it through the existing `SubmitInputAsync` selection pipeline before automatically requesting Model Inspection.

The model files will not be bundled in the MSIX and will not increase the installer size. An internet connection is required only to start or resume a model download. Granite Edge AI will not operate a cloud proxy, receive model bytes, or upload model, path, hardware or user data.

## 2. Approved product decisions

- Use direct verified HTTPS downloads from the official IBM Granite GGUF repository.
- Do not bundle model files with the application.
- Do not introduce a Granite Edge AI cloud relay or download server.
- Preserve the established light card layout and five visible preference labels.
- Map every preference band to a real pinned GGUF artifact.
- Store completed artifacts in the app-managed local model library.
- Automatically reuse an existing artifact only after exact verification.
- Automatically enter the existing Model Import validation path and begin Model Inspection after a successful download.
- Support visible progress, explicit cancellation and restart-safe resumption.
- Never expose a partial or unverified file to Model Import, Model Inspection, Chat or export.

## 3. Approaches considered

### 3.1 Selected: direct pinned vendor download

The application contains a small immutable catalogue and streams the chosen artifact from the official revision. This keeps the installer small, removes Granite Edge hosting and bandwidth costs, preserves local processing, and provides exact integrity checks.

### 3.2 Rejected: Granite Edge proxy or cloud server

A relay would add operating cost, service availability, abuse protection, privacy review and another integrity boundary. It provides no necessary capability for public official artifacts and would make an otherwise local application depend on Granite Edge infrastructure.

### 3.3 Rejected: model files inside the MSIX

Bundling even one model would add gigabytes to installation and update downloads. Bundling five variants would be unacceptable and would duplicate files most users never select.

## 4. Authoritative catalogue

Repository: `ibm-granite/granite-4.0-h-micro-GGUF`

Pinned revision: `51ce07a9c9cfa971ca359d9625836bf8a4a1b61f`

The catalogue contains exactly these entries:

| Slider range | Visible preference | Artifact | Bytes | SHA-256 |
|---:|---|---|---:|---|
| 0 to less than 20 | Maximum efficiency | `granite-4.0-h-micro-Q2_K.gguf` | 1,226,247,840 | `e60b313fc0ce2a0a2c3903f4399ac61fe1048c38f219bcbd9f7a708e168b8ead` |
| 20 to less than 40 | Efficient | `granite-4.0-h-micro-Q3_K_M.gguf` | 1,555,472,032 | `bb684177d546a2c6d3aec9cc591fc8fa75d9c16a27e798cbb2400fe52d9b7e29` |
| 40 to less than 60 | Balanced | `granite-4.0-h-micro-Q4_K_M.gguf` | 1,942,564,512 | `c698c78e895740f0e707eb7f8e92894f83f6d5b3f2f2f0b446dfe9635fa0063e` |
| 60 to less than 80 | High capability | `granite-4.0-h-micro-Q5_K_M.gguf` | 2,273,455,776 | `69857575412143ea74d66e4d54ad70ec420d42452eadfff4a7cc043fe445ed4c` |
| 80 to 100 | Maximum capability | `granite-4.0-h-micro-Q8_0.gguf` | 3,397,676,704 | `a009111abf2865b7aad1e66326a6c772cddc29bccd22898f470292068b27bb59` |

Each source URL is constructed from the fixed repository, pinned revision and fixed filename. No server response may change the catalogue selection, expected length or expected digest. The catalogue is application code or a read-only packaged resource, not mutable remote configuration.

The application displays decimal download sizes rounded to two decimals: 1.23 GB, 1.56 GB, 1.94 GB, 2.27 GB and 3.40 GB. Integrity and storage logic always uses the exact byte count.

Primary references:

- IBM Granite 4.0 model documentation: <https://www.ibm.com/granite/docs/models/granite4-0>
- Official IBM GGUF repository: <https://huggingface.co/ibm-granite/granite-4.0-h-micro-GGUF>
- Pinned repository revision: <https://huggingface.co/ibm-granite/granite-4.0-h-micro-GGUF/tree/51ce07a9c9cfa971ca359d9625836bf8a4a1b61f>
- IBM GGUF tooling and supported formats: <https://github.com/IBM/gguf>

## 5. Component design

### 5.1 `ModelDownloadCatalogEntry`

An immutable entry contains only:

- stable catalogue ID;
- visible preference and slider boundaries;
- quantisation label;
- repository ID;
- pinned revision;
- fixed filename;
- expected byte length;
- lowercase 64-hex SHA-256;
- public source and model-information links.

Construction rejects invalid ranges, overlapping bands, unsafe filenames, non-HTTPS sources, non-positive lengths and malformed digests.

### 5.2 `PinnedGraniteModelCatalog`

This component owns the exact five entries and maps any slider value from 0 through 100 to exactly one entry. It has no network behavior. Its tests pin the complete catalogue so an artifact identity change is a reviewed source change.

### 5.3 `IModelDownloadTransport`

The transport abstracts HTTP for deterministic tests. Production uses one bounded `HttpClient` pipeline with response-header streaming. It must:

- begin at the official pinned HTTPS resolve URL;
- send no credentials, cookies, local paths, hardware facts or custom identifying headers;
- use a fixed product user-agent without machine/user identity;
- enforce bounded connection and inactivity timeouts;
- follow at most five redirects manually;
- reject HTTP downgrade and unapproved redirect domains;
- never forward authorization or cookies across redirects;
- request a byte range only when resuming a validated partial state;
- expose status, headers and a streaming body without buffering the model in memory.

Approved redirect hosts are the exact official host and reviewed Hugging Face distribution suffixes required by the pinned resolve endpoint. Host matching must be label-boundary safe; string suffix matching such as `evil-huggingface.co` is prohibited. SHA-256 verification remains mandatory even after host validation.

### 5.4 `IModelLibrary`

Production storage uses a per-user app-owned directory under `ApplicationData.Current.LocalFolder`, with separate `Models` and `Downloads` children. Completed files and partial files remain on the same volume so final publication is an atomic rename rather than a second multi-gigabyte copy.

The library owns:

- safe fixed paths derived only from catalogue IDs and filenames;
- free-space checks for the remaining bytes plus a bounded safety margin;
- exclusive per-entry leases that prevent two writers;
- partial-state read/write/delete;
- exact final-file verification;
- atomic publication of verified content;
- cleanup of invalid temporary and final files.

No caller supplies a destination path. Catalogue filenames cannot contain directory separators, traversal, drive prefixes, alternate streams or reserved path forms. Reparse-point and final-root containment checks must fail closed.

### 5.5 `IModelDownloadService`

The service coordinates catalogue, transport and library operations. Its public operation accepts one immutable catalogue entry, progress sink and cancellation token, and returns a typed result:

- `Completed` with the verified local file identity;
- `AlreadyAvailable` with a freshly reverified local file identity;
- `Cancelled` after explicit user cancellation and partial deletion;
- `Interrupted` when resumable partial state is safely preserved;
- `Failed` with a bounded privacy-safe error code.

Raw URLs, response bodies, local directories and exception messages never become UI or ordinary diagnostics.

### 5.6 `ModelDownloadCoordinator`

The coordinator is the testable presentation/orchestration boundary. It owns one active download operation identity and these states:

- `Idle`;
- `Preparing`;
- `Downloading`;
- `Verifying`;
- `Completed`;
- `Interrupted`;
- `Failed`.

It snapshots the selected catalogue entry when the user starts. The slider is disabled until that operation reaches a terminal state, preventing the visible choice from drifting away from the downloaded artifact. Late progress or completion from a retired operation is ignored.

The coordinator exposes state and commands to `ModelDownloadCard`; it does not navigate and does not receive `ModelImportPage` internals.

### 5.7 Model Import adapter

`ModelImportPage` constructs or receives the production coordinator and handles one path-private successful-download event containing only the download operation ID and safe display filename. The page atomically claims the verified local path through `TryClaimVerifiedModel(operationId, out verifiedModel)`; the path never appears in the event or navigation payload. A claim is current-operation-bound and succeeds once. The page then calls:

1. `SubmitInputAsync(new ModelSelectionInput(localPath, filename, false))`;
2. verifies that the submitted operation is still current, accepted as GGUF and has a successful quick scan;
3. calls the existing guarded `TryRequestModelInspection()` method.

This is the only route from a download into inspection. No direct navigation, synthetic scan result or downloader-specific Model Inspection request is allowed.

Starting picker selection, Explorer drop, page retirement or another download retires the automatic handoff for the previous operation. A late completed download may remain safely in the library but cannot replace the user's newer selection or navigate.

## 6. Download and verification transaction

### 6.1 New transfer

1. Snapshot the selected catalogue entry and create a new operation identity.
2. Acquire the per-entry lease.
3. Reverify any completed local artifact by exact byte count and SHA-256.
4. If valid, return `AlreadyAvailable`.
5. Validate or discard any partial state.
6. Check free space for remaining bytes plus the safety margin.
7. Open the partial file for exclusive asynchronous sequential append or creation.
8. Stream network bytes directly to disk while enforcing the expected maximum and reporting throttled progress.
9. Flush and close the writer.
10. Require the exact final byte count.
11. Compute SHA-256 over the complete partial file and compare the digest with `CryptographicOperations.FixedTimeEquals`.
12. Atomically rename the verified partial to the fixed final name on the same volume.
13. Reopen the final file, verify final containment and length, and publish `Completed`.

The file cannot be observed as completed before steps 10 through 12 pass.

### 6.2 Resume after interruption or restart

Partial state contains only schema version, catalogue ID, pinned revision, filename, expected length/hash, current byte count, validator data such as ETag when supplied, and last successful update UTC. It contains no absolute path, user identity or host identity. During transfer, the service periodically flushes a bounded checkpoint and atomically replaces the state file. If a crash leaves the partial longer than the last durable checkpoint, recovery truncates it to that checkpoint before requesting a range. A partial shorter than its recorded checkpoint or lacking valid state is discarded and restarted.

On restart:

- validate the state against the current compiled catalogue;
- require the partial file length to equal the recorded byte count and be less than the expected length;
- issue a range request from that exact offset with `If-Range` when a strong validator is available;
- accept resume only from `206 Partial Content` whose `Content-Range` starts at the exact offset and whose total equals the pinned length;
- if the server safely returns a complete `200` response instead of the range, truncate the partial and restart from byte zero;
- reject malformed, overlapping, oversized or contradictory range responses;
- always verify the entire completed file from byte zero before publication.

After an unexpected app closure or network interruption, returning to Model Import automatically resumes only when Windows reports an unrestricted internet connection. Metered, roaming or unknown-cost connections show the preserved progress and require an explicit `Resume download` confirmation that repeats the remaining size. Offline state shows the preserved progress and enables Resume only after connectivity returns.

Explicit `Cancel download` deletes the partial file and state. It is distinct from an unexpected interruption.

### 6.3 Existing completed file

An expected filename is never trusted by presence alone. Before reuse, the service verifies containment, ordinary-file status, exact length and full SHA-256. A mismatch removes or quarantines the invalid app-owned file and requires a fresh download. It never falls through to Model Import.

## 7. UI and interaction

The established card hierarchy, light styling, spacing, rounded surfaces, slider and primary button remain. The feature adds only state needed to make the existing control truthful:

- changing the slider updates visible preference, quantisation and exact rounded download size;
- the primary button reads `Download selected model` in `Idle`;
- preparation shows `Preparing download`;
- transfer shows a determinate progress bar, percentage, downloaded/total size and `Cancel download`;
- verification shows an indeterminate progress indicator and `Verifying download`;
- interrupted state shows the preserved progress plus `Resume download` and `Discard download`;
- failure shows one concise message plus `Try again`;
- completed state announces verification before automatic inspection begins.

All state changes use an accessibility live region without exposing a URL or local path. Buttons retain the established Model Import action styling, minimum hit targets, keyboard focus visuals and disabled-state behavior. Progress updates are throttled to avoid UI-thread saturation.

The card must state that internet is required and show the selected download size before the user commits. Source and license attribution remains in the application's model information documentation rather than adding another card action in this increment.

## 8. Error behavior

User-facing error categories are deliberately bounded:

- `download-offline`: connect to the internet and resume;
- `download-metered-confirmation-required`: confirm before using the connection;
- `download-source-unavailable`: try again later;
- `download-insufficient-space`: free the displayed required amount;
- `download-access-failed`: app storage is unavailable;
- `download-interrupted`: progress was preserved and can resume;
- `download-integrity-failed`: the file was discarded because verification failed;
- `download-response-invalid`: the source returned an unsafe or inconsistent response;
- `download-cancelled`: the partial download was removed.

HTTP status details, signed redirect URLs, local paths and raw exception messages remain internal and are not persisted to ordinary logs. Diagnostics may include only catalogue ID, operation ID, bounded error code, byte counters and exception type.

No error enables Continue, navigation, Chat or export.

## 9. Network and packaging boundary

Add exactly the packaged `internetClient` capability for outbound HTTPS. Do not add private-network, server, broad file-system or enterprise-authentication capabilities.

The application accepts no inbound connections and opens no listener. It sends no telemetry as part of this feature. Downloading is always initiated by the user's card action or a resume governed by the connection-cost rule.

The application binary contains catalogue metadata only. Completed and partial models are per-user app data, not package content, resources, Git files or update payloads.

## 10. Lifecycle and concurrency

- Only one active card transfer is permitted per page/coordinator.
- A per-entry storage lease prevents duplicate writers across coordinators in the same process.
- Starting a different catalogue entry safely interrupts the previous operation before changing state.
- Explicit cancellation is bounded and awaited; the UI cannot report cancellation before the writer is closed.
- Page navigation retires automatic selection/navigation authority, even if a transport finishes later.
- App shutdown asks active transfers to stop and preserve resumable state without opening helper consoles or background processes.
- Startup recovery ignores or removes state that is malformed, stale relative to the compiled catalogue, complete without publication, or inconsistent with file length.

The first version does not download multiple quantisations concurrently and does not implement peer-to-peer transfer, cloud synchronization or arbitrary catalogue URLs.

## 11. Privacy and security invariants

- No local path enters UI text, navigation event payloads, source events, network requests or user-facing diagnostics.
- No model bytes are uploaded.
- No hardware facts, compatibility results, account identity, hostname or username are sent.
- Only fixed catalogue destinations are accepted.
- HTTPS downgrade, redirect loops, unsafe hosts, overlong responses and range inconsistencies fail closed.
- File size and SHA-256 must both match before publication.
- Completed publication is atomic and same-volume.
- Partial files have a non-GGUF extension and cannot be selected by the downloader as completed content.
- App-owned cleanup never follows caller-controlled paths, globs or reparse points.
- Model Import repeats its existing quick scan; download verification does not substitute for model validation.

## 12. Test design

### 12.1 Catalogue tests

- exact five bands, labels, filenames, lengths, digests and revision;
- all slider boundaries including 0, 20, 40, 60, 80 and 100;
- no overlap, gap, unsafe filename, invalid source or duplicate identity;
- displayed quantisation and size change with the selected band.

### 12.2 Download-service unit tests

Using a deterministic fake transport and temporary app-library abstraction:

- complete 200 response with correct bytes publishes atomically;
- correct-length wrong digest is discarded;
- wrong-length, truncated and oversized responses fail;
- cancellation closes the writer and deletes explicit-cancel partial state;
- unexpected interruption preserves valid partial state;
- valid 206 resume appends from the exact offset;
- mismatched `Content-Range` fails without appending;
- safe 200 fallback restarts from zero;
- invalid partial metadata or byte count restarts safely;
- valid existing artifact is reverified and reused without network;
- invalid existing artifact is never reused;
- insufficient space prevents the request;
- redirect limits, host boundaries and HTTPS downgrade fail closed;
- two writers cannot acquire the same entry;
- no test error or diagnostic contains a private path or signed URL.

### 12.3 Coordinator tests

- one active operation and stale-event rejection;
- exact state transitions and progress monotonicity;
- slider is locked to the snapshotted artifact during transfer;
- cancellation, interruption, resume, verification, failure and retry;
- metered/roaming restart requires confirmation;
- unrestricted restart offers or performs the approved resume behavior;
- explicit cancellation differs from interruption.

### 12.4 Model Import integration tests

- the completion event contains only an opaque operation ID and safe filename;
- the page can claim the current verified local path exactly once and only for that operation;
- successful download enters only through `SubmitInputAsync`;
- GGUF quick scan runs against the verified local artifact;
- successful current selection invokes guarded Model Inspection exactly once;
- failed scan never navigates;
- picker/drop/new download/page retirement invalidates an older automatic handoff;
- a late old completion cannot replace the current selection;
- event and navigation contracts remain path-private.

### 12.5 Packaged WinUI tests

- initial Balanced/Q4_K_M/1.94 GB presentation;
- every slider band updates the same established card fields;
- Idle, Preparing, Downloading, Verifying, Interrupted, Failed and Completed states;
- keyboard invocation, focus order, live-region announcements and disabled controls;
- narrow/high-scale layout without clipping;
- exact existing visual hierarchy and action styling remain.

### 12.6 Acceptance verification

- Debug x64 solution build;
- production package build;
- non-zero ModelDownload, ModelImport and Onboarding packaged VSTest filters;
- deterministic service and integration projects;
- package manifest contains only the required outbound capability change;
- package-content scan proves no GGUF artifact is bundled;
- privacy/path scan and `git diff --check`;
- one controlled real download from the pinned official source where the environment permits, followed by exact independent byte/hash verification and the real Model Inspection transition.

The real multi-gigabyte download is acceptance evidence, not a routine unit-test dependency. CI and ordinary developer tests use deterministic bounded streams and exact catalogue metadata.

## 13. Non-goals

- No OpenVINO model catalogue in this increment.
- No arbitrary URL or repository entry.
- No Granite Edge cloud service.
- No account, telemetry, synchronization or cross-device library.
- No simultaneous multi-model downloads.
- No model deletion/library-management screen beyond the state required to discard an interrupted partial.
- No change to Model Inspection, Hardware Inspection, compatibility, optimisation or Chat semantics.
- No visual redesign of Model Import.

## 14. Acceptance criteria

The feature is complete only when:

1. each slider band selects the exact pinned artifact in section 4;
2. the installer contains no model file;
3. transfer progress, cancellation and restart-safe resumption work without helper windows;
4. only exact-length, exact-digest files become completed models;
5. existing models are reverified before reuse;
6. success flows through `SubmitInputAsync` and guarded Model Inspection with stale-operation protection;
7. no local path, model bytes, hardware data or identity leaves the device;
8. packaged and deterministic tests discover non-zero tests and pass, except honestly recorded external network/policy blockers;
9. the approved card structure and styling remain intact;
10. application, worktree and package verification are clean.
