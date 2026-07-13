# Test ID Catalogue

**Source:** exact IDs audited from the workbooks in `Testing (1)(1).zip`  
**Controlled unique ID count:** 105

No source workbook test ID may be renamed or reused. A physical execution adds a run suffix, for example `UL-04-R001`; a retest becomes `UL-04-R002` and does not overwrite the earlier run.

## WB-01 — Upstream llama.cpp

**Build/setup:** `UL-B01`, `UL-B02`, `UL-B03`, `UL-B04`, `UL-B05`, `UL-B06`, `UL-B07`  
**Formal tests:** `UL-01`, `UL-02`, `UL-03`, `UL-04`, `UL-05`, `UL-06`, `UL-07`, `UL-08`, `UL-09`, `UL-10`, `UL-11`, `UL-12`, `UL-13`

## WB-02 — AtomicBot TurboQuant

**Build/setup:** `AB-B01`, `AB-B02`, `AB-B03`, `AB-B04`, `AB-B05`, `AB-B06`, `AB-B07`, `AB-B08`  
**Formal and matched-baseline tests:** `AB-01`, `AB-02`, `AB-03`, `AB-KV3-F16-4K`, `AB-04`, `AB-05`, `AB-06`, `AB-07`, `AB-08F`, `AB-KV8-F16-4K`, `AB-08Q`, `AB-09`, `AB-10`, `AB-11`, `AB-12`, `AB-13`, `AB-14`, `AB-15`, `AB-15M`

## WB-03 — animehacker TQ3_0

**Build/setup:** `AH-B01`, `AH-B02`, `AH-B03`, `AH-B04`, `AH-B05`, `AH-B06`, `AH-B07`, `AH-B08`  
**Formal tests:** `AH-01`, `AH-02`, `AH-03`, `AH-04`, `AH-05`, `AH-06`, `AH-07`, `AH-08`, `AH-09`, `AH-10`

## WB-04 — Official OpenVINO

**Build/setup:** `OV-B01`, `OV-B02`, `OV-B03`, `OV-B04`, `OV-B05`, `OV-B06`, `OV-B07`  
**Conversion/validation:** `OV-C01`, `OV-C02`, `OV-C03`, `OV-C04`, `OV-C05`, `OV-C06`  
**Formal inference:** `OV-01`, `OV-02`, `OV-03`, `OV-04`, `OV-05`, `OV-06`, `OV-07`, `OV-08`, `OV-09`, `OV-10`

## WB-05 — Custom OpenVINO TurboQuant

**Build/setup:** `OVT-B01`, `OVT-B02`, `OVT-B03`, `OVT-B04`, `OVT-B05`, `OVT-B06`, `OVT-B07`, `OVT-B08`  
**Formal tests:** `OVT-01`, `OVT-02`, `OVT-03`, `OVT-04`, `OVT-05`, `OVT-06`, `OVT-07`, `OVT-08`, `OVT-09`

## WB-06 — Cross-route comparison

The comparison workbook does not invent new execution IDs. Every value must cite one or more validated source test IDs and run IDs from WB-01 to WB-05.

## Route names

- `upstream-llama-cpp`
- `atomicbot-turboquant`
- `animehacker-tq3-0`
- `official-openvino`
- `custom-openvino-turboquant`
- `cross-route-comparison`

## Status rule

Use only: `Planned`, `Ready`, `Running`, `Passed`, `Failed`, `Blocked`, `Inconclusive`, `Superseded`, `Not supported`, `Not measured`, or `Not applicable`.
