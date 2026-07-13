# Source-to-Control Mapping

This map shows how every source testing document is applied in the controlled repository.

| Source document | Controlling subject | Repository control |
|---|---|---|
| Testing Standard for the Project | Full software, system, AI, performance, UX, security and evidence standard | `Test-Strategy.md`, `/tests`, metric/register files and validation scripts |
| 1. Purpose of the feasibility investigation | Why pre-development feasibility is required | `Master-Test-Plan.md` objectives and route decisions |
| 2. Main feasibility-testing objective | Overall technical feasibility objective | Test traceability matrix and route exit gates |
| 3. Model Selection Strategy | Diagnostic, Granite 3B and Granite 8B selection | Model and configuration registers plus workbook matrices |
| 4. Hierarchy of Testing Objectives | Ordered evidence hierarchy | Master plan route phases and prerequisite gates |
| 5. Main Testing Questions | Compatibility, activation, memory, speed, quality and stability questions | Test catalogue, run register and cross-route comparison register |
| 6. llama.cpp route repository comparison objective | Upstream, AtomicBot and animehacker comparison | WB-01 to WB-03 and route-specific evidence folders |
| 7. Success and Outcome Levels | Pass, limitation, rejection and bounded-outcome rules | Status vocabulary, failure register and route decision sections |
| 8. Fallback Strategy | CPU/upstream/OpenVINO fallback and experimental-route boundaries | Master plan, decision log and cross-route application-role table |
| 9. Scope Boundaries | What may and may not be claimed | Claim boundaries in traceability and final reports |
| 10. Operational Testing Procedure | Exact run order, evidence structure and result categories | `experiments/granite_turboquant_intel/`, manifests, scripts and registers |
| Seven original workbooks | Exact test IDs, planned configurations and required result tables | Six controlled workbooks, test catalogue and workbook completion register |
| Upstream journal | Historical diagnosis and implementation context | Legacy-only reference; not an authoritative result without raw evidence |

## Conflict rule

When the sources differ:

1. preserve the original documents unchanged;
2. prefer the latest more specific operational/workbook instruction for the same route;
3. do not silently combine incompatible historical results;
4. record the chosen interpretation in `Decision-Log.md`;
5. keep the claim bounded to the evidence actually produced.
