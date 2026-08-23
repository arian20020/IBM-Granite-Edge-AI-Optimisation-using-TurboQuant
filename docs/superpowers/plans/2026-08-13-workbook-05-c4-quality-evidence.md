# Workbook 05 C4 Quality Evidence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a deterministic, blinded, traceable quality-evaluation pipeline for P1–P6 that preserves raw outputs, applies objective failures before subjective scoring, and prevents any evaluator from overriding a critical deterministic failure.

**Architecture:** Python modules first seal and hash raw outputs, then execute prompt-specific deterministic checks, apply rubric caps, prepare blinded A/B comparison packages, validate human or approved judge score records, calculate paired deltas, and trigger adjudication for disagreement or ranking reversal. A clean hosted workflow proves the evaluator using adversarial fixtures; C4 itself does not run a model.

**Tech Stack:** Python 3.12.10, JSON Schema Draft 2020-12, project controls `GTQ-PROMPTS-v1` and `GTQ-QUALITY-RUBRIC-v1`, SHA-256, GitHub Actions.

## Global Constraints

- The controlling prompt set remains `experiments/granite_turboquant_intel/prompts/fixed-feasibility-prompt-set-v1.json` with ID `GTQ-PROMPTS-v1`.
- The controlling rubric remains `experiments/granite_turboquant_intel/rubrics/quality-rubric-v1.json` with ID `GTQ-QUALITY-RUBRIC-v1`.
- Prompt IDs remain exactly `P1`, `P2`, `P3`, `P4`, `P5`, and `P6`.
- Generation defaults remain temperature `0.0`, top-p `1.0`, seed `42`, and maximum output tokens `256`, unless a later controlled prompt revision explicitly changes them.
- The five dimension names and weights remain exactly: correctness/grounding `0.30`, instruction/format `0.25`, completeness/fact retention `0.20`, relevance/clarity/coherence `0.15`, stability/output integrity `0.10`.
- Raw output bytes are written and SHA-256 hashed before any normalization, checking, blinding, scoring, or adjudication.
- Normalized text is supporting evidence only and never replaces raw output.
- Objective checks execute before subjective scoring.
- An objective failure is retained even when an evaluator believes the answer is useful.
- Empty output or unnecessary refusal scores `0`; corruption/repeated loop/critical truncation or wrong P6 remembered value caps at `2`; failed required format/schema, fabricated required fact, wrong exact answer, or missing critical required fact caps at `4` as directed by the controlling rubric.
- Baseline and candidate configuration labels are hidden from evaluators.
- Comparative scoring uses both presentation orders. Ranking reversal or evaluator disagreement greater than one point requires adjudication.
- Per-prompt results are always retained. An average may not hide different prompt failures.
- C4 does not select a model, run inference, prove activation, measure storage, or claim performance.
- The strict formal measured-run manifest remains unchanged.

## File Structure

```text
experiments/granite_turboquant_intel/schemas/workbook05/
  quality-result.schema.json
  deterministic-check-result.schema.json
  adjudication-record.schema.json

experiments/granite_turboquant_intel/manifests/templates/workbook05/
  quality-result-template.json
  deterministic-check-result-template.json
  adjudication-record-template.json

experiments/granite_turboquant_intel/configurations/workbook05/
  quality-evaluator-policy.json

scripts/testing/workbook05/phase3/
  quality_controls.py
  output_sealing.py
  quality_checks.py
  quality_caps.py
  blinding.py
  quality_scoring.py
  quality_bundle_validation.py

scripts/testing/workbook05/
  Invoke-Workbook05QualityEvaluation.ps1
  Validate-Workbook05-Phase3.ps1

tests/testing/workbook05/
  test_phase3_quality_contracts.py
  test_phase3_quality_controls.py
  test_phase3_output_sealing.py
  test_phase3_quality_checks_p1_p2.py
  test_phase3_quality_checks_p3_p4.py
  test_phase3_quality_checks_p5_p6.py
  test_phase3_quality_caps.py
  test_phase3_quality_blinding.py
  test_phase3_quality_scoring.py
  test_phase3_quality_bundle_validation.py
  test_phase3_quality_workflow_contract.py
  fixtures/phase3/quality/

.github/workflows/
  workbook-05-phase3-quality-tests.yml

docs/testing/workbook05/
  phase3-quality-evaluation-runbook.md
```

