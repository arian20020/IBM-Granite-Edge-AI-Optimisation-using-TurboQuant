# OpenVINO Codec Testing Extension v1.1

**Status:** Controlled extension to the merged testing workspace  
**Date:** 13 July 2026  
**Purpose:** add complete OpenVINO TurboQuant, QJL and PolarQuant coverage without confusing official and experimental support.

## Controlling files

- `workbooks/text-templates/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.md`
- `workbooks/text-templates/05_Custom_OpenVINO_TurboQuant_Controlled_Retest_Workbook_v1.md`
- `workbooks/text-templates/06_Cross_Route_Controlled_Comparison_Workbook_v1.md`
- `OpenVINO-Codec-Test-Boundary.md`
- `Test-ID-Catalogue-v1.1.md`
- `OpenVINO-Codec-Traceability-Extension-v1.1.csv`

The original 105-ID source-workbook catalogue remains the provenance baseline. Revision 1.1 adds 119 controlled OpenVINO tests, giving 224 unique campaign test IDs in total.

## Official OpenVINO scope

WB-04 adds official merged TBQ3 and TBQ4 tests, independent key/value settings, symmetric and asymmetric pairs, key-only/value-only TurboQuant, norm-correction ablations, context scaling, repeatability, Granite 8B safety gates and GPU fallback checks. QJL and PolarQuant are negative capability tests on the official route unless the pinned official source proves they were merged.

## Experimental OpenVINO scope

WB-05 covers TBQ4, TBQ3, TBQ4+QJL, TBQ3+QJL, Polar4 and Polar3. It contains algorithm-conformance tests, every ordered 6 x 6 K/V codec pair, full model evaluations, ablations, quality/perplexity comparison, context scaling, repeatability and unsupported-path tests.

## Execution boundary

No codec passes because an enum, property or design document exists. A pass requires a pinned build, verified dispatch, expected cache representation, no unexplained fallback, preserved output and matched memory/performance/quality evidence.

The generated DOCX workbooks and fully merged machine-readable registers are supplied in the revision 1.1 delivery package. Hardware results remain blank until executed on the target Windows Intel machine.
