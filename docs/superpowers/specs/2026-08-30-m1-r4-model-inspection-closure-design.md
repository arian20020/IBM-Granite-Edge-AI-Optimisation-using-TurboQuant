# M1 R4 Model Inspection Closure Design

## Status and authority

This is a bounded delta design for the already approved R4 M1 assignment. The
approved ModelInspectionHandoff contract on
`origin/handoff/hardware-inspection/c0-decision-v1` remains authoritative. This
document does not reopen its product choices and does not authorize changes to
the shell, XAML, shared project composition, hardware facts, or optimization
publication.

## Observed baseline

- The assigned base is commit `282a7690edd9bfbb48dbb324d09e76a7a154652e`,
  tree `812c22ea640633c5e8835266b902fb802728eea9`.
- The live GGUF registry retains the exact path-private projection and passes
  2/2 focused tests.
- The OpenVINO project discovers 411 tests and passes 404 with seven declared
  controlled-native-stage skips.
- The Model Inspection contract project discovers 410 tests. Its initial run
  passes 404 and reports six infrastructure errors because the evaluated-MSBuild
  helper recognizes only `DOTNET_HOST_PATH`, a test apphost named `dotnet.exe`,
  or one machine-specific SDK path.
- `OpenVinoSupportCode` already distinguishes dependency absence, timeout,
  cancellation, package mutation, and protocol failure, but
  `OpenVinoRouteService` collapses most of them to `Invalid`.
- The same approved six-field handoff exists as route-facing adapters, while
  `GraniteEdgeAI.ModelInspection.Contracts.ModelInspectionHandoffV2` is only a
  passive nested projection value and does not own the canonical 512-byte codec.

## Considered approaches

1. Keep the duplicated codecs and add only report evidence. This is too weak:
   schema drift remains possible and typed operational outcomes remain collapsed.
2. Move every route contract into one project and rewrite all consumers. This
   would require project-composition and Q1-collision edits that M1 is forbidden
   to make.
3. Make the existing shared Model Inspection handoff record the canonical
   six-field authority, retain narrow route adapters, prove byte-identical
   snapshots, and refine only the live inspection result mapping. This is the
   selected approach because it closes the semantic gap without widening M1
   ownership.

## Design

`ModelInspectionHandoffV2` in the shared Model Inspection contracts project
will own the schema version, exact ordered field list, UUIDv4 role separation,
lowercase SHA-256 and positive-length validation, canonical serialization,
strict parsing, and 512-byte ceiling. `ModelInspectionProjectionV2` will compose
that authority rather than reimplementing its handoff validation. The GGUF
application codec and OpenVINO protocol adapter remain boundary-specific types,
but focused tests must compare their canonical bytes with the authoritative
snapshot. No optimization field or route path is admitted.

`OpenVinoRouteInspectionOutcome` will preserve ready, ready-with-warning,
conversion-required, incomplete-package, and unsupported states while adding
separate dependency-unavailable, cancelled, timed-out, invalid-evidence, and
stale-evidence states. `OpenVinoRouteService` maps fixed support codes at the
inspection boundary. Expected cancellation becomes a typed terminal result;
programming defects are not hidden by a broad catch. Non-success results own no
handoff lease, configuration, or path-bearing offer unless the result is the
explicit one-time conversion-required state.

The evaluated-MSBuild test helper will prefer an explicitly supplied
`DOTNET_HOST_PATH`, then the current dotnet process, then the SDK host discovered
from the running SDK directory/standard Program Files location, and will reject
missing or non-file candidates. Model Inspection workflows will use the .NET 10
MTP `dotnet test --project` form and pass a concrete host path where nested
MSBuild evaluation needs it. No repository-relative output will contain a local
absolute path.

## Error and custody rules

- Projection and handoff validation occurs before any path-bearing lease is
  consumed.
- A rejected or stale projection leaves the OpenVINO lease retained for explicit
  disposal; successful activation consumes it exactly once.
- Conversion-required retains only its one-time source offer. Every other
  non-success inspection result disposes route-local snapshots and returns no
  path-bearing capability.
- Caller cancellation produces the typed cancelled result at this result-returning
  boundary. Cancellation still flows to the worker and cleanup is awaited.
- Unknown exceptions are not translated into success or a generic operational
  result.

## Verification design

Behavioral RED/GREEN covers byte-exact GGUF/OpenVINO/shared snapshots, same-role
UUID rejection, dependency/timeout/cancellation/stale/invalid mapping, and
lease-retention-before-validation. Full verification uses non-zero discovery,
the relevant contract/transport/worker/client/process/OpenVINO/compatibility/
hardware suites, Debug x64 builds, evaluated fixture/package matrices, privacy
and duplicate scans, `git diff --check`, and a bounded exact-app smoke. Native
and performance acceptance are explicitly not claimed on this development
machine.
