# OpenVINO GenAI TurboQuant patch set

This directory contains the portable project patch set applied to the immutable
OpenVINO GenAI `2026.2.1.0` baseline at commit
`7dea0459b2ac7d8dfd877fd9df6737674fd8371d`.

Patch files must use the `.patch` suffix. The workspace controller applies them
in lexical filename order to a local, no-hardlink clone on branch
`project/turboquant-wb04`. It refuses a dirty upstream or destination and writes
source identity evidence beside the derived checkout. Project-added TurboQuant
functionality must remain identified separately from upstream OpenVINO support.

The ordered series is:

1. `0001-tbq-codec.patch` — deterministic norm-preserving TBQ3/TBQ4 codecs.
2. `0002-kv-config-telemetry.patch` — independent key/value configuration and
   the initial activation-evidence contract.
3. `0003-fused-stateful-runtime.patch` — fused CPU state updates, Granite
   cache-reorder/dynamic-prefill/GQA handling, failure-atomic graph rewrites,
   exact STANDARD/mixed/TBQ persistent-byte reconciliation, and requested,
   activated, and observed cache-precision telemetry.
4. `0004-bounded-identity-gpu-standard.patch` — bounded transformed-model
   identity hashing plus runtime-observed STANDARD cache telemetry on an
   actual OpenVINO GPU device.

The development checkout used to export the fourth patch ended at derived
commit `d19c30ea81466dce337eec4ecd1bc037eabee1f0`, with tree
`2e872dd4817c42d91cb7c3094954d7b56fa12b0a`. Reproducible builds use the
controller-generated replay commit from all four patch files, not the
development commit. The replay tree must equal the tree above byte-for-byte.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

This folder contains the ordered patches used to build the experimental OpenVINO TurboQuant route from its pinned upstream baseline. Apply them only through the controlled replay process described above.

### Start here

Begin with [`0001-tbq-codec.patch`](0001-tbq-codec.patch). The tables below explain the remaining items.

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`0001-tbq-codec.patch`](0001-tbq-codec.patch) | Controlled source patch for 0001 tbq codec. Its presence does not prove it was applied in a test run. | Supporting repository file |
| [`0002-kv-config-telemetry.patch`](0002-kv-config-telemetry.patch) | Controlled source patch for 0002 kv config telemetry. Its presence does not prove it was applied in a test run. | Supporting repository file |
| [`0003-fused-stateful-runtime.patch`](0003-fused-stateful-runtime.patch) | Controlled source patch for 0003 fused stateful runtime. Its presence does not prove it was applied in a test run. | Supporting repository file |
| [`0004-bounded-identity-gpu-standard.patch`](0004-bounded-identity-gpu-standard.patch) | Controlled source patch for 0004 bounded identity gpu standard. Its presence does not prove it was applied in a test run. | Supporting repository file |

### Important boundaries

- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
