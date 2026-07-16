# UL-B01-R003 Harness Failure

| Field | Value |
|---|---|
| Failure ID | FAIL-UL-B01-R003-HARNESS |
| Test ID | UL-B01 |
| Run ID | UL-B01-R003 |
| Stage | Raw evidence initialization |
| Result | Failed |
| Failure type | Test harness |
| Git fetch started | No |
| Compilation performed | No |
| Model execution performed | No |
| Retest | UL-B01-R004 |

## Cause

The harness attempted to create an intentionally empty stdout evidence file.
The Write-Utf8Text helper declared Text as a mandatory string but did not
allow an empty string, so PowerShell stopped during parameter binding.

## Correction

UL-B01-R004 permits empty evidence files explicitly and uses
System.Diagnostics.Process to separate native stdout, stderr, and exit codes.