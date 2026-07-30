# OpenVINO Quality Capture Adapter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Capture and blindly adjudicate complete, real OpenVINO P1-P6 quality evidence for every accepted WB-04 generating configuration without weakening identity or process-safety controls.

**Architecture:** A focused quality campaign controller recomputes the accepted runtime campaign identity, writes a canonical worker spec, and runs one quality worker inside the existing Windows Job Object guard. The worker loads one pipeline and makes seven serial calls; the controller builds hash-bound capture records and the existing adjudicator projects them to blind reviewer input.

**Tech Stack:** Python 3.13 standard library, OpenVINO 2026.2.1, OpenVINO GenAI 2026.2.1.0, Windows Job Objects, guarded_build, pytest, JSON, SHA-256.

## Global Constraints

- Use one governed worker process tree at a time and one worker per accepted configuration.
- Construct exactly one LLMPipeline and execute P1-P5, P6 turn 1, and P6 turn 2 serially.
- Recompute exact campaign, model, build, worker-spec, runtime-config, prompt, rubric, and guard-evidence identity before acceptance.
- Require measured/accepted three-sample runtime evidence with zero cleanup survivors.
- Use the exact available-RAM floor 2_048 * 1024 * 1024 bytes and reject all floor, timeout, fallback, or cleanup failures.
- Enforce max_new_tokens 256, seed 42, do_sample false, and apply_chat_template false.
- Build P6 turn 2 from the actual unmodified P6 turn-1 output.
- Return raw text only from the worker; scoring remains outside the worker.
- Publish create-only evidence; resume rejects missing, duplicate, altered, mismatched, incomplete, or nonzero-cleanup state.
- Give reviewers only opaque labels and response content until the score sheet is hash-frozen.
- Leave P4 frozen and treat it as route-independent regression evidence, not configuration proof.

---

## File structure

- Create scripts/testing/official_openvino/quality_worker.py: one-pipeline seven-call raw-text worker.
- Create scripts/testing/official_openvino/quality_campaign.py: accepted-campaign validation, guarded launch, and resume.
- Modify scripts/testing/official_openvino/guarded_build.py: typed worker-environment injection and canonical environment-hash evidence for a governed command.
- Modify scripts/testing/run_official_openvino_quality.py: capture-record projection and focused campaign CLI.
- Modify scripts/testing/adjudicate_official_openvino_quality.py: capture-schema to blind-input collector.
- Create scripts/testing/tests/test_official_openvino_quality_worker.py.
- Create scripts/testing/tests/test_official_openvino_quality_campaign.py.
- Modify scripts/testing/tests/test_capture_official_openvino_quality.py.
- Modify scripts/testing/tests/test_adjudicate_official_openvino_quality.py.

### Task 1: Define the raw quality-worker protocol

**Files:**

- Create: scripts/testing/official_openvino/quality_worker.py
- Test: scripts/testing/tests/test_official_openvino_quality_worker.py

**Interfaces:**

- Consumes: Mapping[str, Any] with schema official-openvino-wb04-quality-worker-spec/v1, model_path, device, properties, and frozen P1-P6 executions.
- Produces: execute_quality_worker(spec: Mapping[str, Any]) -> dict[str, Any], schema official-openvino-wb04-quality-worker-result/v1, containing seven ordered outcomes and worker_result_sha256; CLI arguments --spec and --result atomically publish that result.

- [ ] **Step 1: Write failing tests**

    def test_worker_uses_one_pipeline_for_seven_ordered_calls(fake_openvino):
        result = execute_quality_worker(quality_worker_spec())
        assert fake_openvino.pipeline_count == 1
        assert fake_openvino.turn_ids == [
            "P1/turn_1", "P2/turn_1", "P3/turn_1", "P4/turn_1",
            "P5/turn_1", "P6/turn_1", "P6/turn_2",
        ]
        assert "Assistant: ACTUAL-SAVED" in fake_openvino.prompts[-1]
        assert result["worker_result_sha256"] == sha256_canonical(
            {key: value for key, value in result.items() if key != "worker_result_sha256"}
        )

    def test_worker_rejects_non_frozen_settings_before_pipeline_creation(fake_openvino):
        with pytest.raises(ValueError, match="max_new_tokens"):
            execute_quality_worker(quality_worker_spec(max_new_tokens=255))
        assert fake_openvino.pipeline_count == 0

- [ ] **Step 2: Run RED**

