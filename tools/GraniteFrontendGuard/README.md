# GraniteFrontendGuard

`GraniteFrontendGuard` is the repository-owned semantic boundary tool for Granite Native Frontend Worker v2. It complements, rather than replaces, existing tests, fixture galleries, runtime automation, accessibility review, and independent code review.

## Commands

Create a baseline before an authorized UI campaign:

```powershell
dotnet run --project tools/GraniteFrontendGuard/GraniteFrontendGuard.csproj -- \
  snapshot --repo . --output .frontend-worker/v2/baselines/<base-sha>/contracts.json
```

Create a second snapshot after implementation and compare them:

```powershell
dotnet run --project tools/GraniteFrontendGuard/GraniteFrontendGuard.csproj -- \
  compare \
  --before .frontend-worker/v2/baselines/<base-sha>/contracts.json \
  --after .frontend-worker/v2/evidence/<run-id>/contracts.json \
  --output .frontend-worker/v2/evidence/<run-id>/contract-comparison.json
```

Verify that this bootstrap branch changed only worker infrastructure and that the implementation lock is closed:

```powershell
dotnet run --project tools/GraniteFrontendGuard/GraniteFrontendGuard.csproj -- \
  verify-bootstrap \
  --repo . \
  --base integration/ucl-cross-route-native-validation-v1 \
  --authorization .frontend-worker/v2/authorization.json
```

## What it captures

- SHA-256 hashes for protected operational files.
- Public, protected, and internal C# declarations and enum members.
- Invocation/construction edges in behaviour-sensitive UI and presentation files.
- XAML event handlers, commands, command parameters, enabled-state bindings, selection bindings, `x:Bind`, and `Binding` expressions.
- Package references, project references, and imported build targets.

## Limitations

The current v2 guard uses Roslyn syntax analysis and XAML/project parsing. It is deliberately conservative, but it cannot prove complete behavioural equivalence. In particular, an unchanged method name does not prove unchanged runtime effects, and a changed presentation factory may still alter visible ordering or copy without changing its declared API.

A frontend campaign therefore also requires:

- the explicit action-parity manifest;
- unchanged backend and integration tests;
- existing feature fixtures;
- native runtime/UI Automation evidence;
- accessibility review;
- rendered visual review;
- independent guardian and release-gate judgement.

Do not weaken or bypass a finding merely because another gate passes.
