# Decision 4 Review Amendment — Shared Process Foundations and Device-Route Privacy

- **Decision:** 4 of 8
- **Status:** Binding review amendment
- **Date:** 2026-08-20
- **Applies to:** `resource-estimation-v1`
- **Parent design:** `2026-08-20-model-hardware-compatibility-resource-estimation-design.md`
- **Parent implementation plan:** `2026-08-20-model-hardware-compatibility-resource-estimation-implementation.md`

This amendment records two clarifications identified during the final adversarial review. It does not change the selected option-C architecture or move any Decision 5–8 responsibility into Decision 4.

## 1. Reuse the canonical shared external-process foundation

The GGUF helper needs provider-specific process integration, but Decision 4 must not create a second generic process-security subsystem.

Where the approved shared `ExternalTools` and `ProcessExecution` packages exist, the resource-estimation implementation must reuse them for:

```text
trusted executable manifest and approved path
binary SHA-256 and architecture verification
version/policy verification
fixed and allowlisted command construction
UseShellExecute=false and no shell interpretation
bounded stdin, stdout and stderr
timeout and caller cancellation
whole-process-tree termination
orphan-process checks
privacy-safe diagnostics
```

The Decision 4 adapter owns only helper-specific behaviour:

```text
resource-helper command profile
bounded stdin request framing
project-owned JSON schema and version handshake
mapping helper output into application resource components
resource-estimation-specific stable failure codes
configuration-fingerprint and provider-identity checks
```

It must not introduce competing generic abstractions such as a second process runner, external-tool manifest format, executable verifier, bounded-stream implementation or process-tree terminator.

If the canonical shared foundation is not yet present on the implementation base, the affected integration slice waits for or integrates that approved package. It must not use a temporary duplicate merely to compile.

### Required tests

```text
composition resolves the canonical shared runner/verifier
no Decision 4 generic process runner exists
helper command exposes only the fixed --stdio mode
all shared timeout/cancellation/tree-cleanup guarantees remain active
provider-specific diagnostics contain no raw process output or path
```

## 2. `TargetDeviceId` is a privacy-safe logical route key

Any `TargetDeviceId` field in a Decision 4 configuration means an application-owned, stable logical route key, for example:

```text
cpu:0
vulkan:0
openvino:CPU
openvino:GPU.0
```

It must not contain or derive from a raw:

```text
PnP device instance path
serial number
machine GUID
account identifier
computer name
vendor telemetry identifier
full driver registry path
```

Raw native identifiers, where Hardware Inspection genuinely needs them to reconcile evidence, remain private to that feature and are not copied into the Compatibility request, configuration fingerprint, public result, diagnostic or retained evidence.

The route-key mapper is allowlisted and deterministic. Unknown or ambiguous device evidence produces a typed unavailable/unsupported result rather than preserving a raw native identifier as a fallback.

### Required tests

```text
known canonical hardware route maps to one logical key
same logical route produces the same key and fingerprint
raw PnP/serial/machine strings are rejected
unknown or ambiguous route does not fall back to raw identity
fingerprint and evidence privacy scans contain no native device identifier
```

## 3. Precedence

Where the parent design or implementation plan could be read as authorising a Decision 4-specific generic process foundation, or as allowing a raw hardware identifier in `TargetDeviceId`, this amendment supplies the binding interpretation.

All other Decision 4 contracts, provider ownership, evidence levels, calibration requirements, security controls, non-claims and downstream decision boundaries remain unchanged.

## 4. Engineering basis

- **Fundamentals of Software Architecture**, Chapters 3, 6, 8 and 26: reduce coupling, reuse one coherent infrastructure boundary and govern architectural decisions.
- **Designing Secure Software**, Chapters 3–4, 6 and 10: minimise exposed information, use allowlists, enforce trust boundaries and reject unsafe fallback behaviour.
- **Code Complete**, Chapters 5, 8 and 28: hide volatile implementation details, avoid duplicate responsibility and control tool/configuration identity.
- **Systems Engineering: Principles and Practice**, Chapters 7, 12, 16 and 17: allocate one owner per function, control integration risk and verify interfaces explicitly.
