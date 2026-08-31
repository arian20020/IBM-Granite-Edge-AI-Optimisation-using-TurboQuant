---
name: granite-native-frontend-initializer
description: Initialise or verify the Granite Native Frontend Worker v2 and its pinned WinUI design providers without changing production UI. Use before the first implementation campaign or after provider updates.
---

# Granite Native Frontend Initializer

This skill operates only in bootstrap scope.

1. Read the root instructions and confirm `implementation_authorized` is false.
2. Validate the repo-local plugin manifest, skills, adapters, provider lock, boundary policy, and start-command document.
3. Check availability of Codex, Git, .NET SDK, Microsoft WinApp CLI, Node/npm, Python, and Accessibility Insights without silently installing or upgrading anything.
4. Verify exact upstream revisions in `provider-lock.json`.
5. Initialise project-local UI/UX Pro Max only when the human explicitly authorises machine changes.
6. Register or enable Microsoft WinUI, Figma, Product Design, and Stark through their supported Codex surfaces when available; do not claim success without command or directory evidence.
7. Use the local Uncodixfy WinUI adapter by default. Do not load the original web-oriented skill as native platform authority.
8. Run `scripts/Test-GraniteNativeFrontendWorkerV2.ps1` and report every unavailable optional provider separately from required infrastructure.

Never edit application XAML, C#, resources, assets, projects, manifests, tests, or fixtures during initialization. Finish with an evidence-backed readiness report and leave the implementation lock closed.