---

### Task 1: Define deterministic-check, quality-result, and adjudication contracts

**Files:**
- Create the three schemas and templates listed above
- Modify: `scripts/testing/workbook05/phase3/contracts.py`
- Create: `tests/testing/workbook05/test_phase3_quality_contracts.py`

**Interfaces:**
- Adds record types: `deterministic-check-result`, `quality-result`, `adjudication-record`
- Result states: `Passed`, `Failed`, `Not scored`, `Adjudication required`, `Adjudicated`

- [ ] **Step 1: Write failing schema tests**

```python
class Phase3QualityContractTests(unittest.TestCase):
    def test_quality_templates_validate(self) -> None:
        bindings = {
            "deterministic-check-result": "deterministic-check-result-template.json",
            "quality-result": "quality-result-template.json",
            "adjudication-record": "adjudication-record-template.json",
        }
        for record_type, filename in bindings.items():
            self.assertEqual([], validate_phase3_record(record_type, load_template(filename), REPOSITORY_ROOT))

    def test_passed_quality_requires_all_five_dimension_scores(self) -> None:
        payload = load_template("quality-result-template.json")
        payload["status"] = "Passed"
        payload["dimension_scores"]["stability_and_output_integrity"] = None
        self.assertNotEqual([], validate_phase3_record("quality-result", payload, REPOSITORY_ROOT))
```

Also reject a quality result without raw-output SHA-256, wrong prompt/rubric IDs, aggregate score outside 0–10, paired delta outside -10–10, hidden-label flag false during pairwise scoring, a deterministic failure omitted from critical caps, or adjudication marked complete without named evidence.

- [ ] **Step 2: Verify RED**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_quality_contracts`

Expected: missing schemas or unknown record types.

- [ ] **Step 3: Implement closed schemas**

A quality result must require:

```json
{
  "schema_version": "1.0",
  "campaign_id": "GTQ-WB05-MF-v1",
  "prompt_set_id": "GTQ-PROMPTS-v1",
  "prompt_id": "P2",
  "rubric_id": "GTQ-QUALITY-RUBRIC-v1",
  "run_id": "SMOKE-P2-M01",
  "raw_output_path": "outputs/P2.txt",
  "raw_output_sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "deterministic_result_path": "quality/P2-deterministic.json",
  "deterministic_failures": [],
  "critical_caps": [],
  "dimension_scores": {
    "correctness_and_grounding": 10.0,
    "instruction_and_format_adherence": 10.0,
    "completeness_and_fact_retention": 10.0,
    "relevance_clarity_and_coherence": 10.0,
    "stability_and_output_integrity": 10.0
  },
  "uncapped_weighted_score": 10.0,
  "applied_cap": null,
  "score_0_to_10": 10.0,
  "matched_baseline_run_id": "SMOKE-P2-BASE-M01",
  "paired_score_delta": 0.0,
  "judge_label_hidden": true,
  "pairwise_orders": ["baseline-first", "candidate-first"],
  "evaluator": {"type": "human", "identifier": "project-owner", "version": "1"},
  "status": "Passed",
  "material_degradation": "No",
  "stability_result": "Stable",
  "adjudication_path": "quality/P2-adjudication.json"
}
```

- [ ] **Step 4: Extend registry and verify GREEN**

Run the focused and shared contract suites; expect pass.

- [ ] **Step 5: Commit**

```powershell
git add experiments/granite_turboquant_intel/schemas/workbook05 experiments/granite_turboquant_intel/manifests/templates/workbook05 scripts/testing/workbook05/phase3/contracts.py tests/testing/workbook05/test_phase3_quality_contracts.py
git commit -m "test: define Phase 3 quality evidence contracts"
```

---

### Task 2: Load and verify the frozen prompt and rubric controls

**Files:**
- Create: `scripts/testing/workbook05/phase3/quality_controls.py`
- Create: `tests/testing/workbook05/test_phase3_quality_controls.py`
- Create: `experiments/granite_turboquant_intel/configurations/workbook05/quality-evaluator-policy.json`

**Interfaces:**
- Produces: `QualityControls`
- Produces: `load_quality_controls(repository_root: Path) -> QualityControls`
- Produces: `control_hashes() -> dict[str, str]`

- [ ] **Step 1: Write failing control-integrity tests**

Assert exact IDs, six unique prompts, generation defaults, five unique dimensions, weights summing to `1.0`, known anchors `0/2/4/6/8/10`, and complete SHA-256 hashes.

```python
def test_dimension_weights_sum_exactly_to_one(self) -> None:
    controls = load_quality_controls(REPOSITORY_ROOT)
    self.assertEqual(Decimal("1.00"), sum(item.weight for item in controls.dimensions))
