# Workbook Control Diagnostic — Stage 1

| Field | Value |
|---|---|
| Run ID | UL-B01-R001 |
| Failure ID | FAIL-CTRL-001 |
| Branch | testing/upstream-llama-cpp-controlled-retest |
| Diagnostic base commit | e488636fd95a0f6362e6e55ab3a7eae724274e39 |
| UTC timestamp | 2026-07-14T02:12:36.0715357Z |
| Local timestamp | 2026-07-14T03:12:36.0888509+01:00 |
| .gitattributes present | True |
| core.autocrlf | file:C:/Program Files/Git/etc/gitconfig	true |
| core.eol | (not explicitly configured) |
| Stale Pending merge validator rule present | True |

## Canonical template hash comparison

| Workbook | Repository hash matches | Working-tree hash matches | Checkout conversion indicated |
|---|---:|---:|---:|
| WB-01 | False | False | False |
| WB-02 | False | False | False |
| WB-03 | False | False | False |
| WB-04 | True | True | False |
| WB-05 | False | False | False |
| WB-06 | False | False | False |

Detailed hashes and Git attributes are stored in:

experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-B01/UL-B01-R001/template-hash-diagnostic-stage1.csv

## Current revision references

- WB-01 1.1: docs/workbook-revision-control; #8; 9ea9429ff23a4a3eb7274d61ce27b8fc8f94c2a8
- WB-02 1.1: docs/workbook-revision-control; #8; 9ea9429ff23a4a3eb7274d61ce27b8fc8f94c2a8
- WB-03 1.1: docs/workbook-revision-control; #8; 9ea9429ff23a4a3eb7274d61ce27b8fc8f94c2a8
- WB-04 1.2: docs/workbook-revision-control; #8; 9ea9429ff23a4a3eb7274d61ce27b8fc8f94c2a8
- WB-05 1.2: docs/workbook-revision-control; #8; 9ea9429ff23a4a3eb7274d61ce27b8fc8f94c2a8
- WB-06 1.2: docs/workbook-revision-control; #8; 9ea9429ff23a4a3eb7274d61ce27b8fc8f94c2a8

## Workbook dependency specification

python-docx==1.2.0

## Installed workbook-generation environment

lxml==6.1.1<br>python-docx==1.2.0<br>typing_extensions==4.16.0

## Interpretation boundary

This stage diagnoses template-byte and revision-gate controls only.

It does not regenerate a workbook, close FAIL-CTRL-001, reclassify UL-B01-R001,
or provide any evidence about llama.cpp, IBM Granite, CPU, GPU, memory,
performance or inference.