Run: pytest scripts/testing/tests/test_official_openvino_quality_worker.py -v

Expected: FAIL because quality_worker and execute_quality_worker do not exist.

- [ ] **Step 3: Implement minimal code**

    QUALITY_WORKER_SPEC = "official-openvino-wb04-quality-worker-spec/v1"

    def execute_quality_worker(spec: Mapping[str, Any]) -> dict[str, Any]:
        value = validate_quality_worker_spec(spec)
        pipeline = ov_genai.LLMPipeline(
            value["model_path"], value["device"], **value["properties"]
        )
        outcomes = generate_all_prompts(pipeline, value["prompt_contract"])
        return build_worker_result(outcomes)

validate_quality_worker_spec requires the four fixed settings, P1-P6 exactly once, and forbids score, rubric, private-label, codec, and ranking fields. generate_all_prompts creates one deterministic GenerationConfig, preserves raw text, and constructs P6 turn 2 with the actual turn-1 output.

- [ ] **Step 4: Run GREEN**

Run: pytest scripts/testing/tests/test_official_openvino_quality_worker.py -v

Expected: PASS.

- [ ] **Step 5: Commit**

    git add scripts/testing/official_openvino/quality_worker.py scripts/testing/tests/test_official_openvino_quality_worker.py
    git commit -m "feat(openvino): add raw quality worker contract"

### Task 2: Recompute accepted campaign identity

**Files:**

- Create: scripts/testing/official_openvino/quality_campaign.py
- Test: scripts/testing/tests/test_official_openvino_quality_campaign.py

**Interfaces:**

- Consumes: QualityCampaignInput containing campaign_root, spec_path, matrix_path, artifact_manifest_path, build_provenance_path, build_root, repo_root, python_executable, python_site_packages, openvino_libraries, sampler_script, prompt_set_path, rendered_root, rubric_path, output_root, and timeout_seconds.
- Produces: load_accepted_quality_campaign(input: QualityCampaignInput) -> AcceptedQualityCampaign and build_quality_worker_spec(campaign: AcceptedQualityCampaign) -> dict[str, Any].

- [ ] **Step 1: Write failing tests**

    def test_campaign_recomputes_identity_and_binds_model_build_runtime(tmp_path):
        campaign = load_accepted_quality_campaign(accepted_input(tmp_path))
        assert campaign.measurement_summary["accepted"] is True
        assert campaign.identity["campaign_identity_sha256"] == campaign.summary_campaign_sha256

    def test_changed_genai_dll_rejects_before_worker_spec_creation(tmp_path):
        source = accepted_input(tmp_path)
        source.runtime_dll.write_bytes(b"altered")
        with pytest.raises(ValueError, match="campaign identity"):
            load_accepted_quality_campaign(source)

    def test_runtime_summary_requires_three_samples_and_zero_cleanup(tmp_path):
        source = accepted_input(tmp_path)
        mutate_json(source.measurement_summary_path, "cleanup_process_count", 1)
        with pytest.raises(ValueError, match="cleanup"):
            load_accepted_quality_campaign(source)

- [ ] **Step 2: Run RED**

Run: pytest scripts/testing/tests/test_official_openvino_quality_campaign.py -k identity -v

Expected: FAIL because quality_campaign and load_accepted_quality_campaign do not exist.

- [ ] **Step 3: Implement minimal code**

    @dataclass(frozen=True)
    class AcceptedQualityCampaign:
        identity: Mapping[str, Any]
        measurement_summary: Mapping[str, Any]
        measurement_summary_sha256: str
        runtime_config_sha256: str
        summary_campaign_sha256: str
        worker_environment: Mapping[str, str]

    def load_accepted_quality_campaign(input: QualityCampaignInput) -> AcceptedQualityCampaign:
        identity = build_campaign_identity(**input.campaign_identity_arguments())
        require_persisted_identity(input.campaign_root, identity)
        summary, digest = load_accepted_summary(input.campaign_root)
        require_summary_matches_identity(summary, identity)
        return AcceptedQualityCampaign(
            identity=identity,
            measurement_summary=summary,
            measurement_summary_sha256=digest,
            runtime_config_sha256=summary["runtime_config_sha256"],
            summary_campaign_sha256=summary["campaign_identity_sha256"],
            worker_environment=build_worker_environment(**input.environment_arguments()),
        )

