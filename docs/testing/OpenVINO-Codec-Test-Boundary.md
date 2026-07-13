# OpenVINO KV-Cache Codec Test Boundary

**Revision:** 1.1  
**Verified:** 13 July 2026  
**Purpose:** prevent official merged support from being confused with experimental-branch support.

## Official merged route

The pinned official source must be inspected at execution time. The verified planning baseline exposes independent key/value cache quantization algorithms `SCALAR` and `TURBO`. TurboQuant uses cache precision to select the 3-bit or 4-bit path. Therefore WB-04 evaluates TBQ3, TBQ4, asymmetric key/value combinations, norm correction and fallback.

QJL and PolarQuant are not assumed to be official merged codecs. WB-04 contains explicit negative capability tests so that absence is recorded rather than silently omitted.

## Experimental custom route

The experimental OpenVINO CPU SDPA branch/PR describes six codec values: TBQ3, TBQ4, TBQ3+QJL, TBQ4+QJL, Polar3 and Polar4, with independent key/value configuration. WB-05 therefore includes:

- algorithm-level conformance for each codec;
- an exhaustive 6 x 6 ordered K/V capability sweep;
- symmetric and representative asymmetric model tests;
- QJL and PolarQuant quality, memory and performance tests;
- norm-correction and fused-quantize ablations;
- context, stability, unsupported-path and regression tests.

## Claim rule

A codec may be called supported only when the pinned source builds, the requested runtime path activates, expected cache representation is observed, the run completes without unexplained fallback, and the result is preserved with hashes. An enum, comment, design document or accepted property is evidence of intent, not execution.
