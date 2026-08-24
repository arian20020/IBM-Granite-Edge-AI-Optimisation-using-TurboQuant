# C1 Handoff — V2.1 OpenVINO Build-Identity Validation Correction

**Branch:** `feature/cross-route-optimisation-contracts-v2-1`
**Base:** `892bc689627142e5ffbd0ef0c12d2c5e952bd5a2` (V2, untouched — not amended, not force-pushed)
**Scope:** acceptance-domain correction. No schema change, no executor.

## Root cause

`OpenVinoBuildIdentity.Create` validated `runtimeBuild`, `genAiBuild` and
`tokenizersBuild` with `OptimizationIdentifier.Require`.

That rule rejects `/` — correctly, for the values it was written for. An
evidence id, configuration id, package id, profile id, device id or run id is a
name this product chose; it has no reason to contain a separator, so a slash in
one is evidence that a path has been passed where an identifier was expected.

A vendor build identity is a different kind of value. The official OpenVINO
runtime reports slash-delimited release-channel information, so the strict rule
made the authoritative identity impossible to represent without truncating or
rewriting it. O1 is prohibited from doing either, which left the field with no
valid value at all.

The fix separates the two rules rather than relaxing the strict one.

## Exact accepted official identity

```
2026.3.0-22451-8a17657b995-releases/2026/3
```

Accepted and preserved byte for byte: no normalisation, case folding,
truncation, rewriting or escaping. Tested through construction, through plan
issuance, and read back ordinally.

## Validator rules

New internal `OptimizationBuildIdentity.Require(value, parameter, what)` in
`GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization`.
Distinct from `OptimizationIdentifier`, which is **unchanged**.

Accepts:

- non-empty printable ASCII;
- ASCII letters, digits, `.`, `_`, `+`, `-`, `/` — and nothing else;
- at most 128 characters (the existing ceiling; the official identity is 42, and
  no published vendor identity approaches it).

Rejects:

| Rejected | Reason |
|---|---|
| leading or trailing whitespace | compared ordinally; a padded copy would not match the runtime that reported it |
| any space, tab, newline or other whitespace | same |
| control characters | not part of a version |
| non-ASCII | published vendor identities are ASCII |
| backslash | filesystem separator, not release-channel structure |
| colon | drive or scheme separator |
| leading `/` | an absolute location |
| trailing `/` | a directory |
| empty segment (`//`) | malformed path shape |
| `.` or `..` segment | traversal |
| over 128 characters | past a version and into unbounded content |

The slash is admitted **only** as a separator between non-empty, non-traversal
segments. Everything that would make the value path-shaped is still refused, so
the concession cannot become a real path.

The value is never passed to a filesystem or path API. It is compared, stored
and hashed, and nothing else.

The rejection message names the character *class* refused and deliberately does
not echo the offending character or the value — a rejection message is a place
adapter-supplied content leaks. Reviewed and recorded in the privacy allowlist
on that basis.

## Fields changed

Corrected to the new validator:

| Field | Why |
|---|---|
| `OpenVinoBuildIdentity.RuntimeBuild` | the reported defect |
| `OpenVinoBuildIdentity.GenAiBuild` | same vendor, same published format |
| `OpenVinoBuildIdentity.TokenizersBuild` | same vendor, same published format |
| `OpenVinoCapabilityPayload.RuntimeVersion` | **found by the audit** — the same OpenVINO runtime build identity the execution payload carries, so validating it generically would have reproduced this defect one field away |

`OpenVinoBuildIdentity.WorkerManifestDigest` keeps the canonical lowercase
SHA-256 validator. It is not a vendor build string.

Deliberately **not** changed, and pinned by
`FieldsLeftOnTheStrictRuleStillRejectASlash` so a later change is considered
rather than accidental:

