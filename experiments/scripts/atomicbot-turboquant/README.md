# AtomicBot TurboQuant Scripts

The controlled runner entry point is `scripts/testing/run_atomicbot_retest.py`.
Use `--dry-run` to inspect matrix selection without launching a runtime. It
supports `--only`, `--from`, `--skip`, and reconciled-state-aware `--resume`;
completed attempts require the explicit `--replace-attempt` switch.

Place versioned PowerShell build, activation-check, capture and run scripts here. Scripts must record requested and verified cache configuration.
