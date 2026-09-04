# Reproduction guide

Use the isolated branch, pinned dependencies and a quiet Windows host. Do not run formal measurements until the five-minute idle and 60-second readiness gate passes.

```powershell
$Python = 'C:\R4-TV1-assets\controlled-py312\Scripts\python.exe'
$Artifacts = 'C:\R4-TV1-assets\production-v2-embeddings-20260904'
$Conditions = 'C:\R4-TV1-assets\formal-conditions-20260904.json'

& $Python -m pytest (Get-ChildItem scripts/testing/tests -Filter 'test_turbovec_*.py').FullName -q
Start-Sleep -Seconds 300
& .\scripts\testing\Capture-TurboVecMachineState.ps1 -PythonExe $Python -OutputRoot 'C:\R4-TV1-assets\new-readiness' -ConditionsJson $Conditions -DurationSeconds 60 -IntervalSeconds 3
```

Only if `new-readiness/decision.json` says `ready: true`, run a new uniquely named block:

```powershell
& $Python scripts/testing/run_turbovec_feasibility.py formal-scale --artifact-root $Artifacts --readiness-root 'C:\R4-TV1-assets\new-readiness' --scale 1000 --output-root 'C:\R4-TV1\experiments\raw-results\turbovec\production-scale-v2\NEW-UNIQUE-RUN-ID' --seed 2026090431
& $Python -m scripts.testing.turbovec.validate_campaign --run-directory 'C:\R4-TV1\experiments\raw-results\turbovec\production-scale-v2\NEW-UNIQUE-RUN-ID'
```

Repeat the entire idle/admission sequence before each scale. Never reuse an output directory, delete a failed attempt, change acceptance thresholds, or compare a candidate against a stale Exact run. Run 100,000 only after the 10,000 result demonstrates safe memory and recovery.