| Field | Why left alone |
|---|---|
| `GgufCapabilityPayload.RuntimeVersion` | llama.cpp build tags (`b4321`) have no published slash-bearing form |
| `GgufQuantiserIdentity.ToolVersion` | same |
| `GgufExecutionPayload.RuntimeBuildId` | same |
| `OpenVinoExecutionPayload.OptimizerVersions` values | published as plain dotted versions (`3.3.0`, `2026.3.0`, `5.5.4`) |
| every generic identifier | unchanged by design |

Widening a rule without published evidence is how a guard stops meaning
anything. If one of these is later shown to carry release-channel structure, the
validator already exists and the change is one line plus a test.

## Contract shape — unchanged

- `OptimizationExecutionPlan.CurrentContractVersion` remains **2**.
- No public member added, removed or renamed. `OpenVinoBuildIdentity` still has
  exactly four properties (asserted).
- Canonical field order unchanged.
- The exact unmodified build string still reaches `ConfigurationSha256`.
- Changing any build identity still changes the digest; changing **only** the
  release-channel portion (`releases/2026/3` → `releases/2026/4`) changes it too.
- All route, payload and privacy invariants from V2 unchanged and green.

## Tests

```powershell
dotnet test --project tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj --configuration Release
```

| | |
|---|---|
| total | **771** |
| failed | **0** |
| succeeded | 771 |
| skipped | 0 |
| V2 baseline | 728 |
| added by V2.1 | 43 |

Whole project, non-zero discovered count, no filter. (The `--filter` and
`--list-tests` forms return `total: 0` with this runner, which is
indistinguishable from a passing run and is not used as evidence.)

Red-green was observed: the new suite failed **18** tests against V2 before the
correction — the official identity rejected, every ordinary vendor version
rejected, and every digest test unreachable — then all passed after it.

New file `Invariants/OpenVinoBuildIdentityValidationTests.cs` covers: the exact
official identity accepted and preserved byte-for-byte; survival through plan
issuance; participation in `ConfigurationSha256`; release-channel-only change
moving the digest; all three fields using the corrected validator; each field
moving the digest independently; every listed rejection case (`C:\private\runtime`,
`/private/runtime`, `releases/../private`, `releases//2026`, `releases/./2026`,
trailing slash, leading slash, backslash, colon, whitespace, control character,
non-ASCII, empty, overlength); the bound being exact at 128; generic
identifiers still rejecting `/` across evidence, package, snapshot, workload,
run, configuration, device and maturity fields; `WorkerManifestDigest` still
requiring a canonical digest; contract version and member count unchanged; and
no build-identity member named as a path.

## Build

```powershell
dotnet build "IBM Granite with TurboQuant (Intel).slnx" -c Debug -p:Platform=x64
```

Build succeeded, zero errors. `git diff --check` reports no whitespace errors.

## Changed paths

```
shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/OptimizationBuildIdentity.cs                (new)
shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/Execution/OpenVinoExecutionPayload.cs       (modified)
shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/OptimizationCapabilitySnapshot.cs           (modified)
tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/OpenVinoBuildIdentityValidationTests.cs         (new)
tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/PrivacyCanaryTests.cs                           (allowlist entry)
```

`OptimizationIdentifier.cs` is **not** in that list, by design.

## Privacy

No path enters a plan, result, diagnostic, log or `ToString` output. The V2
controls are unchanged and still green: `TrustedContextsAreNotReachableFromAPlan`,
`TrustedContextsAreNotRecords`, `NoTrustedContextMemberReturnsThePathUngated`,
and the assembly-wide canary. The one new string-carrying member
(`OptimizationBuildIdentity.Describe`) was reviewed and admitted on the basis
that it echoes neither the value nor the offending character.

## For O1

Pass the runtime's reported build identity through unchanged. Do not truncate
it, do not strip the release channel, do not normalise case. Both
`OpenVinoBuildIdentity` and `OpenVinoCapabilityPayload` now accept it, and the
full string participates in the plan digest — so a machine on a different
release channel produces a different plan identity, which is the intended
behaviour.

## Not done

No executor, UI, XAML, navigation, hardware, packaging, native stage or
application-control configuration was modified. Not merged into O1, G1, I0 or
main. No PR opened.