```

Use `Decimal`, not binary float, for weight validation.

- [ ] **Step 2: Verify RED**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_quality_controls`

Expected: import failure.

- [ ] **Step 3: Implement immutable control loading**

Read with UTF-8 BOM tolerance, reject duplicate IDs/keys, validate expected content shape, and return the complete file SHA-256. Do not rewrite or normalize the frozen files.

- [ ] **Step 4: Add evaluator policy**

```json
{
  "schema_version": "1.0",
  "campaign_id": "GTQ-WB05-MF-v1",
  "prompt_set_id": "GTQ-PROMPTS-v1",
  "rubric_id": "GTQ-QUALITY-RUBRIC-v1",
  "required_pairwise_orders": ["baseline-first", "candidate-first"],
  "disagreement_threshold_points": 1.0,
  "ranking_reversal_requires_adjudication": true,
  "deterministic_failure_override_allowed": false,
  "aggregate_only_reporting_allowed": false
}
```

- [ ] **Step 5: Verify GREEN and commit**

Run focused tests and commit module, policy, and tests.

---

### Task 3: Seal raw output before evaluation

**Files:**
- Create: `scripts/testing/workbook05/phase3/output_sealing.py`
- Create: `tests/testing/workbook05/test_phase3_output_sealing.py`

**Interfaces:**
- Produces: `seal_output(source: Path, evidence_root: Path, prompt_id: str) -> SealedOutput`
- Produces fields: raw path, byte count, SHA-256, UTF-8 status, empty flag, newline convention, normalized supporting path/hash

- [ ] **Step 1: Write failing byte-preservation tests**

```python
def test_raw_bytes_are_hashed_before_newline_normalization(self) -> None:
    source = self.root / "source.txt"
    source.write_bytes(b"A\r\nB\r\n")
    sealed = seal_output(source, self.evidence, "P2")
    self.assertEqual(hashlib.sha256(b"A\r\nB\r\n").hexdigest(), sealed.raw_sha256)
    self.assertEqual("A\nB\n", sealed.normalized_path.read_text(encoding="utf-8"))
```

Also test BOM, invalid UTF-8, empty file, embedded NUL, missing source, and source path outside the run workspace.

- [ ] **Step 2: Verify RED**

Run focused suite; expect import failure.

- [ ] **Step 3: Implement seal-first behavior**

Copy raw bytes to a new immutable evidence path using exclusive creation. Hash the copied bytes and compare with source. Only then create a normalized UTF-8 supporting file. Invalid UTF-8 is `OutputIntegrityFailure`; no replacement characters are inserted.

- [ ] **Step 4: Verify GREEN and commit**

Run focused suite and commit.

---

### Task 4: Implement P1 and P2 deterministic checks

**Files:**
- Create: `scripts/testing/workbook05/phase3/quality_checks.py`
- Create: `tests/testing/workbook05/test_phase3_quality_checks_p1_p2.py`

**Interfaces:**
- Produces: `CheckFinding(code: str, passed: bool, expected: Any, observed: Any, evidence: str)`
- Produces: `evaluate_deterministic(prompt_id: str, output: str, controls: QualityControls, conversation_outputs: Mapping[str, str] | None = None) -> DeterministicDecision`

