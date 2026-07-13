# Test ID Catalogue

**Controlled campaign revision:** 1.1  
**Authoritative unique ID count:** 224

No test ID may be renamed or reused. A physical execution adds `-R001`; a retest increments the run number and never overwrites earlier evidence.

## WB-01 — Upstream llama.cpp (20 IDs)

**Build/setup:** `UL-B01` to `UL-B07`  
**Formal:** `UL-01` to `UL-13`

## WB-02 — AtomicBot TurboQuant (27 IDs)

**Build/setup:** `AB-B01` to `AB-B08`  
**Formal/matched baseline:** `AB-01`, `AB-02`, `AB-03`, `AB-KV3-F16-4K`, `AB-04`, `AB-05`, `AB-06`, `AB-07`, `AB-08F`, `AB-KV8-F16-4K`, `AB-08Q`, `AB-09`, `AB-10`, `AB-11`, `AB-12`, `AB-13`, `AB-14`, `AB-15`, `AB-15M`

## WB-03 — animehacker TQ3_0 (18 IDs)

**Build/setup:** `AH-B01` to `AH-B08`  
**Formal:** `AH-01` to `AH-10`

## WB-04 — Official OpenVINO (60 IDs)

**Build/setup:** `OV-B01` to `OV-B12`  
**Conversion:** `OV-C01` to `OV-C06`  
**Standard official baselines:** `OV-01` to `OV-10`  
**TurboQuant capability sweep:** `OV-TQS-01` to `OV-TQS-12`  
**Formal official TurboQuant:** `OV-TQ-01` to `OV-TQ-20`

The official route evaluates merged TBQ3/TBQ4, symmetric and asymmetric K/V, key-only/value-only Turbo, norm correction, context scaling, repeatability, 8B safety and GPU fallback. `OV-TQ-19` and `OV-TQ-20` are negative capability tests for QJL and PolarQuant unless a later pinned official source proves support.

## WB-05 — Custom OpenVINO complete codec route (99 IDs)

**Build/setup:** `OVT-B01` to `OVT-B15`  
**Algorithm conformance:** `OVT-A01` to `OVT-A12`  
**All 36 ordered K/V codec pairs:** `OVT-S01` to `OVT-S36`  
**Formal end-to-end/ablation/context tests:** `OVT-01` to `OVT-36`

The six experimental codec values are TBQ4, TBQ3, TBQ4+QJL, TBQ3+QJL, Polar4 and Polar3. Existence of a source enum or flag is not a pass; activation and output correctness must be proved.

## WB-06 — Cross-route comparison

WB-06 does not invent execution IDs. Every comparison cites validated WB-01 to WB-05 test IDs and run IDs.

## Status values

`Planned`, `Ready`, `Running`, `Passed`, `Failed`, `Blocked`, `Inconclusive`, `Superseded`, `Not supported`, `Not measured`, `Not applicable`.
