# Starting a bounded implementation campaign

Send Codex one active human message in this exact form:

```text
AUTHORIZE GRANITE FRONTEND V2 IMPLEMENTATION

Campaign ID: <lowercase-kebab-case identifier>
Approved surface(s): <exact page, control, or feature names>
Approved base revision: <branch, tag, or commit SHA that resolves to current HEAD>
Approved visual source: <Figma link, screenshot/spec path, or "design direction required">

Open a Granite Native Frontend Worker v2 campaign only for the listed surfaces.
First create the local authorization record, then run the frontend contract guardian,
produce a clean baseline and exact allowed-file manifest, and complete/approve the
screen specification before any production edit. Use Microsoft WinUI Skills as the
native implementation authority. Preserve all backend, contract, dependency,
manifest, worker, navigation, enabled-state, confirmation, cancellation, retry,
stale-result, persistence, networking, filesystem, and outcome semantics. Do not
implement any unlisted surface.
```

Codex must then run:

```powershell
pwsh -File scripts/Authorize-GraniteNativeFrontendWorkerV2.ps1 `
  -AuthorizationPhrase "AUTHORIZE GRANITE FRONTEND V2 IMPLEMENTATION" `
  -CampaignId "<campaign-id>" `
  -Surface "<exact surface>" `
  -ApprovedBaseRevision "<approved revision>" `
  -ApprovedVisualSource "<source>"
```

The script writes only the ignored local state file declared by `implementation-lock.yml`. It does not store the user's identity or full prompt in Git.

Authorization is invalid when:

- the exact phrase is absent from the active human message;
- campaign ID or bounded surfaces are missing;
- the approved revision does not resolve to current `HEAD`;
- tracked changes already exist before the campaign opens;
- the requested task exceeds the recorded surfaces;
- the required provider, guardian, screen-specification, or file-manifest gate is missing;
- the user asks only for analysis, initialization, installation, or planning.

Close the local campaign state with:

```powershell
pwsh -File scripts/Authorize-GraniteNativeFrontendWorkerV2.ps1 -Close
```