- [ ] **Step 1: Write failing P1 tests**

Cover exactly five Markdown bullet lines, maximum 89 words, case-sensitive exact required substrings `KV cache`, `offline`, and `memory`, extra prose before/after bullets, blank bullets, and Unicode word boundaries.

Semantic requirements “two benefits, two limitations, one check” are recorded as rubric review items, not falsely claimed as objective keyword checks.

- [ ] **Step 2: Write failing P2 tests**

Require exactly three non-empty lines in exact order with exact prefixes `BENEFIT:`, `LIMITATION:`, `CHECK:` and no title, Markdown fence, fourth line, or blank labelled value.

- [ ] **Step 3: Verify RED**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_quality_checks_p1_p2`

Expected: import failure.

- [ ] **Step 4: Implement pure prompt-specific functions**

```python
def check_p2(output: str) -> list[CheckFinding]:
    lines = [line for line in output.splitlines() if line.strip()]
    prefixes = ("BENEFIT:", "LIMITATION:", "CHECK:")
    findings = [finding("P2_NONEMPTY_LINE_COUNT", len(lines) == 3, 3, len(lines))]
    for index, prefix in enumerate(prefixes):
        observed = lines[index] if index < len(lines) else ""
        findings.append(finding(f"P2_PREFIX_{index + 1}", observed.startswith(prefix), prefix, observed))
        findings.append(finding(f"P2_VALUE_{index + 1}", bool(observed[len(prefix):].strip()) if observed.startswith(prefix) else False, "non-empty", observed))
    return findings
```

No network, model, embedding, or judge call is allowed in deterministic functions.

- [ ] **Step 5: Verify GREEN and commit**

Run focused suite and commit.

---

### Task 5: Implement P3 and P4 deterministic checks

**Files:**
- Modify: `scripts/testing/workbook05/phase3/quality_checks.py`
- Create: `tests/testing/workbook05/test_phase3_quality_checks_p3_p4.py`

- [ ] **Step 1: Write failing P3 tests**

Require raw output to parse as JSON without Markdown fences; exact top-level keys `optimisation`, `memory_effect`, `quality_risk`, `verification`; exact value `TurboQuant`; other values non-empty short strings; no arrays, nested objects, duplicate JSON keys, NaN, Infinity, or trailing text.

Use `object_pairs_hook` to reject duplicate keys.

- [ ] **Step 2: Write failing P4 tests**

Require exactly two sentences and explicit retention of:

```text
IBM Granite 4.1 3B
upstream llama.cpp
Q4_K_M
Q8_0 for K
Q8_0 for V
4096 tokens
local Windows Intel execution
TurboQuant not active
```

Use a reviewed alias table only for grammatical variants such as `K and V caches both use Q8_0`. Unsupported-fact detection is marked for semantic review; deterministic code does not claim to detect every hallucination.

- [ ] **Step 3: Verify RED**

Run focused suite and expect failures for missing behavior.

- [ ] **Step 4: Implement strict JSON and sentence/fact checks**

Sentence segmentation for P4 is a bounded project rule over `.`, `?`, and `!` after removing known abbreviations; record the normalized segmentation evidence so disputes are reviewable.

- [ ] **Step 5: Verify GREEN and commit**

Run focused suite and commit.

---

### Task 6: Implement P5 and P6 exact checks

**Files:**
- Modify: `scripts/testing/workbook05/phase3/quality_checks.py`
- Create: `tests/testing/workbook05/test_phase3_quality_checks_p5_p6.py`

- [ ] **Step 1: Write failing P5 tests**

Only the exact one-line output `MARKER:IXN-TQ-7319` passes. Reject leading/trailing explanation, Markdown, case changes, additional whitespace-only lines beyond final newline policy, or a marker copied from an earlier context paragraph.

- [ ] **Step 2: Write failing P6 tests**

Require turn-one raw output `SAVED` and turn-two raw output `amber:4821`. Preserve and hash each turn independently. Wrong remembered value is a stability critical failure capped at `2`.

- [ ] **Step 3: Verify RED**

Run focused suite; expect failures.

- [ ] **Step 4: Implement exact comparison without `.strip()` hiding extra text**

Permit one conventional final line ending but no leading/trailing spaces or extra lines. Compare canonical bytes after removing only one final `\r\n` or `\n`.

- [ ] **Step 5: Verify GREEN and commit**

Run focused suite and all P1–P6 check suites; commit.

---

### Task 7: Apply rubric caps without overriding deterministic evidence

**Files:**
- Create: `scripts/testing/workbook05/phase3/quality_caps.py`
- Create: `tests/testing/workbook05/test_phase3_quality_caps.py`

**Interfaces:**
- Produces: `apply_caps(prompt_id: str, findings: Sequence[CheckFinding], dimension_scores: Mapping[str, Decimal]) -> CappedScore`
- Produces: uncapped weighted score, applicable caps, strictest cap, final score

- [ ] **Step 1: Write failing cap tests**

```python
def test_perfect_subjective_scores_cannot_override_failed_json_schema(self) -> None:
    findings = (failed("P3_VALID_JSON"),)
    scores = perfect_dimension_scores()
    result = apply_caps("P3", findings, scores)
    self.assertEqual(Decimal("4.0"), result.final_score)
    self.assertIn("FORMAT_SCHEMA_CAP_4", result.cap_codes)
