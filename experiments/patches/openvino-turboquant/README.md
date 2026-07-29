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

The development checkout used to export the third patch ended at derived
commit `f485079dfcd4ad1f2cc211dca48d770c2b521f33`, with tree
`13c1a4672e6ac92e606b923aaaacfa6c5d528353`. Reproducible builds use the
controller-generated replay commit from all three patch files, not the
development commit. The replay tree must equal the tree above byte-for-byte.
