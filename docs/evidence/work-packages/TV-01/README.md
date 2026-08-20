# TV-01 evidence record

**Work package:** `TV-01` — Identify and pin exact TurboVec implementation and contract
**Validation date:** 2026-08-21
**Status:** Verified
**Decision:** Command-line demonstrator only

## Definition of Done review

| Check | Result | Evidence |
|---|---|---|
| Exact repository and version | Pass | `RyanCodrai/turbovec` v1.0.0, commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49` in the [controlled evidence record](../../turbovec/README.md) |
| Licence and artefact identity | Pass for the bounded CLI-research role | MIT upstream; Windows x64 wheel SHA-256 `cd855e0b318a57dc57c733f9a62ae98de5192f4f6c2c760e305523e8ceb1b090`; see the [Licence Register](../../../risks/Licence-Register.md) and [Licence Review Notes](../../../risks/Licence-Review-Notes.md) |
| Platform proof | Pass for Windows x64 AMD; Intel unknown | Offline controlled execution on an AMD Ryzen 7 8845HS with `CPUExecutionProvider`; no intended Intel result is claimed |
| Input, output, vector and retrieval contracts | Pass for the CLI demonstrator | The [ADR](../../../architecture/decisions/ADR-TurboVec.md), [evidence record](../../turbovec/README.md) and [non-sensitive evidence index](../../turbovec/controlled-evidence-index.json) record approved input identity, GTVI persistence routes, bounded query output and mismatch/failure behaviour |
| Explicit go/no-go outcome | Pass | **Command-line demonstrator only**. The WinUI application remains Not indexed and no retrieval text is injected into Granite prompts |

## Validation boundary

The controlled AMD run demonstrated exact-identity doctor checks, offline index persistence and reload through both GTVI routes, meaningful bounded queries, quality-gate success and failure/privacy/index-mismatch controls. The small 22-chunk fixture failed the 4-bit storage ratio (`13.3527166`) and latency ratio (`2.38301297`) gates. Intended Intel evidence, representative-scale evaluation and application integration remain unavailable.

`TV-01` is therefore complete because its investigation and decision contract are satisfied; completion does not promote TurboVec into the application and does not complete `F-M25`–`F-M27`, `TV-02`–`TV-04`, or the separate LLM Fit half of `PD-10`.

The authoritative RTM workbook was updated through a recoverable controlled copy and the repository traceability catalogue was regenerated. Legacy evidence-structure maps outside the RTM generator are retained as historical generated snapshots because the repository contains no source-to-output generator for them; they were not hand-edited.