```

Cover empty/refusal `0`, P6 wrong memory `2`, severe corruption `2`, format/fact/exact-answer failure `4`, and multiple caps selecting the lowest.

- [ ] **Step 2: Verify RED**

Run focused suite; expect import failure.

- [ ] **Step 3: Implement Decimal-weighted scoring**

```python
weighted = sum(scores[name] * weights[name] for name in weights)
final = min((weighted, *applicable_caps)) if applicable_caps else weighted
```

Quantize display scores to one decimal using `ROUND_HALF_UP`; retain full Decimal string in evidence.

- [ ] **Step 4: Verify GREEN and commit**

Run focused suite and commit.

---

### Task 8: Create deterministic blinded A/B packages in both orders

**Files:**
- Create: `scripts/testing/workbook05/phase3/blinding.py`
- Create: `tests/testing/workbook05/test_phase3_quality_blinding.py`

**Interfaces:**
- Produces: `create_blinded_pair(baseline: SealedOutput, candidate: SealedOutput, prompt_id: str, seed_hex: str, output_root: Path) -> BlindedPackage`
- Produces public evaluator files with labels `A` and `B`
- Produces sealed mapping file stored outside evaluator package

- [ ] **Step 1: Write failing blinding tests**

Assert that public files contain no run ID, configuration ID, algorithm, precision, path, or filename revealing baseline/candidate identity. Require two orders and deterministic reproducibility from a recorded seed.

- [ ] **Step 2: Verify RED**

Run focused suite; expect import failure.

- [ ] **Step 3: Implement two-order package generation**

Create:

```text
evaluator/order-1/A.txt
evaluator/order-1/B.txt
evaluator/order-1/instructions.json
evaluator/order-2/A.txt
evaluator/order-2/B.txt
evaluator/order-2/instructions.json
sealed/mapping.json
```

Order two reverses order one. `mapping.json` contains raw-output hashes, not copied text, and is excluded from the evaluator directory.

- [ ] **Step 4: Verify GREEN and commit**

Run focused suite and commit.

---

### Task 9: Validate evaluator scores, calculate paired deltas, and require adjudication

**Files:**
- Create: `scripts/testing/workbook05/phase3/quality_scoring.py`
- Create: `tests/testing/workbook05/test_phase3_quality_scoring.py`

**Interfaces:**
- Produces: `validate_score_record(record, controls, deterministic_decision) -> ScoreDecision`
- Produces: `compare_orders(order_one, order_two, mapping) -> PairwiseDecision`
- Produces: `requires_adjudication(...) -> tuple[bool, tuple[str, ...]]`

- [ ] **Step 1: Write failing score-record tests**

Reject missing dimension, score not on 0–10 range, unknown evaluator, unhidden labels, deterministic override, wrong control hash, score arithmetic mismatch, or baseline/candidate run mismatch.

- [ ] **Step 2: Write failing adjudication tests**

Adjudication is required when:

```text
absolute overall evaluator disagreement > 1.0
order-one winner differs from order-two winner
any deterministic critical failure exists
one evaluator reports material degradation and another does not
raw-output hash changed after blinding
```

- [ ] **Step 3: Verify RED**

Run focused suite; expect import failure.

- [ ] **Step 4: Implement comparison and delta**

Paired delta is `candidate final score - matched baseline final score`. Retain both presentation-order judgments and never average away a reversal. `material_degradation` remains `Moderate - adjudication required` until a signed adjudication record exists.

- [ ] **Step 5: Verify GREEN and commit**

Run focused suite and commit.

---

### Task 10: Add the evaluation orchestrator and untrusted-bundle validator

**Files:**
- Create: `scripts/testing/workbook05/Invoke-Workbook05QualityEvaluation.ps1`
- Create: `scripts/testing/workbook05/phase3/quality_bundle_validation.py`
- Create: `tests/testing/workbook05/test_phase3_quality_bundle_validation.py`
- Create: `tests/testing/workbook05/fixtures/phase3/quality/bundles/`

**Interfaces:**
- Orchestrator stages: seal, deterministic, blind, ingest scores, cap, compare, adjudicate, manifest
- Validator: `validate_quality_bundle(bundle_root: Path, repository_root: Path) -> list[BundleIssue]`

- [ ] **Step 1: Write failing adversarial bundle tests**

Reject changed raw output, normalized hash substituted for raw hash, missing deterministic record, score above a critical cap, public mapping leak, one-order-only comparison, aggregate without per-prompt records, silent ranking reversal, missing adjudication, secret or binary payload, unsafe path, or changed prompt/rubric hash.

- [ ] **Step 2: Verify RED**

Run focused suite; expect missing validator.

- [ ] **Step 3: Implement PowerShell orchestration with atomic records**

PowerShell calls Python modules by executable plus argument array. Human scoring input is a reviewed JSON file; the orchestrator never edits it to make validation pass. A validation failure preserves raw evidence and returns nonzero.

- [ ] **Step 4: Implement hosted validator**

Validate schemas, manifest, payload policy, raw hashes, deterministic findings, cap arithmetic, blinding leak policy, both orders, paired relation, and adjudication. Never execute or import bundle files.

- [ ] **Step 5: Verify GREEN and commit**

Run valid and adversarial fixture suites; commit.

---

### Task 11: Add hosted C4 workflow, gate integration, and runbook

**Files:**
- Create: `.github/workflows/workbook-05-phase3-quality-tests.yml`
- Create: `tests/testing/workbook05/test_phase3_quality_workflow_contract.py`
- Modify: `scripts/testing/Validate-Workbook05-Phase3.ps1`
- Create: `docs/testing/workbook05/phase3-quality-evaluation-runbook.md`

**Interfaces:**
- Hosted producer artifact: `workbook-05-phase3-quality-fixtures-${{ github.run_id }}-${{ github.run_attempt }}`
- No self-hosted or model job in C4 workflow

- [ ] **Step 1: Write failing workflow tests**

Require Windows-hosted jobs, exact-head checkout, pinned actions, read-only permissions, producer/validator separation, same-attempt artifact, full Phase 3 gate first, no model/network/judge API call, and no repository write.

- [ ] **Step 2: Verify RED**

Run workflow contract; expect missing workflow.

- [ ] **Step 3: Implement fixture producer and hosted validator**

The producer evaluates curated valid/failing outputs for every prompt and creates blinded comparison fixtures. The validator independently checks the artifact as data.

- [ ] **Step 4: Extend the Phase 3 gate**

Add every C4 Python test and verify the final success marker remains last.

- [ ] **Step 5: Write the runbook**

Explain objective versus subjective checks, raw-versus-normalized evidence, caps, blinding, both presentation orders, paired deltas, adjudication, and why an AI judge cannot override deterministic failure.

- [ ] **Step 6: Verify GREEN and commit**

```powershell
python -m unittest -v tests.testing.workbook05.test_phase3_quality_workflow_contract
& '.\scripts\testing\Validate-Workbook05-Phase3.ps1' -PythonPath 'python'
git add .github/workflows/workbook-05-phase3-quality-tests.yml tests/testing/workbook05/test_phase3_quality_workflow_contract.py scripts/testing/Validate-Workbook05-Phase3.ps1 docs/testing/workbook05/phase3-quality-evaluation-runbook.md
git commit -m "ci: verify Phase 3 quality evidence"
```

---

### Task 12: Run complete C4 verification and prepare the package PR

- [ ] **Step 1: Run all C4 suites**

```powershell
python -m unittest -v `
  tests.testing.workbook05.test_phase3_quality_contracts `
  tests.testing.workbook05.test_phase3_quality_controls `
  tests.testing.workbook05.test_phase3_output_sealing `
  tests.testing.workbook05.test_phase3_quality_checks_p1_p2 `
  tests.testing.workbook05.test_phase3_quality_checks_p3_p4 `
  tests.testing.workbook05.test_phase3_quality_checks_p5_p6 `
  tests.testing.workbook05.test_phase3_quality_caps `
  tests.testing.workbook05.test_phase3_quality_blinding `
  tests.testing.workbook05.test_phase3_quality_scoring `
  tests.testing.workbook05.test_phase3_quality_bundle_validation `
  tests.testing.workbook05.test_phase3_quality_workflow_contract
```