Use build_campaign_identity and build_worker_environment from measure_official_openvino. Require exact persisted identity equality and selected spec/matrix/model-manifest/build-provenance/GenAI module/DLL/device/properties/runtime-config equality.

- [ ] **Step 4: Run GREEN**

Run: pytest scripts/testing/tests/test_official_openvino_quality_campaign.py -k identity -v

Expected: PASS.

- [ ] **Step 5: Commit**

    git add scripts/testing/official_openvino/quality_campaign.py scripts/testing/tests/test_official_openvino_quality_campaign.py
    git commit -m "feat(openvino): bind quality capture to accepted campaign"

### Task 3: Govern one worker and bind execution evidence

**Files:**

- Modify: scripts/testing/official_openvino/quality_campaign.py
- Modify: scripts/testing/official_openvino/quality_worker.py
- Modify: scripts/testing/official_openvino/guarded_build.py
- Modify: scripts/testing/tests/test_official_openvino_quality_campaign.py
- Modify: scripts/testing/tests/test_official_openvino_guarded_build.py

**Interfaces:**

- Consumes: AcceptedQualityCampaign, output_root: Path, timeout_seconds: float, and run_command defaulting to guarded_build.run_guarded_command.
- Produces: run_governed_quality_worker(campaign: AcceptedQualityCampaign, output_root: Path, timeout_seconds: float) -> GovernedQualityWorkerResult with worker_result, worker_result_sha256, guard_evidence, and guard_evidence_sha256.

- [ ] **Step 1: Write failing tests**

    def test_governed_worker_uses_exact_ram_floor_and_zero_survivors(tmp_path):
        result = run_governed_quality_worker(accepted_campaign(tmp_path), tmp_path / "run", 1800)
        assert fake_guard.minimum_available_ram_bytes == 2_048 * 1024 * 1024
        assert result.guard_evidence["cleanup_process_count"] == 0

    def test_timeout_or_cleanup_survivor_is_rejected(tmp_path):
        fake_guard.returned["cleanup_process_count"] = 1
        with pytest.raises(RuntimeError, match="cleanup"):
            run_governed_quality_worker(accepted_campaign(tmp_path), tmp_path / "run", 1800)

    def test_guard_records_the_exact_worker_environment_hash(tmp_path):
        result = run_governed_quality_worker(accepted_campaign(tmp_path), tmp_path / "run", 1800)
        assert result.guard_evidence["environment_sha256"] == sha256_canonical(fake_guard.environment)

- [ ] **Step 2: Run RED**

Run: pytest scripts/testing/tests/test_official_openvino_quality_campaign.py -k governed -v

Expected: FAIL because run_governed_quality_worker does not exist.

- [ ] **Step 3: Implement minimal code**

    def run_governed_quality_worker(campaign, output_root, timeout_seconds, run_command=run_guarded_command):
        record = run_command(
            command=[str(campaign.python_executable), "-m",
                     "scripts.testing.official_openvino.quality_worker",
                     "--spec", str(spec_path), "--result", str(output_root / "worker-result.json")],
            cwd=campaign.repo_root,
            environment=campaign.worker_environment,
            log_path=output_root / "worker.log",
            evidence_path=output_root / "guard-evidence.json",
            expected_exit="zero",
            limits=GuardLimits(
                minimum_available_ram_bytes=2_048 * 1024 * 1024,
                maximum_runtime_seconds=timeout_seconds,
            ),
        )
        return validate_governed_quality_result(record)

First extend guarded_build.run_guarded_command and its private launch helper with environment: Mapping[str, str] | None. Require nonblank string keys and values, pass a copied mapping to subprocess creation, and record environment_sha256 from canonical JSON. Preserve existing callers when environment is None. Then quality_worker writes canonical worker-result JSON at the controller-assigned response path. Validation requires guard success, no timeout/fallback, exact result hash, matching environment hash, and zero survivors in the owned Job Object cleanup record.

- [ ] **Step 4: Run GREEN**

Run: pytest scripts/testing/tests/test_official_openvino_quality_campaign.py -k governed -v

Expected: PASS.

- [ ] **Step 5: Commit**

    git add scripts/testing/official_openvino/quality_campaign.py scripts/testing/official_openvino/quality_worker.py scripts/testing/official_openvino/guarded_build.py scripts/testing/tests/test_official_openvino_quality_campaign.py scripts/testing/tests/test_official_openvino_guarded_build.py
    git commit -m "feat(openvino): govern quality worker execution"

