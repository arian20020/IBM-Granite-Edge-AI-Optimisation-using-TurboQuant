# GraniteFrontendGuard

Repository-owned .NET 8 guard used by the Granite Native Frontend Worker.

## Commands

```powershell
dotnet run --project tools/GraniteFrontendGuard -- snapshot --repo . --output before.json
dotnet run --project tools/GraniteFrontendGuard -- compare --before before.json --after after.json --output comparison.json
dotnet run --project tools/GraniteFrontendGuard -- verify-bootstrap --repo . --base integration/ucl-cross-route-native-validation-v1
```

## Evidence captured

- SHA-256 for every protected file, regardless of extension.
- Public, protected, and internal source declarations.
- Full behaviour-sensitive invocation, construction, assignment, and mutation syntax with occurrence counts.
- Stable XAML event/command/enabled/selection/binding contracts without line-number noise.
- Package references, project references, and imported targets.
- Committed, staged, unstaged, and untracked bootstrap paths.

The guard is deliberately conservative. A difference is evidence for independent review, not automatic proof that a change is acceptable or unacceptable. It complements action-parity manifests, unchanged tests, fixture galleries, accessibility review, and runtime visual evidence.
