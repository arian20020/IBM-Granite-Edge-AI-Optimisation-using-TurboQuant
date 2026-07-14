# Workbook Control Diagnostic — Stage 2

| Workbook | Matching transformation | Matching historical commit | Conclusion |
|---|---|---|---|
| WB-01 | None | None | No tested transformation or reachable historical object matched. |
| WB-02 | None | None | No tested transformation or reachable historical object matched. |
| WB-03 | None | None | No tested transformation or reachable historical object matched. |
| WB-04 | current-repository-bytes, utf8-lf-no-bom-one-final-newline | 6be265ae9c3b, 9ea9429ff23a, 5b48d4d64c1b, c5f037389ba0, 8d02bea53425 | Manifest hash matches historical commit(s): 6be265ae9c3b, 9ea9429ff23a, 5b48d4d64c1b, c5f037389ba0, 8d02bea53425 |
| WB-05 | None | None | No tested transformation or reachable historical object matched. |
| WB-06 | None | None | No tested transformation or reachable historical object matched. |

## Evidence files

- `template-hash-diagnostic-stage2-variants.csv`
- `template-hash-diagnostic-stage2-history.csv`

## Interpretation boundary

This stage tests common encoding and line-ending transformations and searches reachable Git history for exact historical template bytes.

It does not change the controlled manifest, validator, workbook files, FAIL-CTRL-001 or the Blocked classification of UL-B01-R001.
