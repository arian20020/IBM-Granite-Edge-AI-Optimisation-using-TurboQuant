# Task 14 report: stable official-route acceptance gate

## Outcome

Implemented one closed `StableRouteAcceptance` filter covering the complete
official CPU route: two clean closures, inspection and multi-turn generation,
fresh sessions, stop/continue, cancellation and process-tree cleanup, corruption
and bounded failures, offline conversion/source preservation, independently
published FP16/INT8/INT4 packages, and exact default/`u8` KV-cache evidence.

The acceptance script binds the result to an immutable commit, two independent
official manifests, the sealed converter manifest, model/configuration/driver
identities, sanitized CPU evidence, explicit proven-or-unavailable GPU disposition,
test counts, and zero process/staging residue. It does not rerun tests or substitute
another result.

Hosted CI now downloads every converter input from the reviewed locks, verifies
length and SHA-256, seals the offline closure, runs the stable filter, and binds
the evidence before cleanup. Trusted UCL keeps its existing single retained-input
owner: converter closure leasing/building, the stable filter, optional GPU proof,
and evidence binding all occur inside that audited campaign.

## Red/green evidence

- RED: the acceptance entrypoint was absent.
- GREEN: the entrypoint/workflow closure meta-test passed 1/1.
- RED: adding a second trusted-root consumer in workflow YAML violated the retained
  lease ownership boundary.
- GREEN: moving all trusted converter/stable work into the UCL campaign restored
  the focused owner contract and the complete 84-row workflow contract.
- RED: the first combined filter allowed CPU-heavy tests to contend and two native
  startups reached the fixed five-second production deadline.
- GREEN: an assembly-level one-worker policy made the same two tests pass together,
  then the complete stable filter passed 12/12 with no skips.
- GREEN: the standalone converter verifier bound all 23,758 Stage P files and
  approximately 995 MiB byte-for-byte to its strict manifest.
- GREEN: a wrong immutable commit was rejected with `stable_route_rejected`.

## Fresh local verification

- Stable route acceptance: 12 passed, 0 failed, 0 skipped in 4m24s.
- Closed OpenVINO contracts: 162 passed, 0 failed.
- Workflow/lease/privacy contracts: 84 passed, 0 failed.
- Affected PowerShell parser errors: zero.
- Converter Stage P strict manifest: valid.
- Official Stages C/D and converter Stage P retained the Task 13 manifest identities.
- No OpenVINO worker, converter Python process, or operation staging residue.

## External evidence status

The hosted and trusted-UCL workflows are implemented but were not dispatched from
this local worktree. Their immutable-commit artifacts remain an external release
gate and are not represented here as completed evidence.

Review and execution were performed inline per user direction; no subagent was
used.
