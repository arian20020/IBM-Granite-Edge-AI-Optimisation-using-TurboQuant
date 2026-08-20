# Decision 4 Resource-Estimator Delivery Map

- **Decision:** 4 of 8
- **Status:** Approved delivery decomposition
- **Spec:** `docs/superpowers/specs/2026-08-20-model-hardware-compatibility-resource-estimator-design.md`
- **Reason for decomposition:** The estimator boundary, TurboQuant overlay, and OpenVINO provider are independently reviewable subsystems with different evidence prerequisites. Keeping them in separate implementation plans prevents the core GGUF route from being blocked by OpenVINO or format-specific calibration.

## Required execution order

```text
1. Core + standard GGUF plan
   ├── entry gate
   ├── bounded helper spike
   ├── immutable contracts/fingerprint
   ├── provider router/profile validation
   ├── protected standard-GGUF provider
   ├── app/runtime and storage profiles
   └── base calibration/evidence
        ↓
2. Decision 5 accepts exact TurboQuant/OpenVINO calculators
        ↓
3. TurboQuant overlay plan
   ├── format identity
   ├── atomic KV replacement
   ├── calculator integration
   ├── activation evidence
   └── per-format calibration
        ↓
4. OpenVINO plan
   ├── immutable package identity gate
   ├── typed scheduler/device configuration
   ├── structural provider
   ├── versioned profiles
   └── admitted-route calibration
```

## Implementation plans

1. `docs/superpowers/plans/2026-08-20-model-hardware-compatibility-resource-estimator-core-implementation.md`
2. `docs/superpowers/plans/2026-08-20-model-hardware-compatibility-turboquant-resource-estimator-implementation.md`
3. `docs/superpowers/plans/2026-08-20-model-hardware-compatibility-openvino-resource-estimator-implementation.md`

## Scope priorities

```text
Must finish first
→ standard GGUF/llama.cpp resource-estimation core

Must finish for the project's main optimisation claim
→ TurboQuant KV overlay for every format actually exposed

Second slice
→ OpenVINO only for routes with immutable package identity,
  accepted calculators and matched evidence
```

An unavailable OpenVINO route must not delay or weaken the GGUF/TurboQuant core. It remains a typed `NotEstablished` route.

## Shared gates

Every plan must preserve:

```text
application-owned immutable contracts
one exact configuration
no silent fallback
checked exact bytes
shared-memory counted once
private path handling
pinned provider/runtime identities
matched underprediction evidence
packaged WinUI verification
exact-head evidence
```

## Completion meaning

Decision 4 planning is complete when the specification and all three implementation plans are approved. Production Decision 4 is complete only after the implemented slices pass their own acceptance criteria. `F-M08` remains unverified until Decisions 5–6 and matched measured evidence satisfy the complete requirement.