### Task 4: Publish capture records and safe resume state

**Files:**

- Modify: scripts/testing/run_official_openvino_quality.py
- Modify: scripts/testing/official_openvino/quality_campaign.py
- Modify: scripts/testing/tests/test_capture_official_openvino_quality.py
- Modify: scripts/testing/tests/test_official_openvino_quality_campaign.py

**Interfaces:**

- Consumes: AcceptedQualityCampaign, GovernedQualityWorkerResult, frozen prompt/rubric paths, and resume: bool.
- Produces: capture_governed_quality_campaign(input: QualityCampaignInput, resume: bool) -> dict[str, Any].

- [ ] **Step 1: Write failing tests**

    def test_capture_binds_guard_hash_and_actual_p6_history(tmp_path):
        result = capture_governed_quality_campaign(accepted_input(tmp_path), resume=False)
        p6 = read_json(tmp_path / "quality" / "P6" / "response.json")
        assert p6["guard_evidence_sha256"] == result["guard_evidence_sha256"]
        assert "Assistant: ACTUAL-SAVED" in p6["turn_prompts"][1]["raw_prompt"]

    def test_tampered_guard_evidence_rejects_resume_before_launch(tmp_path):
        capture_governed_quality_campaign(accepted_input(tmp_path), resume=False)
        mutate_json(tmp_path / "quality" / "governed-execution.json", "cleanup_process_count", 1)
        with pytest.raises(ValueError, match="guard evidence"):
            capture_governed_quality_campaign(accepted_input(tmp_path), resume=True)

- [ ] **Step 2: Run RED**

Run: pytest scripts/testing/tests/test_capture_official_openvino_quality.py scripts/testing/tests/test_official_openvino_quality_campaign.py -k "capture or resume" -v

Expected: FAIL because governed capture and guard-binding fields do not exist.

- [ ] **Step 3: Implement minimal code**

    def capture_governed_quality_campaign(input, *, resume):
        campaign = load_accepted_quality_campaign(input)
        governed = load_or_run_governed_quality_worker(campaign, resume=resume)
        records = build_capture_records_from_worker(campaign, governed)
        return publish_or_validate_capture(records, campaign, governed, resume=resume)

Extend capture record and summary validation with quality_worker_spec_sha256, worker_result_sha256, and guard_evidence_sha256. Preserve raw failures as captured-with-failures; do not create a score or a complete outcome from failed-turn evidence.

- [ ] **Step 4: Run GREEN**

Run: pytest scripts/testing/tests/test_capture_official_openvino_quality.py scripts/testing/tests/test_official_openvino_quality_campaign.py -k "capture or resume" -v

Expected: PASS.

- [ ] **Step 5: Commit**

    git add scripts/testing/run_official_openvino_quality.py scripts/testing/official_openvino/quality_campaign.py scripts/testing/tests/test_capture_official_openvino_quality.py scripts/testing/tests/test_official_openvino_quality_campaign.py
    git commit -m "feat(openvino): bind governed quality capture evidence"

### Task 5: Adapt capture evidence to blind adjudication

**Files:**

- Modify: scripts/testing/adjudicate_official_openvino_quality.py
- Modify: scripts/testing/tests/test_adjudicate_official_openvino_quality.py

**Interfaces:**

- Consumes: complete capture-summary roots plus frozen prompt/rubric paths.
- Produces: existing build_blind_scoring_input result, with opaque labels, content, turn hashes, and deterministic gates only.

- [ ] **Step 1: Write failing tests**

    def test_capture_projects_to_blind_input_without_private_identity(tmp_path):
        scoring = build_blind_scoring_input(
            raw_root=capture_root(tmp_path), prompt_set_path=PROMPT_SET,
            rendered_root=RENDERED, rubric_path=RUBRIC,
        )
        encoded = canonical_json(scoring).decode("utf-8")
        assert "campaign_identity_sha256" not in encoded
        assert "OV-TQ-03" not in encoded
        assert "TBQ" not in encoded

    def test_adapter_rejects_tampered_p6_capture_record(tmp_path):
        tamper_capture_record(capture_root(tmp_path), "P6", "record_sha256", "0" * 64)
        with pytest.raises(ValueError, match="record hash"):
            build_blind_scoring_input(
                raw_root=capture_root(tmp_path), prompt_set_path=PROMPT_SET,
                rendered_root=RENDERED, rubric_path=RUBRIC,
            )

