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


def _sha256_json(value: object) -> str:
    encoded = json.dumps(
        value,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=True,
        allow_nan=False,
    ).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


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


def _campaign_inputs(
    tmp_path: Path,
    *,
    max_context: int = 512,
    fp16_terminal: bool = False,
) -> AdaptiveCampaignConfig:
    if fp16_terminal:
        terminal_receipt = tmp_path / "f16-terminal.json"
        _write_json(
            terminal_receipt,
            {
                "schema": "official-openvino-artifact-preparation-terminal/v1",
                "status": "artifact-preparation-terminal",
            },
        )
        inventory_path = tmp_path / "artifact-inventory.json"
        _write_json(
            inventory_path,
            {
                "schema": "official-openvino-adaptive-artifact-inventory/v1",
                "launch_reserve_mib": 4096,
                "emergency_floor_mib": 2048,
                "bindings": {
                    "u4": _binding(tmp_path, "u4"),
                    "u8": _binding(tmp_path, "u8"),
                    "f16": {
                        "precision": "f16",
                        "status": "artifact-preparation-terminal",
                        "artifact_id": None,
                        "model_root": None,
                        "manifest_path": None,
                        "manifest_sha256": None,
                        "terminal_stage": "artifact-preparation",
                        "terminal_receipt_path": str(terminal_receipt),
                        "terminal_receipt_sha256": _sha256(terminal_receipt),
                    },
                },
            },
        )
        matrix_path = tmp_path / "matrix.json"
        build_adaptive_comparison_matrix(
            artifact_inventory_path=inventory_path,
            historical_matrix_path=HISTORICAL_MATRIX,
            output_path=matrix_path,
        )
    else:
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
        "workload_job": {
            "setup_ok": True,
            "query_ok": True,
            "terminate_job_called": False,
            "queried_active_process_count_after_cleanup": 0,
            "survivor_pids_after_cleanup": [],
        },
        "sampler_job": {
            "setup_ok": True,
            "query_ok": True,
            "terminate_job_called": False,
            "queried_active_process_count_after_cleanup": 0,
            "survivor_pids_after_cleanup": [],
        },
    }
    path = config.campaign_root.parent / f"failure-{test_id}-{context}-{code}.json"
    _write_json(path, record)
    return MeasurementFailureRecord(
        role="pilot",
        record_path=path,
        record=record,
        fingerprint="task-three-sequence-identity",
    )


