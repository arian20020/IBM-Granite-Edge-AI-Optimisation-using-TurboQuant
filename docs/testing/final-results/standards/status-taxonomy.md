# Status taxonomy

Canonical data uses normalized lowercase status tokens. Human-readable reports use the corresponding display labels.

| Canonical token | Display label | Meaning |
| --- | --- | --- |
| `passed` | Passed | The intended work executed and met its recorded acceptance condition. |
| `failed` | Failed | The work executed but did not meet its recorded acceptance condition. |
| `blocked` | Blocked | A precondition or operational constraint prevented completion. |
| `artifact_unavailable` | Artifact unavailable | A required model or other artifact was unavailable. |
| `not_executed` | Not executed | The intended work was not run. |
| `not_applicable` | Not applicable | The intended work does not apply to this route or configuration. |
| `not_collected` | Not collected | The metric was absent from a historical campaign. |

Every non-passed attempt includes a nonblank `reason`. Adapters may preserve a source-specific value in `source_status` or `failure_kind`, but must not introduce an alternative canonical status token.
