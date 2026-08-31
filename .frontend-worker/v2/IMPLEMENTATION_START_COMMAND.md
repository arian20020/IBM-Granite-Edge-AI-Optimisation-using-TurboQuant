# Starting an implementation campaign

The worker is intentionally unable to infer permission. Start a bounded implementation campaign by sending Codex a prompt in this exact form:

```text
AUTHORIZE GRANITE FRONTEND V2 IMPLEMENTATION

Approved surface(s): <exact page, control, or feature names>
Approved base branch or commit: <branch or SHA>
Approved visual source: <Figma link, screenshot/spec path, or "design direction required">

Open the Granite Native Frontend Worker v2 implementation lock for only the
listed surface(s). First run the frontend contract guardian and produce the
allowed-file manifest. Then use the repo-local Granite master skill and the
pinned Microsoft WinUI skills. Do not change backend behaviour, contracts,
project dependencies, manifests, worker infrastructure, navigation outcomes,
cancellation, retry, or stale-result semantics. Do not implement any unlisted
surface.
```

Codex must preserve the complete user message in `authorization.json` by running:

```powershell
./scripts/Authorize-GraniteNativeFrontendWorkerV2.ps1 `
  -AuthorizationPhrase "AUTHORIZE GRANITE FRONTEND V2 IMPLEMENTATION" `
  -Surface "<exact surface>" `
  -ApprovedBy "<user identifier>" `
  -ApprovedVisualSource "<source>" `
  -AuthorizationRequest "<complete user request>"
```

Authorization is invalid when:

- the exact phrase is absent;
- no bounded surface is listed;
- the base revision has changed without re-baselining;
- the current task exceeds the recorded surface;
- the user asks only for analysis, planning, installation, or provider verification.

To close the lock after the campaign, run the same script with `-Close`.