def _write_native_task_three_failure(
    config: AdaptiveCampaignConfig,
    campaign_root: Path,
    *,
    attempt_number: int,
    test_id: str = "OV-12",
    context: int = 4096,
    identity_artifact_id: str | None = None,
    include_convenience_identity: bool = False,
    launch_floor_bytes: int = START_RESERVE_MIB * 1024**2,
    emergency_floor_bytes: int = 2048 * 1024**2,
) -> Path:
    matrix_payload = json.loads(config.matrix_path.read_text(encoding="utf-8"))
    case = next(item for item in matrix_payload["cases"] if item["test_id"] == test_id)
    adaptive_spec_path = (
        config.spec_root / test_id / str(context) / "runtime-spec.json"
    )
    adaptive_spec = json.loads(adaptive_spec_path.read_text(encoding="utf-8"))
    role = "pilot"
    identity = {
        "config": {
            "device": adaptive_spec["device"],
            "max_new_tokens": adaptive_spec["max_new_tokens"],
            "expected_input_tokens": context,
            "ignore_eos": adaptive_spec["ignore_eos"],
            "seed": adaptive_spec["seed"],
            "apply_chat_template": adaptive_spec["apply_chat_template"],
            "properties": adaptive_spec["properties"],
        },
        "context": context,
        "matrix": {
            "path": str(config.matrix_path.resolve()),
            "file_sha256": _sha256(config.matrix_path),
            "schema_version": matrix_payload.get("schema_version"),
            "source_identity": matrix_payload["source_identity"],
            "build_identity": matrix_payload["build_identity"],
            "case": case,
        },
        "model": {
            "artifact_manifest_path": adaptive_spec["artifact_manifest_path"],
            "artifact_manifest_sha256": adaptive_spec[
                "artifact_manifest_sha256"
            ],
            "validated_artifact": {
                "artifact_id": (
                    identity_artifact_id
                    if identity_artifact_id is not None
                    else adaptive_spec["artifact_id"]
                ),
                "artifact_root": adaptive_spec["model_path"],
            },
        },
    }
    identity_hash = _sha256_json(identity)
    campaign_identity = {
        "schema": "official-openvino-wb04-campaign-identity/v1",
        "identity": identity,
        "campaign_identity_sha256": identity_hash,
    }
    identity_path = campaign_root / "campaign-identity.json"
    if identity_path.exists():
        assert json.loads(identity_path.read_text(encoding="utf-8")) == campaign_identity
    else:
        _write_json(identity_path, campaign_identity)

    role_spec = {
        "schema": "official-openvino-wb04-worker-spec/v1",
        "role": role,
        "controlled_test_id": test_id,
        "model_path": adaptive_spec["model_path"],
        "device": adaptive_spec["device"],
        "prompt": adaptive_spec["workload"]["prompt"],
        "context": context,
        "expected_input_tokens": context,
        "properties": adaptive_spec["properties"],
        "max_new_tokens": adaptive_spec["max_new_tokens"],
        "ignore_eos": adaptive_spec["ignore_eos"],
        "seed": adaptive_spec["seed"],
        "apply_chat_template": adaptive_spec["apply_chat_template"],
        "campaign_identity_sha256": identity_hash,
    }
    attempt_dir = (
        campaign_root
        / "attempts"
        / role
        / f"attempt-{attempt_number:03d}"
    )
    spec_path = attempt_dir / "spec.json"
    _write_json(spec_path, role_spec)
    record = {
        "schema": "official-openvino-wb04-governed-run/v1",
        "role": role,
        "run_nonce": f"native-failure-{attempt_number}",
        "worker": {
            "role": role,
            "controlled_test_id": test_id,
            "context": context,
            "expected_input_tokens": context,
            "device": adaptive_spec["device"],
            "model_path": adaptive_spec["model_path"],
        },
        "low_memory_stop": True,
        "exit_code": 137,
        "launch_minimum_available_ram_bytes": launch_floor_bytes,
        "emergency_minimum_available_ram_bytes": emergency_floor_bytes,
        "available_ram_bytes": {
            "before": 5000 * 1024**2,
            "minimum": 1900 * 1024**2,
            "after": 4500 * 1024**2,
        },
        "cleanup_process_count": 0,
        "residual_owned_process_count": 0,
        "emergency_actions": [],
        "os_instability": False,
        "workload_job": {
            "setup_ok": True,
            "query_ok": True,
            "terminate_job_called": True,
            "queried_active_process_count_after_cleanup": 0,
            "survivor_pids_after_cleanup": [],
        },
        "sampler_job": {
            "setup_ok": True,
            "query_ok": True,
            "terminate_job_called": True,
            "queried_active_process_count_after_cleanup": 0,
            "survivor_pids_after_cleanup": [],
        },
        "validation_errors": ["available RAM is below the emergency floor"],
    }
    if include_convenience_identity:
        record.update(
            {
                "controlled_test_id": test_id,
                "context_tokens": context,
                "artifact_id": case["artifact_id"],
                "artifact_manifest_sha256": case[
                    "artifact_manifest_sha256"
                ],
                "model": case["model"],
                "device": case["device"],
                "execution_route": case["execution_route"],
                "launch_minimum_available_ram_mib": START_RESERVE_MIB,
                "emergency_minimum_available_ram_mib": 2048,
            }
        )
    record_path = attempt_dir / "run" / "attempt.json"
    _write_json(record_path, record)
    root = campaign_root.resolve()
    receipt = {
        "schema": "official-openvino-wb04-sequence-receipt/v1",
        "role": role,
        "attempt_number": attempt_number,
        "campaign_identity_sha256": identity_hash,
        "spec_sha256": _sha256_json(role_spec),
        "spec_path": spec_path.resolve().relative_to(root).as_posix(),
        "spec_file_sha256": _sha256(spec_path),
        "runtime_record_path": record_path.resolve().relative_to(root).as_posix(),
        "runtime_record_sha256": _sha256(record_path),
        "accepted": False,
        "controller_error": "RuntimeError: guarded low-memory failure",
    }
    _write_json(attempt_dir / "sequence-receipt.json", receipt)
    return record_path


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
    for number in (1, 2):
        _write_native_task_three_failure(
            config,
            historical,
            attempt_number=number,
            test_id="OV-12",
            context=4096,
        )
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


