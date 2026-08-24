# O1 OpenVINO C1 V2 Migration Design

Date: 2026-08-24

## Goal

Migrate the existing O1 OpenVINO optimization adapter from the frozen C1 V1
choice contract to the authoritative C1 V2 execution contract at
`892bc689627142e5ffbd0ef0c12d2c5e952bd5a2`, without restarting or rewriting
the proven O1 transaction, provenance, rollback, runtime-profile, or capability
projection work.

## Authority and compatibility

`OptimizationExecutionPlan` and its `ExecutionPayload.OpenVino` member are the
only authority for confirmed execution choices. O1 accepts a plan only when:

- `plan.IsExecutableBy(2)` is true;
- `plan.ContractVersion == 2`;
- the candidate, capability snapshot, and execution payload routes are all
  `OptimizationRoute.OpenVino`;
- `ExecutionPayload.OpenVino` is present and `ExecutionPayload.Gguf` is absent;
- candidate persistence and every overlapping candidate/payload field agree;
- the exact capability entry and current capability snapshot still admit the
  payload.

V1, missing, GGUF, mixed, mismatched, or future payloads fail closed with a
bounded `ReplanRequired` result before native work. O1 will not retain a second
execution-plan schema or infer any missing value.

## Payload mapping

The adapter maps the authoritative `OpenVinoExecutionPayload` directly into
the existing route-native `OpenVinoOptimizationCandidate`:

- `ConfigurationId`, `Device`, `Maturity`, and `EvidenceId` are copied exactly;
- source and target weight precisions map member-for-member to the route enum;
- KV-cache precision maps member-for-member;
- all three compiled-cache facts must describe the existing route policy;
- `CreatesCompletePackage` must agree with persistent conversion;
- runtime, GenAI, tokenizer, and worker-manifest identities must match current
  admitted evidence exactly;
- every optimizer version is copied into and checked at provenance/completion;
- any TurboQuant build identity is rejected because O1 has no persistent TBQ
  executor and deliberately advertises no experimental optimization candidate.

Runtime-only execution is derived only from
`TargetWeightPrecision == SourceWeightPrecision`. Persistent behavior is
derived only from unequal precisions and must agree with both candidate and
payload persistence. Neither mode may silently become the other.

## Configuration digest

C1's canonicalizer remains internal and is not duplicated. O1 recomputes
`ConfigurationSha256` by resolving the exact confirmed candidate, then calling
the public V2 `OptimizationPlanIssuer.Issue` with the same execution payload,
capability snapshot, workload, binding, preference, model-layer input, and
creation time. The newly issued plan's digest must equal the confirmed plan's
digest ordinally. A disagreement returns `ReplanRequired` before staging.

O1 provenance schema v2 remains an execution-result/evidence schema. It binds
the authoritative plan ID, contract version, C1 configuration digest, complete
V2 OpenVINO payload, live journey/capability identities, and actual execution
outcomes. It does not redefine what the plan means.

## Trusted source and boundary validation

The model identity in the OpenVINO inspection handoff is the regular
`openvino_model.bin` file, not the package directory. O1 creates
`TrustedSourceContext.ForPlan(plan, <source>/openvino_model.bin)` and requires a
verified resolution:

1. before static inspection reads the package/model;
2. after the live `BeforeStaging` state read and immediately before conversion;
3. after the live `BeforePublish` state read before atomic publication.

The verified path is revealed only after verification and is used to prove that
the directory passed to the existing sealed package pipeline contains the exact
planned model. A failed regular-file, length, digest, or plan check returns
`ReplanRequired/SourceIdentityMismatch` before native launch.

At every existing boundary O1 independently reloads and verifies plan ID,
model-inspection run/handoff, model digest/length, hardware run/snapshot,
capability snapshot ID/hash/payload, V2 execution payload, and recomputed
configuration digest. No device, precision, cache policy, build identity,
optimizer version, TurboQuant identity, or persistence behavior is substituted.

## Preserved lifecycle

The existing hardened lifecycle remains intact:

- persistent: preflight, trusted source, retained transaction, conversion,
  validation, smoke, schema-v2 provenance, live revalidation, atomic publish,
  reinspection, rollback, cleanup;
- runtime-only: no conversion or persistent model artifact, hardened atomic
  profile storage, live revalidation, schema-v2 profile binding;
- reinspection failures roll back and return C1 `ReinspectionFailed`;
- terminal results remain bounded and path/tool-output free;
- five official CPU configurations remain only when their exact V2 payloads
  are admitted by current capability evidence;
- TurboQuant optimization remains an explicit nonclaim.

## Testing

Tests will first fail against the V1 adapter and then prove:

- V1/non-executable, missing, GGUF, mixed, and mismatched payloads fail closed;
- every V2 OpenVINO field reaches candidate execution or durable provenance;
- candidate/payload mismatch and digest mismatch return `ReplanRequired`;
- trusted-source failure occurs before any native pipeline call;
- live plan/model/hardware/capability checks still run at all three boundaries;
- runtime-only plans create no model package;
- persistent plans never fall back to runtime-only profiles;
- O1 contains no duplicate plan/payload authority;
- the real converter-gated E2E uses projector, V2 issuer, adapter, service,
  sealed pipeline, and schema-v2 provenance.

Fresh completion evidence includes the complete O1 suite, complete C1 V2 suite
(at least 728 tests), OpenVINO contracts, integration observations, Debug x64
application build, and `git diff --check`. Native-stage skips and Windows
Application Control failures remain failures/skips, never passes. Release
packaging remains blocked until verified official/converter/TurboQuant stages
and policy-compliant binaries exist.

## Delivery

After clean review and verification, push only
`feature/openvino-optimisation-adapter-v1`. Do not merge into I0 or main.
