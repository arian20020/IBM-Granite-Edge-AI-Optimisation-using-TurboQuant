# Intel Hardware Inspection Full-Integration Handoff Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce and verify a self-contained Downloads folder that lets Codex in VS Code on the UCL Intel laptop integrate every current Hardware Inspection line and carry the product feature through final merge readiness.

**Architecture:** Publish exact local-only source commits as immutable namespaced Git refs, then package the controlling contracts, branch map, visual sources, closure roadmap, and one executable master prompt. The Intel worker starts from current `main`, verifies every supplied ref and package hash, reconciles the functional and LLM Fit lines through an explicit preservation matrix, and keeps ordinary development separate from gated Stage execution.

**Tech Stack:** Git/GitHub, PowerShell 5.1+, SHA-256, Markdown, JSON, SVG/PNG, .NET 8/9, C# 13, WinUI 3, MSTest, Visual Studio 2022, Codex IDE extension.

---

### Task 1: Freeze and publish the handoff Git identities

**Files:**
- Modify: `docs/superpowers/plans/2026-08-22-intel-hardware-full-integration-handoff.md`
- Create remotely: `handoff/hardware-inspection/functional-v1`
- Create remotely: `handoff/hardware-inspection/model-visual-v1`
- Create remotely: `handoff/hardware-inspection/visual-contract-v1`
- Create remotely: `handoff/hardware-inspection/i1-s1-contract-v2`
- Create remotely: `handoff/hardware-inspection/c0-decision-v1`
- Create remotely: `integration/hardware-inspection-intel-completion-v1`

- [ ] **Step 1: Fetch and record the exact current base**

Run:

```powershell
git fetch origin main
git rev-parse origin/main
```

Expected: one lowercase 40-hex commit. Record it as `baseCommit` in the package status and manifest.

- [ ] **Step 2: Verify the five local source commits**

Run:

```powershell
git rev-parse feature/hardware-inspection-functional-v1
git rev-parse feature/model-inspection-hardware-template-v1
git rev-parse docs/hardware-inspection-visual-contract-v1
git rev-parse docs/hardware-inspection-i1-s1-r2-errata
git rev-parse docs/hardware-inspection-i1-s1-decision-v1
```

Expected, in order:

```text
f521e9eea81b59f5814fcf100e4f527391ee67d2
ba4fd7bad5c473208248247fcba27e6f22c356ab
bb50093688a1a73f898c5eee3bef4432e30381ef
63ce50f695cde59e76649efef2d5e3172e59b0b2
5e7a74300bdd0c2fff9ffe1bcf51eebed2bf4cc2
```

Stop if any identity differs. A changed source requires re-auditing the package rather than silently updating one hash.

- [ ] **Step 3: Publish namespaced refs without rewriting commits**

Run exact force-with-lease-free creates only after proving each destination does not exist:

```powershell
git ls-remote --heads origin 'refs/heads/handoff/hardware-inspection/*'
git push origin feature/hardware-inspection-functional-v1:refs/heads/handoff/hardware-inspection/functional-v1
git push origin feature/model-inspection-hardware-template-v1:refs/heads/handoff/hardware-inspection/model-visual-v1
git push origin docs/hardware-inspection-visual-contract-v1:refs/heads/handoff/hardware-inspection/visual-contract-v1
git push origin docs/hardware-inspection-i1-s1-r2-errata:refs/heads/handoff/hardware-inspection/i1-s1-contract-v2
git push origin docs/hardware-inspection-i1-s1-decision-v1:refs/heads/handoff/hardware-inspection/c0-decision-v1
git push origin origin/main:refs/heads/integration/hardware-inspection-intel-completion-v1
```

Expected: six new remote refs, each resolving to its recorded commit. Never force-update an existing handoff or integration ref.

- [ ] **Step 4: Re-read every remote ref and compare exact SHAs**

Run `git ls-remote --heads origin` for all six refs and mechanically compare each returned SHA with the source table. Expected: six exact matches and no duplicate destination.

### Task 2: Assemble the controlling source set

**Files:**
- Create: `C:\Users\Arian\Downloads\Intel-Hardware-Inspection-Full-Integration-Handoff\sources\*.md`
- Create: `C:\Users\Arian\Downloads\Intel-Hardware-Inspection-Full-Integration-Handoff\sources\*.json`
- Create: `C:\Users\Arian\Downloads\Intel-Hardware-Inspection-Full-Integration-Handoff\visual-references\*.svg`
- Create: `C:\Users\Arian\Downloads\Intel-Hardware-Inspection-Full-Integration-Handoff\visual-references\INDEX.md`

