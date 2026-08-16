# Automation Scripts

Store repeatable build, test, benchmark, evidence-validation and release scripts here.

## Hardware Inspection

[`hardware-inspection/`](./hardware-inspection/) contains the integrity-pinned
LLM Fit v1.1.9 acquisition, Windows reference capture, and Gate 1 report
generator. The acquisition/capture path accepts no project or command override;
candidate execution is restricted to `--version` and
`--no-dashboard --json system`.

Raw candidate JSON, TRX, Windows names and local paths remain ignored. The
report generator reads bounded strict inputs, hashes every actual source,
retains only allowlisted derived facts, and writes Markdown atomically. A
prerequisite `Blocked` report requires positive fixed TRX/envelope evidence;
missing files alone never establish a blocker.
