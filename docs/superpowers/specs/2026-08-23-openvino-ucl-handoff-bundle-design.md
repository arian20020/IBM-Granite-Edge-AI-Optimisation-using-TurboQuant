# OpenVINO UCL continuation bundle design

Date: 2026-08-23

Source branch: `feature/openvino-route`

Baseline implementation commit: `c1e0fe2f`

## Purpose

Create one portable, offline-oriented ZIP that lets a Codex worker on the UCL
Intel laptop reconstruct the exact repository state, understand the complete
OpenVINO/TurboQuant plan and execution history, validate transferred build
inputs, and continue the remaining hardware-dependent acceptance work without
mistaking transferred local artifacts for trusted UCL evidence.

The archive is a continuation kit, not a release artifact and not evidence of
UCL execution.

## Archive layout

The outer ZIP will contain:

- `READ_FIRST.md`: human-oriented entry point, prerequisites, extraction steps,
  trust warnings, expected size, and the shortest safe resume procedure.
- `CONTINUATION_PROMPT.md`: the detailed prompt to give the laptop Codex worker.
  It will cover the product objective, full task plan, completed work, commits,
  test evidence, problems encountered, security rulings, prohibited shortcuts,
  remaining work, final acceptance conditions, and handoff/reporting format.
- `repository/openvino-route.bundle`: a Git bundle containing the complete
  `feature/openvino-route` history through the handoff commit.
- `repository/source-c1e0fe2f.zip`: a tracked-file snapshot for immediate
  inspection even before cloning the Git bundle.
- `closures/official-worker.zip`: the verified official worker Stage 16 closure.
- `closures/turboquant-worker.zip`: the verified Task 16 TurboQuant closure.
- `closures/converter-stage-p.zip`: the final Task 13 converter Stage P closure.
- `inventory/payloads.json`: closed metadata identifying every payload by role,
  source commit or stage identity, archive length, SHA-256, expected extracted
  root, and whether it is an input or admissible evidence.
- `CHECKSUMS.sha256`: SHA-256 for every top-level payload and documentation file.
- `tools/Initialize-UclHandoff.ps1`: a fail-closed bootstrap script that checks
  archive hashes, clones the Git bundle, verifies the expected commit, extracts
  closures into distinct roots, verifies their checked-in manifest contracts,
  and refuses a dirty or mismatched checkout.
- `context/`: copies of the master implementation plan, Task 1–18 reports,
  progress ledger, release evidence catalogue, and a concise verification
  summary. These are duplicated outside the repository snapshot so they can be
  read before initialization.

## Payload policy

The bundle includes the three generated worker/converter closures because they
total about 1.23 GB uncompressed and are expensive to reconstruct. They remain
strictly transfer inputs. Their manifests and outer archive hashes allow the
laptop worker to detect corruption, but they do not prove independent UCL
construction, hardware execution, security approval, or release acceptance.

The temporary mirrored official Stage B is excluded because it was only a
local two-root regression copy and was never claimed as an independent build.
Build outputs, TestResults, user profiles, credentials, prompts, generated
model output, and local evidence roots are excluded.

The pinned Granite model is not bundled. The UCL worker must obtain the
authorized model through the approved source, then record and bind its exact
model ID, SHA-256, and byte length. External security and license approvals are
also not bundled or synthesized.

## Continuation prompt requirements

The prompt will instruct the laptop worker to:

1. Validate the outer ZIP checksum and every payload before extraction.
2. Clone from the Git bundle and confirm the expected clean handoff commit.
3. Read the master plan, progress file, Task 15–18 reports, evidence catalogue,
   and release-gate scripts before changing code or running acceptance work.
4. Preserve the established route-neutral lifecycle, protected-process
   boundary, distinct official/converter/TurboQuant closures, exact manifest
   binding, CPU requested/actual equality, and fail-closed capability policy.
5. Never use transferred local results as hosted or trusted UCL evidence.
6. Obtain the authorized pinned Granite model and required external approval
   records without placing secrets, model bytes, prompts, generated text, raw
   stdout/stderr, identity, or absolute paths in retained evidence.
7. Run the official CPU, matched TurboQuant, cancellation/cleanup, normal WinUI,
   privacy, and optional physical Intel GPU campaigns on the exact candidate.
8. Diagnose failures with focused RED/GREEN tests; preserve user work; avoid
   silent fallback, cache/backend substitution, synthetic acceptance evidence,
   unreviewed protocol migration, or central activation before gates close.
9. Run the ordered release gate only after all inputs agree on one immutable,
   clean commit.
10. Leave TurboQuant unregistered and unpackaged if any required evidence or
    approval remains open, and report the exact blocker rather than claiming
    completion.

## Bootstrap behavior

The bootstrap script will accept an explicit destination directory. It will
not default to a home directory, workspace root, or machine-wide temporary
directory. It will reject an existing nonempty destination, checksum mismatch,
missing payload, unexpected Git commit, aliased closure roots, manifest
verification failure, or dirty checkout.

It will produce a local `handoff-state.json` containing only path-safe payload
identities and verified hashes. That file is operational state, not release
evidence.

## Verification

Bundle construction is complete only when:

- the source checkout is clean and contains the expected handoff commit;
- `git bundle verify` succeeds and the feature ref resolves to that commit;
- the source snapshot contains only tracked files from the exact commit;
- all three closure verifiers pass before packaging;
- every nested archive can be listed and test-extracted;
- payload lengths and hashes match both inventory files;
- the bootstrap script parses and succeeds in a fresh temporary destination;
- the reconstructed checkout is clean at the expected commit;
- no excluded sensitive or generated evidence paths appear in the archive;
- the continuation prompt has no placeholders or contradictory acceptance
  claims; and
- the final outer ZIP has a separately reported SHA-256 and byte length.

## End product

The deliverable is one named ZIP plus its SHA-256 and size. After extraction,
the UCL laptop worker can initialize a clean working repository and verified
local input closures, read the full context, and execute the remaining trusted
hardware and governance gates. The final product feature is accepted only when
the repository release gate returns `openvino_release_accepted` on one clean
immutable candidate and all required external records are genuinely present.