- [ ] **Step 2: Run RED**

Run: pytest scripts/testing/tests/test_adjudicate_official_openvino_quality.py -k capture -v

Expected: FAIL because the collector accepts only legacy completion roots.

- [ ] **Step 3: Implement minimal code**

    def _collect_capture_responses(raw_root, contract, rubric):
        summaries = load_and_validate_capture_summaries(raw_root, contract, rubric)
        return project_capture_records_to_blind_rows(summaries, contract)

Dispatch existing _collect_raw_responses by validated artifact type. Require six records, capture/record/response hashes, P6 two-turn evidence, and guard binding. Project no private identity field. Keep existing score-sheet, objective-cap, identical-content equality, and unblinding code unchanged.

- [ ] **Step 4: Run GREEN**

Run: pytest scripts/testing/tests/test_adjudicate_official_openvino_quality.py -k capture -v

Expected: PASS.

- [ ] **Step 5: Commit**

    git add scripts/testing/adjudicate_official_openvino_quality.py scripts/testing/tests/test_adjudicate_official_openvino_quality.py
    git commit -m "feat(openvino): adapt captured responses for blind adjudication"

### Task 6: Add focused CLI and verify the boundary

**Files:**

- Modify: scripts/testing/run_official_openvino_quality.py
- Modify: scripts/testing/tests/test_official_openvino_quality_campaign.py

**Interfaces:**

- Consumes: every QualityCampaignInput path, frozen prompt/rubric paths, output_root, timeout_seconds, exact 2048 MiB floor, and resume.
- Produces: an immutable quality capture root accepted by the adjudication CLI.

- [ ] **Step 1: Write failing CLI tests**

    def test_quality_cli_requires_identity_paths_and_exact_ram_floor():
        with pytest.raises(SystemExit):
            parse_args(["--campaign-root", "campaign"])
        args = parse_args(full_quality_cli_args())
        assert args.minimum_available_ram_mib == 2048

- [ ] **Step 2: Run RED**

Run: pytest scripts/testing/tests/test_official_openvino_quality_campaign.py -k cli -v

Expected: FAIL because the quality CLI has only legacy executor-manifest arguments.

- [ ] **Step 3: Implement minimal code**

Add a mutually exclusive campaign mode with every QualityCampaignInput argument, timeout, RAM floor, and resume. Require the floor to equal 2048 MiB and dispatch only to capture_governed_quality_campaign. Preserve legacy mode for existing compatibility tests, but never use it for WB-04 real capture.

- [ ] **Step 4: Run full verification**

Run: pytest scripts/testing/tests/test_official_openvino_quality_worker.py scripts/testing/tests/test_official_openvino_quality_campaign.py scripts/testing/tests/test_capture_official_openvino_quality.py scripts/testing/tests/test_adjudicate_official_openvino_quality.py scripts/testing/tests/test_run_official_openvino_quality.py -v

Expected: PASS.

- [ ] **Step 5: Run non-inference syntax and help checks**

    python -m py_compile scripts/testing/official_openvino/quality_worker.py scripts/testing/official_openvino/quality_campaign.py scripts/testing/run_official_openvino_quality.py scripts/testing/adjudicate_official_openvino_quality.py
    python scripts/testing/run_official_openvino_quality.py --help
    python scripts/testing/adjudicate_official_openvino_quality.py --help

Expected: all commands exit 0 without model execution.

- [ ] **Step 6: Commit**

    git add scripts/testing/run_official_openvino_quality.py scripts/testing/tests/test_official_openvino_quality_campaign.py
    git commit -m "feat(openvino): add governed quality capture cli"

## Self-review

- Coverage: Tasks 1 and 3 enforce one pipeline, seven calls, frozen generation, actual P6 transcript, RAM floor, Job Object cleanup, and raw-text-only output. Tasks 2 and 4 bind campaign/model/build/runtime identity and reject altered resume state. Task 5 keeps review blind and preserves objective caps. Task 6 supplies operational entry and regression evidence.
- Placeholder scan: every task states files, types, tests, commands, implementation action, and expected outcome.
- Type consistency: AcceptedQualityCampaign is produced in Task 2 and consumed in Tasks 3-4; GovernedQualityWorkerResult is produced in Task 3 and consumed in Task 4; Task 4 capture roots feed Task 5; Task 6 dispatches the Task 4 entry point.
