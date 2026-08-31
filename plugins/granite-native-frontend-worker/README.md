# Granite Native Frontend Worker v2

Repo-local Codex plugin for contract-protected WinUI 3, C# and XAML frontend work. Root `AGENTS.md` routes every frontend task through this worker automatically.

## Current state

The implementation lock is closed. This package can initialise and verify providers, prepare design specifications and run read-only audits. It must not modify production application files until the human supplies:

`START GRANITE FRONTEND IMPLEMENTATION V2`

## Install

From the repository root:

```powershell
pwsh -File scripts/frontend-worker/Initialize-GraniteNativeFrontendWorkerV2.ps1 -Install
```

Start a new Codex thread after installation.

The bootstrap also records readiness for Codex, Git, npm, Python, .NET, WinApp CLI and Accessibility Insights without silently enabling Developer Mode or performing elevated installs.

Verify:

```powershell
pwsh -File scripts/frontend-worker/Test-GraniteNativeFrontendWorkerV2.ps1
```

## Invoke

Select the `granite_native_frontend_master` custom agent or invoke the `granite-native-frontend-master` skill.

For the present bootstrap phase, use:

```text
Read docs/frontend-worker/GRANITE-NATIVE-FRONTEND-WORKER-V2-MASTER-PROMPT.md.
Initialise and verify every required provider.
Do not modify production XAML or C#.
Return INITIALIZATION READY or exact blockers, then stop.
```

## Provider roles

- Microsoft WinUI: native implementation authority.
- Stark: read-only Windows/UX design adviser.
- Uncodixfy: adapted read-only anti-generic audit.
- UI/UX Pro Max: targeted WinUI design intelligence.
- Figma: optional approved visual reference.
- Product Design: optional concept exploration only.

Only `xaml_surface_implementer` may write production application files after explicit authorization.

Custom-agent TOML files are included for Codex surfaces that support them. On surfaces that expose only skills, the master executes the same roles sequentially and retains the one-writer/reviewer separation.
