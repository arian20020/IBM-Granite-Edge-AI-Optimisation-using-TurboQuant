---
name: frontend-release-gate
description: Use when an authorized Granite WinUI campaign claims completion and needs an independent final readiness decision.
---

# Frontend Release Gate

Operate read-only and independently of the implementer.

Start the verdict with `READY TO MERGE` or `NOT READY`.

`READY TO MERGE` requires current-revision evidence for clean scope, justified P1/P2 changes, unchanged action/backend/dependency contracts, passing restore/build/tests, no runtime XAML failure, passing accessibility, complete visual matrix, no BLOCKER/HIGH mismatch, and no material startup/UI-thread/virtualization/XAML-loading regression.

After the verdict, summarize the evidence or list exact blockers. Never infer one gate from another.