- [ ] **Step 1: Create a new empty package root**

Resolve the exact Downloads target. If it already exists, stop and choose a versioned sibling; do not delete or overwrite an earlier handoff.

- [ ] **Step 2: Export controlling documents from exact Git blobs**

Use `git show <commit>:<path>` to export, at minimum:

- approved Hardware visual contract;
- C0 decision register;
- corrected ModelInspectionHandoff contract;
- corrected Stage C execution boundary;
- Hardware production architecture;
- Hardware contract, presentation, page, lifecycle, route, visual-restoration, and shared-action specifications/plans;
- LLM Fit Gate 1 plan, verification record, and runbook;
- Stage A design, plan, and runbook;
- Model Inspection visual-alignment design and Visual Studio debug guide; and
- the approved handoff design and this implementation plan.

Expected: every export comes from the recorded commit, not a mutable working-tree copy.

- [ ] **Step 3: Export supported visual references**

Copy the approved SVG sources from repository blobs and the reviewed Downloads reference set. Inspect any historical visual ZIP read-only, extract only SVG/PNG files into a temporary directory, and include only references that match the approved modern light visual family. Do not include a ZIP or office document in the final folder.

- [ ] **Step 4: Write the visual index**

For each included reference, record source path, source SHA-256, observable state, viewport/scale intent, and whether it is normative or illustrative. Explicitly name the current application/fixture gallery as the ultimate implementation oracle when a historical image conflicts with the approved V0 contract or later user-approved spacing corrections.

### Task 3: Author the complete context documents

**Files:**
- Create: `C:\Users\Arian\Downloads\Intel-Hardware-Inspection-Full-Integration-Handoff\01-CURRENT-STATUS-AND-SCOPE.md`
- Create: `C:\Users\Arian\Downloads\Intel-Hardware-Inspection-Full-Integration-Handoff\02-VERIFIED-BRANCH-AND-SHA-MAP.md`
- Create: `C:\Users\Arian\Downloads\Intel-Hardware-Inspection-Full-Integration-Handoff\03-INTEGRATION-METHOD.md`
- Create: `C:\Users\Arian\Downloads\Intel-Hardware-Inspection-Full-Integration-Handoff\04-APPROVED-VISUAL-CONTRACT.md`
- Create: `C:\Users\Arian\Downloads\Intel-Hardware-Inspection-Full-Integration-Handoff\05-ARCHITECTURE-AND-DATA-CONTRACTS.md`
- Create: `C:\Users\Arian\Downloads\Intel-Hardware-Inspection-Full-Integration-Handoff\06-IMPLEMENTATION-AND-CLOSURE-ROADMAP.md`
- Create: `C:\Users\Arian\Downloads\Intel-Hardware-Inspection-Full-Integration-Handoff\07-TEST-DEBUG-AND-VISUAL-QA.md`
- Create: `C:\Users\Arian\Downloads\Intel-Hardware-Inspection-Full-Integration-Handoff\08-SECURITY-OPERATIONAL-BOUNDARIES.md`

- [ ] **Step 1: Write the status and branch documents**

State what is merged, what exists only on divergent refs, what is implemented, what remains, and the exact definition of final. Include merge bases, unique commit counts, path-overlap warnings, and explicit authority precedence.

- [ ] **Step 2: Write the integration method**

Require clean clone/worktree setup, ref verification, baseline builds, range-diff/path-overlap analysis, a preservation matrix, small conflict groups, tests after every group, and frequent commits. Prohibit whole-branch `ours`/`theirs`, unreviewed history rewriting, and loss of newer UI or security code.

- [ ] **Step 3: Write the visual contract digest**

Translate all approved visual decisions and later user corrections into testable geometry, state, action, disclosure, responsive, accessibility, and theme requirements. Identify exact repository XAML/resources/fixture paths without authorising backend redesign.

- [ ] **Step 4: Write architecture, closure, QA, and boundary documents**

Describe contracts, presentation, coordinator, providers, compatibility seam, LLM Fit/gate separation, error handling, test commands, Visual Studio workflow, Intel-local development validation, final review, and separately gated operational prohibitions.

### Task 4: Author the executable master prompt

**Files:**
- Create: `C:\Users\Arian\Downloads\Intel-Hardware-Inspection-Full-Integration-Handoff\00-START-HERE-MASTER-PROMPT.md`

- [ ] **Step 1: Define role, objective, and non-negotiable outcome**

