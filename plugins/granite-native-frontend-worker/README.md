# Granite Native Frontend Worker v2

Repo-local Codex plugin for contract-protected WinUI 3, C#, and XAML frontend work. Root `AGENTS.md` routes frontend tasks through the canonical master prompt and `granite-native-frontend-master` skill.

## Default state

Implementation is closed unless the ignored local authorization state declared by `.frontend-worker/v2/implementation-lock.yml` is valid and matches the active human instruction.

The exact future phrase is:

`AUTHORIZE GRANITE FRONTEND V2 IMPLEMENTATION`

The phrase alone is insufficient: campaign ID, bounded surfaces, approved base revision, visual source, guardian baseline, approved screen specification, and exact file manifest are also required.

## Initialise

From the repository root on Windows:

```powershell
pwsh -File scripts/Initialize-GraniteNativeFrontendWorkerV2.ps1 -Install
```

Start a fresh Codex session after plugin installation, then verify:

```powershell
pwsh -File scripts/Test-GraniteNativeFrontendWorkerV2.ps1
```

The initializer never enables Developer Mode, elevates privileges, changes application dependencies, or modifies production application files.

## Provider roles

Required:

- Microsoft WinUI Skills — native technical authority.
- Superpowers — planning, TDD, debugging, and verification methodology.
- Bundled Uncodixfy WinUI v2 adapter — anti-generic design audit.

Optional:

- Stark — native product/design adviser.
- UI/UX Pro Max — narrow WinUI research.
- Figma — approved visual source.
- Product Design — concept exploration before approval.

Only `xaml_surface_implementer` may write production application files after every campaign gate passes.