def test_unsafe_boundary_persists_campaign_halt_across_resume(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    unsafe = _failure_record(config, cleanup=1)
    first_runner = _FakeRunner([unsafe])

    first = _run(config, first_runner)

    assert first["campaign_halt"]["reason"] == "unsafe-runtime-failure"
    assert eligible_steps(first) == ()
    assert campaign_status(config, first)["next_eligible_step"] is None

    resumed_runner = _FakeRunner([{} for _ in range(5)])
    resumed = _run(config, resumed_runner)

    assert resumed_runner.calls == []
    assert resumed["campaign_halt"] == first["campaign_halt"]
    with pytest.raises(RuntimeError, match="halted"):
        preflight_adaptive_campaign(
            config,
            available_ram=lambda: START_RESERVE_MIB * 1024**2,
        )


def test_resume_reopens_actual_artifact_inventory_bytes(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    load_or_create_state(config)
    inventory = tmp_path / "artifact-inventory.json"
    inventory.write_bytes(inventory.read_bytes() + b" ")

    with pytest.raises(ValueError, match="artifact inventory.*drift"):
        load_or_create_state(config)


@pytest.mark.parametrize(
    ("proof", "reason"),
    [
        ({}, "missing"),
        ({"query_ok": False, "active_pids": []}, "query"),
        ({"query_ok": True, "active_pids": [4321]}, "survivor"),
    ],
)
def test_live_campaign_job_proof_fails_closed_before_launch(
    tmp_path: Path,
    proof: dict[str, object],
    reason: str,
) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner([{} for _ in range(5)])

    result = run_adaptive_campaign(
        config,
        run_runtime=runner,
        run_quality=None,
        available_ram=lambda: START_RESERVE_MIB * 1024**2,
        owned_survivor_probe=lambda _: proof,
    )

    assert runner.calls == []
    assert reason in result["campaign_halt"]["reason"]


def test_live_zero_campaign_job_proof_is_required_for_every_launch(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner([{} for _ in range(5)])
    proof_calls: list[Path] = []

    def prove_zero(campaign_root: Path) -> dict[str, object]:
        proof_calls.append(campaign_root)
        return {
            "schema": "official-openvino-adaptive-job-probe/v1",
            "query_ok": True,
            "active_pids": [],
        }

    result = run_adaptive_campaign(
        config,
        run_runtime=runner,
        run_quality=None,
        available_ram=lambda: START_RESERVE_MIB * 1024**2,
        owned_survivor_probe=prove_zero,
    )

    assert len(runner.calls) == 5
    assert len(proof_calls) >= len(runner.calls)
    assert all(item["query_ok"] is True for item in result["safety_probes"])


def test_missing_task_three_job_cleanup_proof_is_unsafe(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    failure = _failure_record(config)
    record = dict(failure.record)
    record.pop("workload_job", None)
    record.pop("sampler_job", None)
    _write_json(failure.record_path, record)
    native = MeasurementFailureRecord(
        role=failure.role,
        record_path=failure.record_path,
        record=record,
        fingerprint=failure.fingerprint,
    )
    runner = _FakeRunner([native, {}])

    result = _run(config, runner)

    assert runner.calls == [("OV-11", 512)]
    assert result["steps"]["OV-11:512"]["runtime_status"] == "safety-boundary"


def test_boundary_builder_normalizes_native_task_three_identity_and_byte_floors(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path, max_context=4096)
    historical = tmp_path / "native-historical"
    for number in (1, 2):
        _write_native_task_three_failure(
            config,
            historical,
            attempt_number=number,
        )

    result = build_boundary_index(
        matrix_path=config.matrix_path,
        spec_root=config.spec_root,
        historical_root=historical,
        output_path=tmp_path / "native-boundaries.json",
    )

    assert len(result["boundaries"]) == 1
    boundary = result["boundaries"][0]
    assert (boundary["test_id"], boundary["context_tokens"]) == ("OV-12", 4096)
    assert boundary["identity"]["launch_reserve_mib"] == 4096
    assert boundary["identity"]["emergency_floor_mib"] == 2048


def test_boundary_builder_rejects_unbound_top_level_convenience_identity(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path, max_context=4096)
    historical = tmp_path / "mismatched-native-historical"
    for number in (1, 2):
        _write_native_task_three_failure(
            config,
            historical,
            attempt_number=number,
            identity_artifact_id="different-artifact",
            include_convenience_identity=True,
        )

    result = build_boundary_index(
        matrix_path=config.matrix_path,
        spec_root=config.spec_root,
        historical_root=historical,
        output_path=tmp_path / "mismatched-native-boundaries.json",
    )

    assert result["boundaries"] == []


def test_boundary_builder_rejects_convenience_mib_when_native_byte_floor_differs(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path, max_context=4096)
    historical = tmp_path / "wrong-byte-floor-historical"
    for number in (1, 2):
        _write_native_task_three_failure(
            config,
            historical,
            attempt_number=number,
            include_convenience_identity=True,
            launch_floor_bytes=START_RESERVE_MIB * 1024**2 + 1,
        )

    result = build_boundary_index(
        matrix_path=config.matrix_path,
        spec_root=config.spec_root,
        historical_root=historical,
        output_path=tmp_path / "wrong-byte-floor-boundaries.json",
    )

    assert result["boundaries"] == []


def test_artifact_preparation_terminal_is_receipted_checkpointed_and_resumable(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path, fp16_terminal=True)
    runner = _FakeRunner([{} for _ in range(4)])
    checkpoints: list[tuple[Path, bool]] = []

    result = _run(config, runner, checkpoints=checkpoints)

    assert len(runner.calls) == 4
    terminal = result["steps"]["OV-13:512"]
    assert terminal["runtime_status"] == "artifact-preparation-terminal"
    assert terminal["attempt_count"] == 0
    terminal_receipt = config.campaign_root / terminal["terminal_receipt_path"]
    assert terminal_receipt.is_file()
    assert _sha256(terminal_receipt) == terminal["terminal_receipt_sha256"]
    before = terminal_receipt.read_bytes()
    assert len(checkpoints) == 5

    resumed_runner = _FakeRunner([{}])
    resumed_checkpoints: list[tuple[Path, bool]] = []
    resumed = _run(
        config,
        resumed_runner,
        checkpoints=resumed_checkpoints,
    )

    assert resumed_runner.calls == []
    assert resumed_checkpoints == []
    assert terminal_receipt.read_bytes() == before
    assert resumed["steps"]["OV-13:512"] == terminal


def _zero_survivor_proof(_campaign_root: Path) -> dict[str, object]:
    return {
        "schema": "official-openvino-adaptive-job-probe/v1",
        "query_ok": True,
        "active_pids": [],
    }


class _CrashAfterNativeTaskThreeFailure:
    def __init__(self, config: AdaptiveCampaignConfig, attempt_number: int):
        self.config = config
        self.attempt_number = attempt_number
        self.calls: list[tuple[str, int]] = []

    def __call__(self, **kwargs: object) -> dict[str, object]:
        spec = json.loads(Path(kwargs["spec_path"]).read_text(encoding="utf-8"))
        test_id = spec["controlled_test_id"]
        context = spec["context_tokens"]
        self.calls.append((test_id, context))
        _write_native_task_three_failure(
            self.config,
            Path(kwargs["campaign_root"]),
            attempt_number=self.attempt_number,
            test_id=test_id,
            context=context,
        )
        raise KeyboardInterrupt("simulated crash before controller receipt")


def test_resume_reconciles_native_failure_persisted_before_controller_receipt(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    crash = _CrashAfterNativeTaskThreeFailure(config, 1)

    with pytest.raises(KeyboardInterrupt):
        run_adaptive_campaign(
            config,
            run_runtime=crash,
            run_quality=None,
            available_ram=lambda: START_RESERVE_MIB * 1024**2,
            owned_survivor_probe=_zero_survivor_proof,
        )

    resumed_runner = _FakeRunner([{} for _ in range(5)])
    resumed = run_adaptive_campaign(
        config,
        run_runtime=resumed_runner,
        run_quality=None,
        available_ram=lambda: START_RESERVE_MIB * 1024**2,
        owned_survivor_probe=_zero_survivor_proof,
    )

    assert resumed_runner.calls.count(("OV-11", 512)) == 1
    assert resumed["steps"]["OV-11:512"]["attempt_count"] == 2
    assert [
        item["attempt_number"]
        for item in resumed["steps"]["OV-11:512"]["attempts"]
    ] == [1, 2]


def test_two_unreceipted_native_failures_close_boundary_without_third_launch(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    first_crash = _CrashAfterNativeTaskThreeFailure(config, 1)
    with pytest.raises(KeyboardInterrupt):
        run_adaptive_campaign(
            config,
            run_runtime=first_crash,
            run_quality=None,
            available_ram=lambda: START_RESERVE_MIB * 1024**2,
            owned_survivor_probe=_zero_survivor_proof,
        )

    second_crash = _CrashAfterNativeTaskThreeFailure(config, 2)
    with pytest.raises(KeyboardInterrupt):
        run_adaptive_campaign(
            config,
            run_runtime=second_crash,
            run_quality=None,
            available_ram=lambda: START_RESERVE_MIB * 1024**2,
            owned_survivor_probe=_zero_survivor_proof,
        )

    forbidden_runner = _FakeRunner([{}])
    resumed = run_adaptive_campaign(
        config,
        run_runtime=forbidden_runner,
        run_quality=None,
        available_ram=lambda: START_RESERVE_MIB * 1024**2,
        owned_survivor_probe=_zero_survivor_proof,
    )

    assert forbidden_runner.calls.count(("OV-11", 512)) == 0
    step = resumed["steps"]["OV-11:512"]
    assert step["attempt_count"] == 2
    assert step["runtime_status"] == "boundary-confirmed"
