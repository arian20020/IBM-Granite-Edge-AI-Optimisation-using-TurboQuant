"""Policy and orchestration tests for the adaptive comparison controller."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

import pytest

from scripts.testing import build_official_openvino_boundary_index as boundary_cli
from scripts.testing import run_official_openvino_adaptive_comparison as campaign_cli
from scripts.testing.build_official_openvino_adaptive_matrix import (
    build_adaptive_comparison_matrix,
)
from scripts.testing.measure_official_openvino import (
    CampaignLock,
    MeasurementFailureRecord,
    MeasurementSequenceFailure,
)
from scripts.testing.official_openvino.adaptive_campaign_spec import (
    generate_adaptive_format_comparison_specs,
)
from scripts.testing.official_openvino.adaptive_campaign import (
    AdaptiveCampaignConfig,
    CANDIDATE_ORDER,
    CONTEXTS,
    START_RESERVE_MIB,
    build_boundary_index,
    build_ladder,
    campaign_status,
    eligible_steps,
    load_or_create_state,
    preflight_adaptive_campaign,
    run_adaptive_campaign,
)


ROOT = Path(__file__).resolve().parents[3]
HISTORICAL_MATRIX = ROOT / "experiments/manifests/official-openvino/retest-matrix.json"


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, sort_keys=True, separators=(",", ":")) + "\n",
        encoding="utf-8",
    )


def _binding(tmp_path: Path, precision: str) -> dict[str, object | None]:
    manifest = tmp_path / f"{precision}-manifest.json"
    manifest.write_text(f"{precision} manifest\n", encoding="utf-8")
    model_root = tmp_path / f"{precision}-model"
    model_root.mkdir()
    return {
        "precision": precision,
        "status": "available",
        "artifact_id": f"artifact-{precision}",
        "model_root": str(model_root),
        "manifest_path": str(manifest),
        "manifest_sha256": _sha256(manifest),
        "terminal_stage": None,
        "terminal_receipt_path": None,
        "terminal_receipt_sha256": None,
    }


@pytest.fixture
def matrix(tmp_path: Path) -> Path:
    inventory = tmp_path / "artifact-inventory.json"
    _write_json(
        inventory,
        {
            "schema": "official-openvino-adaptive-artifact-inventory/v1",
            "launch_reserve_mib": 4096,
            "emergency_floor_mib": 2048,
            "bindings": {
                precision: _binding(tmp_path, precision)
                for precision in ("u4", "u8", "f16")
            },
        },
    )
    output = tmp_path / "matrix.json"
    build_adaptive_comparison_matrix(
        artifact_inventory_path=inventory,
        historical_matrix_path=HISTORICAL_MATRIX,
        output_path=output,
    )
    return output


def _empty_state() -> dict[str, object]:
    return {"steps": {}}


def _passing_step(*, quality: str = "passed") -> dict[str, object]:
    return {
        "runtime_status": "passed",
        "quality_status": quality,
        "attempt_count": 1,
    }


def test_ladder_runs_breadth_first_in_expected_memory_order(matrix: Path) -> None:
    assert build_ladder(matrix)[:10] == (
        ("OV-11", 512),
        ("OV-TQ-22", 512),
        ("OV-TQ-21", 512),
        ("OV-12", 512),
        ("OV-13", 512),
        ("OV-11", 1024),
        ("OV-TQ-22", 1024),
        ("OV-TQ-21", 1024),
        ("OV-12", 1024),
        ("OV-13", 1024),
    )
    assert build_ladder(matrix) == tuple(
        (test_id, context)
        for context in CONTEXTS
        for test_id in CANDIDATE_ORDER
    )


def test_quality_blocked_does_not_stop_runtime_promotion() -> None:
    state = _empty_state()
    state["steps"]["OV-11:512"] = _passing_step(quality="quality-blocked")

    assert ("OV-11", 1024) in eligible_steps(state)


def test_confirmed_boundary_prunes_only_that_candidate() -> None:
    state = _empty_state()
    for test_id in CANDIDATE_ORDER:
        state["steps"][f"{test_id}:512"] = _passing_step()
    state["steps"]["OV-12:1024"] = {
        "runtime_status": "boundary-confirmed",
        "quality_status": "not-run",
        "attempt_count": 2,
    }
    state["steps"]["OV-11:1024"] = _passing_step()

    remaining = eligible_steps(state)

    assert ("OV-12", 2048) not in remaining
    assert ("OV-11", 2048) in remaining


def test_no_intermediate_level_can_be_skipped() -> None:
    state = _empty_state()
    state["steps"]["OV-11:512"] = _passing_step()

    assert ("OV-11", 1024) in eligible_steps(state)
    assert ("OV-11", 2048) not in eligible_steps(state)


def _campaign_inputs(tmp_path: Path, *, max_context: int = 512) -> AdaptiveCampaignConfig:
    matrix_path = matrix.__wrapped__(tmp_path)  # type: ignore[attr-defined]
    inventory_path = tmp_path / "artifact-inventory.json"
    build_root = tmp_path / "build"
    package = build_root / "openvino_genai"
    package.mkdir(parents=True)
    (package / "__init__.py").write_text("", encoding="utf-8")
    (package / "py_openvino_genai.pyd").write_bytes(b"extension")
    (package / "openvino_genai.dll").write_bytes(b"runtime")
    cache_root = tmp_path / "cache"
    cache_root.mkdir()
    spec_root = tmp_path / "specs"
    generate_adaptive_format_comparison_specs(
        matrix_path=matrix_path,
        build_root=build_root,
        artifact_inventory_path=inventory_path,
        cache_root=cache_root,
        output_root=spec_root,
    )
    build_provenance = tmp_path / "build-provenance.json"
    _write_json(build_provenance, {"schema": "test-build", "status": "passed"})
    python_executable = tmp_path / "python.exe"
    python_executable.write_bytes(b"python")
    site_packages = tmp_path / "site-packages"
    site_packages.mkdir()
    openvino_libraries = tmp_path / "openvino-libraries"
    openvino_libraries.mkdir()
    sampler = tmp_path / "sampler.ps1"
    sampler.write_text("# sampler\n", encoding="utf-8")
    return AdaptiveCampaignConfig(
        matrix_path=matrix_path,
        spec_root=spec_root,
        campaign_root=tmp_path / "campaign",
        build_root=build_root,
        build_provenance_path=build_provenance,
        python_executable=python_executable,
        python_site_packages=site_packages,
        openvino_libraries=openvino_libraries,
        sampler_script=sampler,
        max_context=max_context,
    )


def _failure_record(
    config: AdaptiveCampaignConfig,
    *,
    test_id: str = "OV-11",
    context: int = 512,
    category: str = "ram-floor",
    code: str = "RAM_FLOOR",
    cleanup: int = 0,
    survivors: int = 0,
    emergency_actions: list[object] | None = None,
    os_instability: bool = False,
    ram_query_succeeded: bool = True,
    launch_reserve_restored: bool = True,
) -> MeasurementFailureRecord:
    case_payload = json.loads(config.matrix_path.read_text(encoding="utf-8"))
    case = next(item for item in case_payload["cases"] if item["test_id"] == test_id)
    record = {
        "controlled_test_id": test_id,
        "context_tokens": context,
        "stage": "pilot",
        "failure_category": category,
        "failure_code": code,
        "artifact_manifest_sha256": case["artifact_manifest_sha256"],
        "artifact_id": case["artifact_id"],
        "model": case["model"],
        "device": case["device"],
        "execution_route": case["execution_route"],
        "launch_minimum_available_ram_mib": 4096,
        "emergency_minimum_available_ram_mib": 2048,
        "cleanup_process_count": cleanup,
        "residual_owned_process_count": survivors,
        "emergency_actions": emergency_actions or [],
        "os_instability": os_instability,
        "ram_query_succeeded": ram_query_succeeded,
        "launch_reserve_restored": launch_reserve_restored,
    }
    path = config.campaign_root.parent / f"failure-{test_id}-{context}-{code}.json"
    _write_json(path, record)
    return MeasurementFailureRecord(
        role="pilot",
        record_path=path,
        record=record,
        fingerprint="task-three-sequence-identity",
    )


class _FakeRunner:
    def __init__(self, outcomes: list[object]):
        self.outcomes = list(outcomes)
        self.calls: list[tuple[str, int]] = []

    def __call__(self, **kwargs: object) -> dict[str, object]:
        spec = json.loads(Path(kwargs["spec_path"]).read_text(encoding="utf-8"))
        test_id = spec["controlled_test_id"]
        context = spec["context_tokens"]
        self.calls.append((test_id, context))
        outcome = self.outcomes.pop(0) if self.outcomes else {}
        if isinstance(outcome, MeasurementFailureRecord):
            raise MeasurementSequenceFailure("guarded failure", outcome)
        result = {
            "schema": "official-openvino-wb04-attempt-sequence/v1",
            "accepted_sample_count": 3,
            "cleanup_process_count": 0,
            **dict(outcome),
        }
        evidence = Path(kwargs["campaign_root"]) / "attempt-sequence.json"
        _write_json(evidence, result)
        return result


def _run(
    config: AdaptiveCampaignConfig,
    runner: _FakeRunner,
    *,
    checkpoints: list[tuple[Path, bool]] | None = None,
) -> dict[str, object]:
    def publish(path: Path, final: bool) -> dict[str, object]:
        assert path.is_file()
        assert checkpoints is not None
        checkpoints.append((path, final))
        return {"published": True}

    return run_adaptive_campaign(
        config,
        run_runtime=runner,
        run_quality=None,
        publish_checkpoint=publish if checkpoints is not None else None,
        available_ram=lambda: START_RESERVE_MIB * 1024**2,
    )


def test_first_clean_guarded_failure_retries_same_context_once(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner([_failure_record(config), {}])

    result = _run(config, runner)

    assert runner.calls[:2] == [("OV-11", 512), ("OV-11", 512)]
    assert runner.calls.count(("OV-11", 512)) == 2
    assert result["steps"]["OV-11:512"]["runtime_status"] == "passed"
    assert result["steps"]["OV-11:512"]["attempt_count"] == 2


def test_two_matching_failures_confirm_boundary(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner([_failure_record(config), _failure_record(config)])

    result = _run(config, runner)

    step = result["steps"]["OV-11:512"]
    assert step["runtime_status"] == "boundary-confirmed"
    assert step["attempt_count"] == 2


def test_two_nonmatching_failures_are_inconclusive(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner(
        [_failure_record(config), _failure_record(config, code="FUNCTIONAL_OTHER")]
    )

    result = _run(config, runner)

    assert (
        result["steps"]["OV-11:512"]["runtime_status"]
        == "inconclusive-safety-boundary"
    )


@pytest.mark.parametrize(
    "changes",
    [
        {"cleanup": 1},
        {"survivors": 1},
        {"emergency_actions": [{"action": "terminate"}]},
        {"os_instability": True},
        {"ram_query_succeeded": False},
        {"launch_reserve_restored": False},
    ],
)
def test_unsafe_failure_prohibits_retry(
    tmp_path: Path, changes: dict[str, object]
) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner([_failure_record(config, **changes)])

    result = _run(config, runner)

    assert runner.calls == [("OV-11", 512)]
    assert result["steps"]["OV-11:512"]["runtime_status"] == "safety-boundary"


def test_resume_rejects_matrix_spec_inventory_or_build_drift(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    load_or_create_state(config)
    _write_json(config.build_provenance_path, {"schema": "changed", "status": "passed"})

    with pytest.raises(ValueError, match="drift"):
        load_or_create_state(config)


def test_controller_receipts_are_immutable_and_monotonic(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner([_failure_record(config), {}])

    result = _run(config, runner)

    receipts = result["steps"]["OV-11:512"]["attempts"]
    assert [item["attempt_number"] for item in receipts] == [1, 2]
    paths = [config.campaign_root / item["receipt_path"] for item in receipts]
    assert all(path.is_file() for path in paths)
    before = [path.read_bytes() for path in paths]
    resumed = _run(config, _FakeRunner([{} for _ in range(4)]))
    assert [path.read_bytes() for path in paths] == before
    assert resumed["steps"]["OV-11:512"] == result["steps"]["OV-11:512"]


def test_checkpoint_is_published_after_each_completed_or_terminal_step(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    checkpoints: list[tuple[Path, bool]] = []
    outcomes = [_failure_record(config), {}, {}, {}, {}, {}]

    result = _run(config, _FakeRunner(outcomes), checkpoints=checkpoints)

    assert len(checkpoints) == 5
    assert all(final is False for _, final in checkpoints)
    assert all(step["quality_status"] == "quality-blocked" for step in result["steps"].values())


def test_preflight_validates_and_does_not_launch_or_create_attempts(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)

    state = preflight_adaptive_campaign(
        config,
        available_ram=lambda: START_RESERVE_MIB * 1024**2,
    )
    status = campaign_status(config, state)

    assert status["next_eligible_step"] == ["OV-11", 512]
    assert not any(config.campaign_root.rglob("attempt-*"))


def test_explicit_equivalent_boundary_skips_dangerous_relaunch(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path, max_context=4096)
    historical = tmp_path / "historical"
    first = _failure_record(config, test_id="OV-12", context=4096)
    second = _failure_record(config, test_id="OV-12", context=4096)
    for number, failure in enumerate((first, second), 1):
        attempt = historical / f"attempt-{number:03d}" / "run" / "attempt.json"
        _write_json(attempt, failure.record)
        receipt = attempt.parents[1] / "sequence-receipt.json"
        _write_json(receipt, {"runtime_record_sha256": _sha256(attempt)})
    index_path = tmp_path / "boundary-index.json"
    build_boundary_index(
        matrix_path=config.matrix_path,
        spec_root=config.spec_root,
        historical_root=historical,
        output_path=index_path,
    )
    config = AdaptiveCampaignConfig(
        **{**config.__dict__, "reference_boundary_index": index_path}
    )
    runner = _FakeRunner([{} for _ in range(19)])

    result = _run(config, runner)

    assert ("OV-12", 4096) not in runner.calls
    assert result["steps"]["OV-12:4096"]["runtime_status"] == "boundary-confirmed"


def test_non_equivalent_boundary_evidence_is_rejected(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    index = tmp_path / "boundary-index.json"
    _write_json(
        index,
        {
            "schema": "official-openvino-adaptive-boundary-index/v1",
            "matrix_sha256": _sha256(config.matrix_path),
            "spec_index_sha256": _sha256(config.spec_root / "spec-index.json"),
            "boundaries": [
                {
                    "test_id": "OV-12",
                    "context_tokens": 512,
                    "failure_fingerprint": "f" * 64,
                    "matching_attempt_count": 2,
                    "identity": {"artifact_manifest_sha256": "0" * 64},
                    "attempts": [],
                }
            ],
        },
    )
    config = AdaptiveCampaignConfig(
        **{**config.__dict__, "reference_boundary_index": index}
    )

    with pytest.raises(ValueError, match="equivalent boundary"):
        load_or_create_state(config)


def test_campaign_cli_exposes_only_plan_listed_flags() -> None:
    parser = campaign_cli._parser()
    flags = {
        option
        for action in parser._actions
        for option in action.option_strings
        if option != "--help"
    }
    assert flags == {
        "-h",
        "--matrix",
        "--spec-root",
        "--campaign-root",
        "--build-root",
        "--build-provenance",
        "--python-executable",
        "--python-site-packages",
        "--openvino-libraries",
        "--sampler-script",
        "--reference-boundary-index",
        "--max-context",
        "--resume",
        "--publish-checkpoints",
        "--preflight-only",
    }
    max_context = next(action for action in parser._actions if action.dest == "max_context")
    assert tuple(max_context.choices) == CONTEXTS


def test_campaign_cli_preflight_prints_first_step_without_attempt(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    config = _campaign_inputs(tmp_path)
    monkeypatch.setattr(
        campaign_cli,
        "available_ram_bytes",
        lambda: START_RESERVE_MIB * 1024**2,
    )
    argv = [
        "--matrix", str(config.matrix_path),
        "--spec-root", str(config.spec_root),
        "--campaign-root", str(config.campaign_root),
        "--build-root", str(config.build_root),
        "--build-provenance", str(config.build_provenance_path),
        "--python-executable", str(config.python_executable),
        "--python-site-packages", str(config.python_site_packages),
        "--openvino-libraries", str(config.openvino_libraries),
        "--sampler-script", str(config.sampler_script),
        "--max-context", "512",
        "--preflight-only",
    ]

    assert campaign_cli.main(argv) == 0

    printed = json.loads(capsys.readouterr().out)
    assert printed["next_eligible_step"] == ["OV-11", 512]
    assert not any(config.campaign_root.rglob("attempt-*"))


def test_boundary_builder_cli_emits_valid_empty_index(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    config = _campaign_inputs(tmp_path)
    historical = tmp_path / "empty-historical"
    historical.mkdir()
    output = tmp_path / "empty-boundaries.json"

    assert boundary_cli.main(
        [
            "--matrix", str(config.matrix_path),
            "--spec-root", str(config.spec_root),
            "--historical-root", str(historical),
            "--output", str(output),
        ]
    ) == 0

    result = json.loads(output.read_text(encoding="utf-8"))
    assert result["boundaries"] == []
    assert json.loads(capsys.readouterr().out)["boundary_count"] == 0


def test_task_three_native_clean_ram_record_is_retryable(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    failure = _failure_record(config)
    record = dict(failure.record)
    for convenience in (
        "failure_category",
        "ram_query_succeeded",
        "launch_reserve_restored",
    ):
        record.pop(convenience)
    record.update(
        low_memory_stop=True,
        available_ram_bytes={
            "before": 5000 * 1024**2,
            "minimum": 1900 * 1024**2,
            "after": 4500 * 1024**2,
        },
        validation_errors=["available RAM is below the emergency floor"],
    )
    _write_json(failure.record_path, record)
    native = MeasurementFailureRecord(
        role=failure.role,
        record_path=failure.record_path,
        record=record,
        fingerprint=failure.fingerprint,
    )
    runner = _FakeRunner([native, {}])

    result = _run(config, runner)

    assert runner.calls[:2] == [("OV-11", 512), ("OV-11", 512)]
    assert result["steps"]["OV-11:512"]["runtime_status"] == "passed"


def test_resume_rejects_tampered_controller_receipt(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    result = _run(config, _FakeRunner([{} for _ in range(5)]))
    relative = result["steps"]["OV-11:512"]["attempts"][0]["receipt_path"]
    receipt = config.campaign_root / relative
    _write_json(receipt, {"schema": "tampered"})

    with pytest.raises(ValueError, match="receipt"):
        load_or_create_state(config)


def test_crash_after_clean_failure_resumes_with_monotonic_attempt_number(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner([_failure_record(config)])
    calls = 0

    def interrupted_ram() -> int:
        nonlocal calls
        calls += 1
        if calls == 2:
            raise KeyboardInterrupt("simulated controller interruption")
        return START_RESERVE_MIB * 1024**2

    with pytest.raises(KeyboardInterrupt):
        run_adaptive_campaign(
            config,
            run_runtime=runner,
            run_quality=None,
            available_ram=interrupted_ram,
        )

    first_receipt = (
        config.campaign_root
        / "controller-receipts"
        / "OV-11"
        / "512"
        / "attempt-001.json"
    )
    before = first_receipt.read_bytes()
    resumed = _run(config, _FakeRunner([{} for _ in range(5)]))

    assert first_receipt.read_bytes() == before
    assert resumed["steps"]["OV-11:512"]["attempt_count"] == 2
    assert (
        config.campaign_root
        / "controller-receipts"
        / "OV-11"
        / "512"
        / "attempt-002.json"
    ).is_file()


def test_preflight_obeys_whole_campaign_lock(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)

    with CampaignLock(config.campaign_root):
        with pytest.raises(RuntimeError, match="locked"):
            preflight_adaptive_campaign(
                config,
                available_ram=lambda: START_RESERVE_MIB * 1024**2,
            )


def test_preflight_rejects_hash_updated_runtime_property_tamper(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    spec_path = config.spec_root / "OV-11" / "512" / "runtime-spec.json"
    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    spec["properties"]["INFERENCE_NUM_THREADS"] = 2
    _write_json(spec_path, spec)
    index_path = config.spec_root / "spec-index.json"
    index = json.loads(index_path.read_text(encoding="utf-8"))
    entry = next(
        item
        for item in index["runtime_specs"]
        if item["test_id"] == "OV-11" and item["context_tokens"] == 512
    )
    entry["sha256"] = _sha256(spec_path)
    _write_json(index_path, index)

    with pytest.raises(ValueError, match="property"):
        preflight_adaptive_campaign(
            config,
            available_ram=lambda: START_RESERVE_MIB * 1024**2,
        )
