# OpenVINO route release evidence catalogue

This catalogue is the path-safe handoff for the OpenVINO route release gate.
It identifies evidence contracts and current disposition; it does not replace
the controlled RTM workbook or manually rewrite generated traceability files.

## Current disposition

The official CPU route, inspection, conversion, and standard optimization
layers have local verification recorded in the Task 1–14 reports under
`.superpowers/sdd/2026-08-20-openvino-route/`. TurboQuant Tasks 15–17 have a
verified local implementation boundary, but the experimental route remains
unregistered and unpackaged. The pinned-Granite trusted UCL campaign and
external security/license decisions are still required.

`Invoke-OpenVinoReleaseGate.ps1` therefore fails with
`openvino_release_blocked` unless separate official, converter, and TurboQuant
closures plus hosted and UCL evidence roots are supplied. Missing external
evidence is never converted into a pass.

The final invocation also requires a dedicated operation root; cleanup scans
that exact owned root and the three supplied closure roots rather than treating
the machine-wide temporary directory as application-owned state.

## Requirement-to-evidence map

| Identity | Evidence contract | Current disposition |
| --- | --- | --- |
| All 398 P1 atoms | Controlled RTM workbook plus regenerated repository views | I0 update pending; no manual generated-file edit |
| `MI-SEAM-001..028` | OpenVINO route contract, worker-client, process, and app suites | Local suites available; immutable release-candidate binding pending |
| P2/P3 findings | Task reports and final security/license reviews | External review pending where recorded |
| `F-M18` | Official CPU CLI/app prompting and stable acceptance evidence | Local verified; hosted/UCL candidate binding pending |
| `F-M20` | Explicit requested/actual device evidence and GPU fail-closed gate | CPU local verified; optional GPU remains evidence-gated |
| `F-M21` | Separate TurboQuant worker, typed activation, and normal WinUI flow | Worker/adapter local; WinUI UCL acceptance open |
| `F-M22` | Matched official/TBQ4 quality, memory, performance, lifecycle evidence | Real pinned-Granite matched campaign open |
| `N-M02` | Offline local execution, dependency locks, package integrity, cleanup | Local verified; final candidate evidence pending |
| `N-M11` | Experimental label, pinned tuple, explicit verified fallback | Local policy/tests verified; UCL acceptance open |
| `DR-WF-008` | Runtime/device identity separated from artifact format | Local typed contracts verified |
| `DR-WF-010` | Stable official route independent of experimental closure | Static and unit contracts verified; final package exercise pending |
| `DR-WF-011` | TurboQuant stays Experimental until Granite criteria pass | Enforced; acceptance remains open |
| `DR-WF-013` | Hosted evidence is immutable, typed, sanitized, and retained | Workflow contracts local; final hosted run pending |
| `DR-WF-014` | Trusted UCL evidence binds authorized hardware and exact commit | Workflow contracts local; final UCL run pending |
| `DR-WF-015` | Terminal cleanup and artifact privacy are release gates | Local scanners present; final candidate execution pending |

## Evidence admission rules

- Evidence must bind one lowercase 40-character Git commit and the exact model,
  configuration, closure, manifest, and requested/actual device identities.
- Official, converter, and TurboQuant install roots and manifests must be
  distinct. Removing the experimental closure must not affect the official
  route.
- Only bounded JSON/TRX/log/text/ZIP artifacts pass the generic privacy gate.
  Absolute paths, identity, environment dumps, prompts, generated text,
  model/tokenizer bytes, credentials, raw stdout/stderr, reparse points,
  unknown payload types, and archive traversal are rejected.
- Hosted and trusted UCL results remain separate, sanitized, and retained for
  30 days by their workflows.
- No requirement moves to completed/verified solely because a script or test
  exists; the cited immutable run must be available and pass.

## Final I0 handoff

After external approvals and both evidence campaigns close on one candidate,
I0 must update the controlled RTM workbook, regenerate its derived catalogue,
add direct immutable evidence links for every scoped atom, and run the ordered
release gate. Until then the correct release disposition is blocked, with the
official stable route remaining independently usable.
