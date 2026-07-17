# Workbook Control Diagnostic — Stage 3A

| Field | Value |
|---|---|
| Run ID | UL-B01-R001 |
| Failure ID | FAIL-CTRL-001 |
| Branch | testing/upstream-llama-cpp-controlled-retest |
| Base commit | 664f00a26e24c44b063e1d5074bb31edc2564024 |
| Independent generations | 2 |
| Delay between generations | 4 seconds |
| All six complete DOCX files byte-identical | True |

## Whole-file comparison

| Workbook | SHA-256 match | Size match |
|---|---:|---:|
| WB-01 | True | True |
| WB-02 | True | True |
| WB-03 | True | True |
| WB-04 | True | True |
| WB-05 | True | True |
| WB-06 | True | True |

## Stage 3A conclusion

The current generation process reproduced all six final DOCX files byte-for-byte in two independent runs.

This indicates that current same-environment DOCX generation is deterministic. It does not validate the stale hashes currently stored in the controlled manifest.

## Interpretation boundary

This diagnostic does not repair the controlled manifest, approve generated workbooks, close FAIL-CTRL-001, or reclassify UL-B01-R001.

No llama.cpp clone, build, Granite model load, hardware test or inference test was performed.
