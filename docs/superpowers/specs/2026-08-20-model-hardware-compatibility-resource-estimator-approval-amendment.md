# Decision 4 Approval and Review Amendment

- **Decision:** 4 of 8
- **Status:** Approved design; implementation planning authorised
- **Approval date:** 2026-08-20
- **Parent design:** `docs/superpowers/specs/2026-08-20-model-hardware-compatibility-resource-estimator-design.md`
- **Applies to:** the complete `IConfigurationResourceEstimator` architecture

This binding amendment records approval of the parent Decision 4 design and two final review controls that must be applied during implementation. It does not change the selected provider architecture or move Decision 5–8 responsibilities into Decision 4.

## 1. Canonical process-security foundation

Decision 4 must reuse the canonical approved external-process foundation already established by the protected Model Inspection worker, or an accepted shared successor extracted from that foundation.

The shared foundation owns:

```text
trusted executable resolution and binary-integrity verification
CreateProcessW/STARTUPINFOEX launch without shell interpretation
creation-time Job Object assignment
exact inherited-handle allowlist
no unrelated handle inheritance
JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
active-process and process-memory limits
bounded stdin, stdout and stderr
timeout and caller cancellation
whole-process-tree termination and orphan-process proof
privacy-safe generic diagnostics
```

The Decision 4 adapter owns only:

```text
resource-estimator helper command profile
request framing over stdin
project-owned schema and version handshake
resource-estimation failure-code mapping
configuration-fingerprint and provider-identity validation
component mapping into application contracts
```

It must not introduce a competing generic process runner, executable verifier, trusted-tool manifest format, bounded-stream collector, Job Object wrapper or process-tree terminator merely to launch the estimator helper.

If the canonical shared foundation is not present on the accepted implementation base, the affected integration slice waits for or first integrates the approved foundation. A temporary duplicate is not permitted.

## 2. Privacy-safe device-route identity

Every device identity that crosses from Hardware Inspection into Model–Hardware Compatibility is an application-owned logical route key, represented by `ResourceDeviceRouteId`.

Examples:

```text
cpu:0
vulkan:0
openvino:CPU
openvino:GPU.0
```

It must not contain or be derived from a raw:

```text
PnP device-instance path
serial number
machine GUID
account name
computer name
telemetry identifier
registry path
```

The mapper is allowlisted and deterministic. Unknown or ambiguous native evidence returns a typed `NotEstablished` result rather than falling back to a raw identifier. Raw native identifiers remain private to Hardware Inspection and cannot enter the Compatibility request, configuration fingerprint, diagnostic or retained evidence.

Required tests:

```text
known canonical hardware route maps to one logical route key
same logical route produces the same key and fingerprint
raw PnP/serial/machine/account/registry values are rejected
unknown or ambiguous evidence never falls back to raw identity
privacy scans find no native device identifier in retained evidence
```

## 3. Current repository gate

The current Decision 4 planning branch is stacked on Decision 3 planning head `7180ccb8c8cdf94410a835f76cf34369557f6af8`. That base contains the Model Import/Quick-Scan production slice, but it does not contain the complete production Decisions 1–3 handoffs, Hardware Inspection handoff or canonical shared process package needed by the estimator implementation.

Therefore:

```text
Decision 4 planning may complete now

Decision 4 production implementation
→ begins only on an accepted integration base
→ reuses canonical predecessor contracts
→ does not create temporary object/dynamic/dictionary DTOs
```

The implementation plan must start with an executable entry gate that records the exact accepted SHAs, namespaces, type paths and packaged-test baseline before production code is written.

## 4. Precedence

Where the parent design could be read as allowing a Decision 4-specific generic process foundation, a raw hardware identifier, or immediate implementation on the present planning-only base, this amendment supplies the binding interpretation. All other contracts, provider boundaries, calibration rules, evidence grades, security controls, time-boxing and non-claims remain unchanged.

## 5. Engineering basis

- **Fundamentals of Software Architecture**, Chapters 3, 6, 8, 21, 22 and 26: keep one coherent infrastructure boundary, minimise coupling and govern architectural decisions.
- **Designing Secure Software**, Chapters 2–4, 6, 10 and 13: minimise exposed information, use allowlists, make trust boundaries explicit and reject unsafe fallback.
- **Systems Engineering: Principles and Practice**, Chapters 7, 12, 13, 16 and 17: allocate one owner per function, retire risk through an explicit gate and verify interfaces before integration.
- **Code Complete**, Chapters 3, 5, 8 and 28: satisfy prerequisites, hide volatile details and control tool/configuration identity.