Expected: pass.

- [ ] **Step 2: Run complete Phase 3 gate**

```powershell
& '.\scripts\testing\Validate-Workbook05-Phase3.ps1' -PythonPath 'python'
```

Expected final line: `WORKBOOK05_PHASE3_GATE_PASS`.

- [ ] **Step 3: Review every adversarial fixture**

Confirm each mutation fails for its intended reason rather than because the fixture is malformed elsewhere. Remove no failing case merely to simplify the validator.

- [ ] **Step 4: Run repository integrity checks**

```powershell
git diff --check
git status --short
```

Expected: clean working tree after commits and no generated evaluator bundle tracked.

- [ ] **Step 5: Open the C4 PR with full evaluation context**

Include prompt-by-prompt checks, rubric arithmetic, cap table, blinding design, adjudication triggers, fixture RED/GREEN evidence, and the exact statement that C4 does not evaluate a real model yet.

- [ ] **Step 6: Verify exact final head in normal and quality workflows before merge**

Record both run IDs and ensure no unresolved review thread remains.

## C4 Acceptance Gate

C4 is accepted only when:

- raw bytes are sealed and hashed before evaluation;
- every P1–P6 objective check has valid and adversarial tests;
- semantic requirements are not misrepresented as simple keyword checks;
- critical caps always dominate subjective scores;
- baseline/candidate labels are absent from evaluator packages;
- both presentation orders are retained;
- disagreement and reversal trigger adjudication;
- per-prompt evidence cannot be replaced by an average;
- a clean hosted runner validates the complete text-only fixture artifact;
- the formal measured-run schema remains strict and unchanged;
- no model, activation, storage, performance, or real quality claim is authorised by C4 alone.

## Textbook Basis

- *AI Engineering*, Chapters 3 and 4: exact evaluation, AI-as-judge limitations, comparative evaluation, component-level criteria, and explicit evaluation pipelines.
- *The Art of Unit Testing*, Chapters 7–10: trustworthy, maintainable, readable tests and a layered test recipe.
- *Code Complete*, Chapters 20–23 and 28: complementary quality techniques, developer testing, debugging evidence, and measurement records.
- *Designing Secure Software*, Chapters 4, 10, and 12: fail-secure defaults, untrusted input, and adversarial validation.
- *Why Programs Fail*, Chapters 8, 10, and 11: observation, executable expectations, and anomaly detection.
- *Systems Engineering: Principles and Practice*, Chapters 6 and 17: requirements-to-test traceability and controlled evaluation evidence.
- *Fundamentals of Software Architecture*, Chapter 6: the evaluator tests act as fitness functions that prevent erosion of the quality-claim boundary.