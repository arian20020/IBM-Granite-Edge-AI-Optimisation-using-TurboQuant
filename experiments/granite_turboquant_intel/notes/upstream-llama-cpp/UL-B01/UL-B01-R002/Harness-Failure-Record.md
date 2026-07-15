# UL-B01-R002 Harness Failure

| Field | Value |
|---|---|
| Failure ID | FAIL-UL-B01-R002-HARNESS |
| Test ID | UL-B01 |
| Run ID | UL-B01-R002 |
| Stage | Exact-source fetch |
| Result | Failed |
| Failure type | Test harness |
| Upstream repository failure proven | No |
| Compilation performed | No |
| Model execution performed | No |
| Retest | UL-B01-R003 |

## Cause

Windows PowerShell treated Git's normal fetch progress written to stderr as a
terminating NativeCommandError because the wrapper used
ErrorActionPreference = Stop. The script stopped before it could inspect
Git's actual exit code and verify the tag, commit, remote and working tree.

## Correction

UL-B01-R003 uses System.Diagnostics.Process through Start-Process so stdout,
stderr and the native exit code are captured independently. stderr text alone
is not considered a failure.