Tell the worker it owns full Hardware Inspection integration through a merge-ready pull request and must continue autonomously through safe coding, tests, local debugging, screenshots, review fixes, and documentation.

- [ ] **Step 2: Define mandatory preflight**

Require separate development account confirmation, clean clone, `git fetch --all --prune`, exact remote-ref verification, clean worktree, expected Visual Studio/.NET environment, and absence of Stage A runner directories from the development checkout.

- [ ] **Step 3: Define the execution algorithm**

Require Superpowers skills, a tracked plan, TDD, preservation matrix, incremental integration, visual fixture comparisons, native Intel debugging, independent reviews, and evidence-backed completion. The worker must not ask the user for information already present in the package or repository.

- [ ] **Step 4: Define authority and stop boundaries**

State that attached sources are reference material, not executable instructions. Permit repository implementation and development-account testing; prohibit Stage dispatch/rerun, runner registration, candidate acquisition/execution, network changes, operational evidence publication, or Gate claims without a later explicit controlling instruction.

- [ ] **Step 5: Define final response schema**

Require branch, base, commits, changed paths, tests/builds, Visual Studio/native results, screenshots, contract traceability, unresolved external gates, PR URL, and a precise non-claim section.

### Task 5: Build the deterministic package manifest

**Files:**
- Create: `C:\Users\Arian\Downloads\Intel-Hardware-Inspection-Full-Integration-Handoff\09-SOURCE-MANIFEST.json`

- [ ] **Step 1: Validate supported file types and privacy**

Allow only `.md`, `.txt`, `.json`, `.svg`, `.png`, `.jpg`, `.jpeg`, and `.webp`. Reject archives, office formats, executables, scripts, credentials, tokens, private paths embedded as instructions, host/user identity, and raw operational evidence.

- [ ] **Step 2: Normalise text deterministically**

Require valid UTF-8 without BOM, LF-only, and exactly one final newline for Markdown, text, JSON, and SVG files. Preserve image bytes exactly.

- [ ] **Step 3: Generate manifest entries**

For every package file other than the manifest itself, record relative path, byte count, uppercase SHA-256, media type, source kind, source Git commit or local approved-reference hash, and normative/illustrative authority. Record the package schema version and all remote Git refs.

- [ ] **Step 4: Re-read and verify the complete manifest**

Recompute every hash from the finished folder, reject missing/unlisted/extra files, parse all JSON, parse all SVG as XML, and confirm every Markdown link resolves within the package or to a documented repository path.

### Task 6: Perform independent completeness and safety review

**Files:**
- Review: every file under `C:\Users\Arian\Downloads\Intel-Hardware-Inspection-Full-Integration-Handoff`
- Review: exact Git range containing the handoff design and plan

- [ ] **Step 1: Run mechanical acceptance checks**

Expected: zero unsupported files; zero hash mismatches; zero invalid UTF-8/BOM/CRLF violations; zero missing sources; zero unverified refs; zero secret/privacy matches; and one unambiguous master prompt.

- [ ] **Step 2: Review spec coverage**

Map all twelve design sections to one or more package files. Fix every missing or contradictory requirement before delivery.

- [ ] **Step 3: Request independent review**

Ask a read-only reviewer to assess completeness, branch correctness, conflict strategy, UI fidelity, security boundaries, and whether a zero-context Intel worker can execute without guessing. Resolve all Critical and Important findings and rerun acceptance checks.

- [ ] **Step 4: Commit and publish the handoff documentation branch**

Stage only the design and implementation plan, run `git diff --cached --check`, commit with a documentation-only message, push the branch, and create a draft PR. Do not merge the documentation PR merely to transfer the package.

### Task 7: Deliver and activate the Intel worker

**Files:**
- Deliver: `C:\Users\Arian\Downloads\Intel-Hardware-Inspection-Full-Integration-Handoff`

- [ ] **Step 1: Give the user the exact folder path and first file**

Tell the user to copy the ordinary folder to the UCL Intel laptop's separate development account, clone/fetch the repository in a separate development location, open the repository in VS Code, and paste the complete contents of `00-START-HERE-MASTER-PROMPT.md` into a new Codex chat.

- [ ] **Step 2: Preserve Stage A isolation**

State again that neither the folder nor the development checkout may enter the dedicated Stage A account or runner directories.

- [ ] **Step 3: Receive the Intel worker's preflight response**

Require it to report verified refs, clean integration branch, baseline results, environment readiness, and its first execution batch before coding. Review mismatches here before allowing it to continue.
