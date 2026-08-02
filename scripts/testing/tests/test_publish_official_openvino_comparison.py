"""Publication tests for governed WB-04 adaptive-comparison checkpoints."""

from __future__ import annotations

import csv
import hashlib
import io
import json
import shutil
import subprocess
import sys
import zipfile
from dataclasses import replace
from pathlib import Path
from types import SimpleNamespace

import pytest

from scripts.testing.publish_official_openvino_comparison import (
    PUBLICATION_PATHS,
    PublicationInputs,
    build_publication_bundle,
    publish_reconciled_checkpoint,
    register_rows_from_release,
    rewrite_csv_slice_atomically,
    validate_published_bundle,
)
from scripts.testing.official_openvino.comparison_reconcile import (
    BoundaryOutcome,
    ComparisonKey,
    validate_closed_campaign,
    validate_complete_release,
)
from scripts.testing.official_openvino.matrix import COMPARISON_RUN_ORDER
from scripts.testing.tests.test_finalize_official_openvino_comparison_workbook import (
    complete_release as _task8_complete_release,
    incomplete_release as _task8_incomplete_release,
)


MATRIX_SHA256 = "2" * 64
RECONCILIATION_SHA256 = "9" * 64
EVIDENCE_COMMIT = "a" * 40
MATRIX_PATH = Path("experiments/synthetic/adaptive-comparison.json")
ROOT = Path(__file__).resolve().parents[3]
SOURCE_TEMPLATE = (
    "docs/testing/workbooks/text-templates/"
    "04_Official_OpenVINO_Controlled_Retest_Workbook_v1.md"
)
GENERATED_DOCX = (
    "docs/testing/workbooks/generated/"
    "04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx"
)
HISTORICAL_MATRIX_SHA256 = (
    "7db2636b403d285aa3886c9f23560a9e48bd43645e9aca35e0d1fc4f16eaea42"
)


def _with_split_gpu_memory(release):
    """Give Task 9's synthetic releases the split GPU receipts real samples expose."""

    runtime = {}
    for key, outcome in release.runtime.items():
        samples = []
        for index, original in enumerate(outcome.samples):
            sample = dict(original)
            sample.setdefault("gpu_dedicated_memory_peak_mb", float(index + 1))
            sample.setdefault("gpu_shared_memory_peak_mb", float(index + 4))
            sample.setdefault("num_input_tokens", 20)
            sample.setdefault("num_generated_tokens", 10)
            samples.append(sample)
        runtime[key] = replace(outcome, samples=tuple(samples))
    return replace(release, runtime=runtime)


def complete_release():
    return _with_split_gpu_memory(_task8_complete_release())


def incomplete_release():
    return _with_split_gpu_memory(_task8_incomplete_release())


def _bind_matrix_sha(release, digest: str):
    runtime = {
        key: replace(
            outcome,
            identity_hashes={**outcome.identity_hashes, "matrix_sha256": digest},
        )
        for key, outcome in release.runtime.items()
    }
    return replace(release, runtime=runtime)


def closed_complete_release():
    release = complete_release()
    terminals = dict(release.terminals)
    additions = {
        ComparisonKey("OV-12", 1024): "OV-12 terminal",
        ComparisonKey("OV-13", 1024): "OV-13 terminal",
        ComparisonKey("OV-TQ-21", 1024): "OV-TQ-21 terminal",
        ComparisonKey("OV-TQ-22", 2048): "OV-TQ-22 terminal",
    }
    for key, reason in additions.items():
        terminals[key] = {
            "test_id": key.test_id,
            "context_tokens": key.context_tokens,
            "stage": "runtime-boundary",
            "principal_reason": reason,
            "evidence_path": Path(
                f"experiments/synthetic/terminal/{key.test_id}-{key.context_tokens}.json"
            ),
            "evidence_sha256": hashlib.sha256(reason.encode()).hexdigest(),
        }
    return replace(release, terminals=terminals)


def matrix_cases() -> tuple[SimpleNamespace, ...]:
    contexts = (512, 1024, 2048, 4096, 8192)
    identities = (
        ("OV-11", "u4", "standard", "f16", "f16", "CPU"),
        ("OV-12", "u8", "standard", "f16", "f16", "CPU"),
        ("OV-13", "f16", "standard", "f16", "f16", "CPU"),
        ("OV-TQ-21", "u8", "tbq4", "u4", "u4", "CPU"),
        ("OV-TQ-22", "u8", "tbq3", "u3", "u3", "CPU"),
    )
    return tuple(
        SimpleNamespace(
            test_id=test_id,
            description=f"Adaptive comparison {test_id}",
            model="ibm-granite/granite-3.3-2b-instruct",
            weight_precision=weight_precision,
            k_algorithm=algorithm,
            v_algorithm=algorithm,
            k_precision=k_precision,
            v_precision=v_precision,
            device=device.lower(),
            requested_device=device,
            contexts=contexts,
            guard="ram-2048-mib",
            quality_required=True,
            runtime_key_algorithm=algorithm.upper(),
            runtime_value_algorithm=algorithm.upper(),
            execution_route=(
                "patched-stateful" if algorithm != "standard" else "stateful-standard"
            ),
            attention_path=(
                "stateful_sdpa_reference_codec"
                if algorithm != "standard"
                else "stateful_sdpa_standard"
            ),
        )
        for test_id, weight_precision, algorithm, k_precision, v_precision, device
        in identities
    )


def projected(release=None):
    return register_rows_from_release(
        release or complete_release(),
        matrix_cases=matrix_cases(),
        matrix_path=MATRIX_PATH,
        matrix_sha256=MATRIX_SHA256,
        evidence_commit=EVIDENCE_COMMIT,
        reconciliation_input_sha256=RECONCILIATION_SHA256,
    )


def projected_mode(
    release,
    *,
    publication_final: bool,
    campaign_closed: bool,
    release_complete: bool,
):
    return register_rows_from_release(
        release,
        matrix_cases=matrix_cases(),
        matrix_path=MATRIX_PATH,
        matrix_sha256=MATRIX_SHA256,
        evidence_commit=EVIDENCE_COMMIT,
        reconciliation_input_sha256=RECONCILIATION_SHA256,
        publication_final=publication_final,
        campaign_closed=campaign_closed,
        release_complete=release_complete,
    )


def fixture_repo(tmp_path: Path) -> tuple[Path, Path]:
    repo = tmp_path / "fixture-repo"
    for relative in (
        *(
            "docs/testing/OpenVINO-Codec-Traceability-Extension-v1.1.csv",
            "docs/testing/Configuration-Register.csv",
            "docs/testing/Test-Run-Register.csv",
            "docs/testing/Performance-Measurement-Register.csv",
            "docs/testing/Device-Verification-Register.csv",
            "docs/testing/Quality-Evaluation-Register.csv",
            "docs/testing/Failure-Register.csv",
            "docs/testing/Evidence-Index.csv",
            "docs/testing/Workbook-Completion-Register.csv",
            "docs/testing/Workbook-Revision-Register.csv",
            "docs/testing/workbooks/Controlled-Workbook-Manifest.csv",
        ),
        SOURCE_TEMPLATE,
    ):
        source = ROOT / relative
        destination = repo / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination)
    docx = repo / GENERATED_DOCX
    docx.parent.mkdir(parents=True, exist_ok=True)
    docx.write_bytes(b"previous fixture DOCX bytes")
    matrix = repo / MATRIX_PATH
    matrix.parent.mkdir(parents=True, exist_ok=True)
    matrix.write_bytes(b"fixture adaptive comparison matrix\n")
    validator = repo / "scripts/testing/Validate-Workbook-Revision-Control.py"
    validator.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(
        ROOT / "scripts/testing/Validate-Workbook-Revision-Control.py", validator
    )
    release_input = repo / "experiments/synthetic/checkpoint/reconciliation-input.json"
    release_input.parent.mkdir(parents=True, exist_ok=True)
    release_input.write_text("{}\n", encoding="utf-8")
    return repo, release_input


def publication_inputs(repo: Path, release_input: Path, release=None) -> PublicationInputs:
    matrix = (repo / MATRIX_PATH).resolve()
    matrix_sha256 = hashlib.sha256(matrix.read_bytes()).hexdigest()
    reconciliation_sha256 = hashlib.sha256(release_input.read_bytes()).hexdigest()
    return PublicationInputs(
        release=_bind_matrix_sha(release or complete_release(), matrix_sha256),
        matrix_cases=matrix_cases(),
        matrix_path=matrix,
        matrix_sha256=matrix_sha256,
        reconciliation_input_path=release_input.resolve(),
        reconciliation_input_sha256=reconciliation_sha256,
        publication_state_path=(release_input.parent / "publication-state.json").resolve(),
    )


def fake_generate(markdown_path: Path, docx_path: Path, revision_csv_path: Path) -> None:
    payload = markdown_path.read_bytes() + b"\0" + revision_csv_path.read_bytes()
    docx_path.parent.mkdir(parents=True, exist_ok=True)
    docx_path.write_bytes(b"synthetic-docx:" + hashlib.sha256(payload).hexdigest().encode())


def fake_audit(
    docx_path: Path,
    manifest_path: Path,
    markdown_path: Path,
    profile,
) -> dict[str, object]:
    assert docx_path.read_bytes().startswith(b"synthetic-docx:")
    assert profile.expected_matrix_sha256 in markdown_path.read_text(encoding="utf-8")
    assert manifest_path.is_file()
    return {"accepted": True}


def fake_revision_validator(_root: Path) -> dict[str, object]:
    return {"accepted": True}


def build_fixture_bundle(tmp_path: Path, release=None):
    repo, release_input = fixture_repo(tmp_path)
    return repo, release_input, build_publication_bundle(
        release or complete_release(),
        repo_root=repo,
        require_complete=False,
        evidence_commit=EVIDENCE_COMMIT,
        matrix_cases=matrix_cases(),
        matrix_path=(repo / MATRIX_PATH),
        matrix_sha256=MATRIX_SHA256,
        reconciliation_input_path=release_input,
        reconciliation_input_sha256=RECONCILIATION_SHA256,
        generate_docx=fake_generate,
        audit_docx_fn=fake_audit,
        revision_validator=fake_revision_validator,
    )


def hash_destinations(repo: Path, release_input: Path) -> dict[str, str | None]:
    result: dict[str, str | None] = {}
    for relative in PUBLICATION_PATHS[:-1]:
        path = repo / relative
        result[relative] = hashlib.sha256(path.read_bytes()).hexdigest() if path.exists() else None
    state = release_input.parent / "publication-state.json"
    result["publication-state.json"] = (
        hashlib.sha256(state.read_bytes()).hexdigest() if state.exists() else None
    )
    return result


def publish_fixture(
    repo: Path,
    release_input: Path,
    monkeypatch: pytest.MonkeyPatch,
    **kwargs,
):
    import scripts.testing.publish_official_openvino_comparison as publisher

    loaded = publication_inputs(repo, release_input)
    monkeypatch.setattr(publisher, "_load_publication_inputs", lambda path, root: loaded)
    monkeypatch.setattr(
        publisher,
        "_runtime_matrix_identity_hashes",
        lambda _path, _sha, ids: {test_id: loaded.matrix_sha256 for test_id in ids},
    )
    monkeypatch.setattr(
        publisher,
        "load_comparison_release_input",
        lambda path: SimpleNamespace(
            matrix_path=loaded.matrix_path,
            matrix_sha256=loaded.matrix_sha256,
        ),
    )
    kwargs.setdefault("revision_validator", fake_revision_validator)
    return publish_reconciled_checkpoint(
        release_input,
        repo_root=repo,
        require_complete=False,
        evidence_commit=EVIDENCE_COMMIT,
        generate_docx=fake_generate,
        audit_docx_fn=fake_audit,
        **kwargs,
    )


def rows_for(
    rows_by_path, filename: str, *, test_id: str, context: int
) -> list[dict[str, str]]:
    rows = rows_by_path[next(path for path in rows_by_path if path.endswith(filename))]
    configuration_id = f"WB04-CMP-{test_id}-{context}"
    return [
        dict(row)
        for row in rows
        if row.get("Configuration_ID") == configuration_id
        or (
            row.get("Test_ID") == test_id
            and f"context_tokens={context}" in row.get("Notes", "")
        )
    ]


def test_accepted_runtime_projects_three_performance_rows_and_one_run_row() -> None:
    rows = projected()

    performance = rows_for(
        rows, "Performance-Measurement-Register.csv", test_id="OV-11", context=512
    )
    runs = rows_for(rows, "Test-Run-Register.csv", test_id="OV-11", context=512)

    assert [row["Repetition_Number"] for row in performance] == ["1", "2", "3"]
    assert len(runs) == 1
    assert [row["TTFT_ms"] for row in performance] == ["3", "4", "6"]
    assert runs[0]["TTFT_ms"] == "4.33333333333333"


def test_complete_quality_projects_exactly_six_weighted_prompt_rows() -> None:
    rows = rows_for(
        projected(), "Quality-Evaluation-Register.csv", test_id="OV-11", context=512
    )

    assert [row["Prompt_ID"] for row in rows] == ["P1", "P2", "P3", "P4", "P5", "P6"]
    assert [row["Weighted_Score_0_to_10"] for row in rows] == [
        "6", "6.1", "6.2", "6.3", "6.4", "6.5"
    ]
    assert all(row["Raw_Response_Path"] == "" for row in rows)
    assert all(row["Response_SHA256"] == "" for row in rows)
    assert all("per-criterion and raw-response facts are not exposed" in row["Notes"] for row in rows)
    assert {row["Result"] for row in rows} == {"Adjudicated"}


def test_low_or_zero_numeric_quality_is_adjudicated_not_inferred_passed() -> None:
    release = complete_release()
    key = ComparisonKey("OV-11", 512)
    quality = dict(release.quality)
    scores = {f"P{index}": 0.0 if index == 1 else float(index) for index in range(1, 7)}
    quality[key] = replace(quality[key], prompt_scores=scores)
    rows = rows_for(
        projected(replace(release, quality=quality)),
        "Quality-Evaluation-Register.csv",
        test_id=key.test_id,
        context=key.context_tokens,
    )
    assert rows[0]["Weighted_Score_0_to_10"] == "0"
    assert {row["Result"] for row in rows} == {"Adjudicated"}
    for row in rows:
        assert row["Deterministic_Check_Result"] == ""
        assert row["Critical_Cap_Applied"] == ""
        assert row["Critical_Cap_Reason"] == ""


def test_projection_uses_exact_governed_prompt_and_rubric_ids_where_applicable() -> None:
    rows = projected()
    for path in (
        "docs/testing/Configuration-Register.csv",
        "docs/testing/Quality-Evaluation-Register.csv",
    ):
        governed = [row for row in rows[path] if row.get("Workbook_ID") == "WB-04"]
        assert governed
        assert {row["Prompt_Set_ID"] for row in governed} == {"GTQ-PROMPTS-v1"}
        assert {row["Rubric_ID"] for row in governed} == {"GTQ-QUALITY-RUBRIC-v1"}


def test_placement_cells_require_explicit_telemetry_and_offload_uses_matrix_route() -> None:
    rows = projected()
    run = rows_for(
        rows, "Test-Run-Register.csv", test_id="OV-11", context=512
    )[0]
    device = rows_for(
        rows, "Device-Verification-Register.csv", test_id="OV-11", context=512
    )[0]
    assert run["Actual_Model_Layer_Placement"] == ""
    assert run["Actual_KV_Placement"] == ""
    assert device["Actual_Model_Layer_Placement"] == ""
    assert device["Actual_KV_Placement"] == ""
    assert device["Requested_Offload"] == "stateful-standard"
    assert "model-layer and KV placement are not exposed" in run["Notes"]
    assert "model-layer and KV placement are not exposed" in device["Notes"]

    release = complete_release()
    key = ComparisonKey("OV-11", 512)
    runtime = dict(release.runtime)
    outcome = runtime[key]
    activation = dict(outcome.activation)
    telemetry = dict(activation["telemetry"])
    telemetry.update(
        actual_model_layer_placement="all layers on CPU",
        actual_kv_placement="CPU persistent state",
    )
    activation["telemetry"] = telemetry
    runtime[key] = replace(outcome, activation=activation)
    explicit = projected(replace(release, runtime=runtime))
    explicit_run = rows_for(
        explicit, "Test-Run-Register.csv", test_id="OV-11", context=512
    )[0]
    assert explicit_run["Actual_Model_Layer_Placement"] == "all layers on CPU"
    assert explicit_run["Actual_KV_Placement"] == "CPU persistent state"


def test_runtime_run_does_not_misattribute_tokens_to_quality_prompts() -> None:
    rows = projected()
    run = rows_for(
        rows, "Test-Run-Register.csv", test_id="OV-11", context=512
    )[0]
    assert run["Prompt_Set_ID"] == ""
    assert run["Prompt_ID"] == ""
    assert run["Rubric_ID"] == ""
    assert run["Actual_Input_Tokens"] != ""
    assert run["Actual_Output_Tokens"] != ""
    assert f"runtime_prompt_sha256={'1' * 64}" in run["Notes"]

    release = complete_release()
    key = ComparisonKey("OV-11", 512)
    runtime = dict(release.runtime)
    outcome = runtime[key]
    samples = [dict(sample) for sample in outcome.samples]
    for sample in samples:
        sample["num_input_tokens"] = 20
        sample["num_generated_tokens"] = 10
    samples[2]["num_input_tokens"] = 21
    runtime[key] = replace(outcome, samples=tuple(samples))
    with pytest.raises(ValueError, match="input tokens.*formal samples"):
        projected(replace(release, runtime=runtime))


def test_runtime_terminal_projects_only_failure_not_numeric_rows() -> None:
    release = complete_release()
    key = ComparisonKey("OV-12", 4096)
    digest = hashlib.sha256(b"OV-12 runtime terminal").hexdigest()
    terminals = dict(release.terminals)
    terminals[key] = {
        "test_id": key.test_id,
        "context_tokens": key.context_tokens,
        "stage": "runtime-boundary",
        "principal_reason": "RAM floor reached before model load",
        "evidence_path": Path("experiments/synthetic/terminal/OV-12-4096.json"),
        "evidence_sha256": digest,
    }
    boundaries = dict(release.boundaries)
    boundaries[key.test_id] = BoundaryOutcome(
        key.test_id, 512, 512, 4096, "runtime-boundary", digest
    )
    release = replace(release, terminals=terminals, boundaries=boundaries)
    rows = projected(release)

    assert rows_for(
        rows, "Performance-Measurement-Register.csv", test_id=key.test_id, context=key.context_tokens
    ) == []
    assert rows_for(
        rows, "Quality-Evaluation-Register.csv", test_id=key.test_id, context=key.context_tokens
    ) == []
    failures = rows_for(
        rows, "Failure-Register.csv", test_id=key.test_id, context=key.context_tokens
    )
    assert len(failures) == 1
    assert failures[0]["Failure_Stage"] == "runtime-boundary"
    assert failures[0]["Observed_Symptom"] == "RAM floor reached before model load"
    assert failures[0]["Raw_Evidence_Path"].endswith("OV-12-4096.json")
    assert digest in failures[0]["Notes"]
    terminal_run = rows_for(
        rows, "Test-Run-Register.csv", test_id=key.test_id, context=key.context_tokens
    )[0]
    assert terminal_run["Model_Load_Success"] == ""
    assert terminal_run["Generation_Success"] == ""
    assert terminal_run["Next_Action"] == ""
    for field in ("Safety_Impact", "Suspected_Cause", "Root_Cause", "Fix_or_Workaround"):
        assert failures[0][field] == ""
    assert failures[0]["Failure_Category"] == "Governed runtime terminal"


def test_projection_has_five_traceability_rows_twenty_five_configs_and_wr037() -> None:
    rows = projected()
    traceability = rows["docs/testing/OpenVINO-Codec-Traceability-Extension-v1.1.csv"]
    configurations = rows["docs/testing/Configuration-Register.csv"]
    revisions = rows["docs/testing/Workbook-Revision-Register.csv"]

    assert [row["Test_ID"] for row in traceability] == list(COMPARISON_RUN_ORDER)
    assert len(configurations) == 25
    wr037 = [row for row in revisions if row["Record_ID"] == "WR-037"]
    assert len(wr037) == 1
    assert wr037[0]["Change_Reference"] == (
        "testing/openvino-adaptive-format-comparison; adaptive comparison "
        f"reconciliation input SHA-256 {RECONCILIATION_SHA256}; Pending PR; Pending merge"
    )
    assert wr037[0]["Status"] == "Current - pending PR"
    wr036 = [row for row in revisions if row["Record_ID"] == "WR-036"]
    assert len(wr036) == 1
    assert wr036[0]["Status"] == "Superseded"


def test_projection_order_comes_from_committed_matrix_contract_not_local_constants() -> None:
    import scripts.testing.publish_official_openvino_comparison as publisher

    release = complete_release()
    forward = register_rows_from_release(
        release,
        matrix_cases=matrix_cases(),
        matrix_path=MATRIX_PATH,
        matrix_sha256=MATRIX_SHA256,
        evidence_commit=EVIDENCE_COMMIT,
        reconciliation_input_sha256=RECONCILIATION_SHA256,
    )
    reverse = register_rows_from_release(
        release,
        matrix_cases=tuple(reversed(matrix_cases())),
        matrix_path=MATRIX_PATH,
        matrix_sha256=MATRIX_SHA256,
        evidence_commit=EVIDENCE_COMMIT,
        reconciliation_input_sha256=RECONCILIATION_SHA256,
    )
    assert forward == reverse
    assert not hasattr(publisher, "COMPARISON_IDS")
    assert not hasattr(publisher, "COMPARISON_CONTEXTS")


def test_runtime_only_quality_status_is_explicit_without_numeric_fabrication() -> None:
    for status in ("capture-complete-awaiting-adjudication", "quality-blocked"):
        release = incomplete_release()
        key = ComparisonKey("OV-12", 512)
        quality = dict(release.quality)
        quality[key] = replace(
            quality[key],
            status=status,
            prompt_scores=None,
            aggregates=None,
        )
        rows = projected(replace(release, quality=quality))
        run = rows_for(
            rows, "Test-Run-Register.csv", test_id=key.test_id, context=key.context_tokens
        )[0]
        assert run["Quality_Score_0_to_10"] == ""
        assert status in run["Result_Reason"]
        assert rows_for(
            rows,
            "Quality-Evaluation-Register.csv",
            test_id=key.test_id,
            context=key.context_tokens,
        ) == []


def test_completion_rows_distinguish_open_closed_awaiting_and_validated_final() -> None:
    early = projected_mode(
        complete_release(),
        publication_final=False,
        campaign_closed=False,
        release_complete=False,
    )["docs/testing/Workbook-Completion-Register.csv"]
    assert [row["Completion_Status"] for row in early[:4]] == ["Complete"] * 4
    assert {row["Completion_Status"] for row in early[4:]} == {"Partial checkpoint"}
    assert {row["Missing_Data_Code"] for row in early[4:]} == {"CAMPAIGN_OPEN"}

    final_release = closed_complete_release()
    validate_closed_campaign(final_release)
    validate_complete_release(final_release)
    awaiting_quality = dict(final_release.quality)
    key = ComparisonKey("OV-12", 512)
    awaiting_quality[key] = replace(
        awaiting_quality[key],
        status="capture-complete-awaiting-adjudication",
        prompt_scores=None,
        aggregates=None,
    )
    awaiting = replace(final_release, quality=awaiting_quality)
    validate_closed_campaign(awaiting)
    with pytest.raises(ValueError, match="numeric adjudication"):
        validate_complete_release(awaiting)
    closed_rows = projected_mode(
        awaiting,
        publication_final=False,
        campaign_closed=True,
        release_complete=False,
    )["docs/testing/Workbook-Completion-Register.csv"]
    quality_section = next(row for row in closed_rows if row["Section_or_Test_ID"] == "9")
    assert quality_section["Completion_Status"] == "Awaiting adjudication"
    assert quality_section["Missing_Data_Code"] == "QUALITY_ADJUDICATION_PENDING"
    assert "adjudication" in quality_section["Remaining_Action"].lower()
    assert all(row["Completion_Status"] != "Complete" for row in closed_rows[4:])

    final_rows = projected_mode(
        final_release,
        publication_final=True,
        campaign_closed=True,
        release_complete=True,
    )["docs/testing/Workbook-Completion-Register.csv"]
    assert {row["Completion_Status"] for row in final_rows} == {"Complete"}
    assert {row["Remaining_Action"] for row in final_rows} == {"None"}


def test_test_run_next_action_tracks_quality_state_without_fabricated_failure() -> None:
    release = complete_release()
    complete = rows_for(
        projected(release), "Test-Run-Register.csv", test_id="OV-12", context=512
    )[0]
    assert complete["Next_Action"] == "None"

    key = ComparisonKey("OV-12", 512)
    quality = dict(release.quality)
    quality[key] = replace(
        quality[key],
        status="capture-complete-awaiting-adjudication",
        prompt_scores=None,
        aggregates=None,
    )
    awaiting = rows_for(
        projected(replace(release, quality=quality)),
        "Test-Run-Register.csv",
        test_id=key.test_id,
        context=key.context_tokens,
    )[0]
    assert awaiting["Next_Action"] == "Complete governed P1-P6 adjudication"
    assert awaiting["Failure_IDs"] == ""

    terminal_key = ComparisonKey("OV-11", 2048)
    terminal = rows_for(
        projected(release),
        "Test-Run-Register.csv",
        test_id=terminal_key.test_id,
        context=terminal_key.context_tokens,
    )[0]
    assert terminal["Next_Action"] == "Respect governed quality terminal: quality-capture"


def test_semantic_register_sorting_orders_contexts_and_sections_numerically(
    tmp_path: Path,
) -> None:
    _repo, _release_input, bundle = build_fixture_bundle(tmp_path)
    configurations = list(
        csv.DictReader(
            io.StringIO(
                bundle.files["docs/testing/Configuration-Register.csv"].decode("utf-8-sig")
            )
        )
    )
    ov11_contexts = [
        int(row["Context_Target_Tokens"])
        for row in configurations
        if row["Workbook_ID"] == "WB-04" and row["Test_ID"] == "OV-11"
    ]
    assert ov11_contexts == [512, 1024, 2048, 4096, 8192]

    completion = list(
        csv.DictReader(
            io.StringIO(
                bundle.files["docs/testing/Workbook-Completion-Register.csv"].decode(
                    "utf-8-sig"
                )
            )
        )
    )
    assert [
        int(row["Section_or_Test_ID"])
        for row in completion
        if row["Workbook_ID"] == "WB-04"
    ] == list(range(1, 12))


def test_per_sample_and_aggregate_resource_cells_use_exact_available_evidence() -> None:
    release = complete_release()
    key = ComparisonKey("OV-11", 512)
    runtime = dict(release.runtime)
    outcome = runtime[key]
    samples = []
    for index, original in enumerate(outcome.samples):
        sample = dict(original)
        sample.update(
            {
                "source": f"experiments/synthetic/runtime/OV-11-512-S{index + 1}.json",
                "source_sha256": hashlib.sha256(f"sample-{index}".encode()).hexdigest(),
                "num_input_tokens": 20,
                "num_generated_tokens": 10,
                "available_ram_before_mb": 700 + index,
                "available_ram_after_mb": 650 + index,
                "cpu_percent": {"values": [10.0 + index, 30.0 + index], "count": 2},
                "gpu_percent": {"values": [2.0 + index, 6.0 + index], "count": 2},
                "gpu_dedicated_memory_peak_mb": 10.0 + index,
                "gpu_shared_memory_peak_mb": 20.0 + index,
            }
        )
        samples.append(sample)
    runtime[key] = replace(outcome, samples=tuple(samples))
    rows = projected(replace(release, runtime=runtime))
    performance = rows_for(
        rows, "Performance-Measurement-Register.csv", test_id=key.test_id, context=key.context_tokens
    )
    run = rows_for(rows, "Test-Run-Register.csv", test_id=key.test_id, context=key.context_tokens)[0]
    device = rows_for(
        rows, "Device-Verification-Register.csv", test_id=key.test_id, context=key.context_tokens
    )[0]

    assert performance[0]["Peak_Working_Set_Bytes"] == str(102 * 1024**2)
    assert performance[0]["Peak_Private_Bytes"] == str(82 * 1024**2)
    assert performance[0]["Minimum_Available_RAM_During_Bytes"] == str(502 * 1024**2)
    assert performance[0]["KV_Cache_Allocated_Bytes"] == str(12 * 1024**2)
    assert performance[0]["Available_RAM_Before_Bytes"] == str(700 * 1024**2)
    assert performance[0]["Available_RAM_After_Bytes"] == str(650 * 1024**2)
    assert performance[0]["GPU_Dedicated_Peak_Bytes"] == str(10 * 1024**2)
    assert performance[0]["GPU_Shared_Peak_Bytes"] == str(20 * 1024**2)
    assert (
        performance[0]["CPU_Mean_Percent"],
        performance[0]["CPU_Median_Percent"],
        performance[0]["CPU_Peak_Percent"],
    ) == ("20", "20", "30")
    assert (
        performance[0]["GPU_Engine_Mean_Percent"],
        performance[0]["GPU_Engine_Median_Percent"],
        performance[0]["GPU_Engine_Peak_Percent"],
    ) == ("4", "4", "6")
    assert "cpu_sample_count=2" in performance[0]["Notes"]
    assert "gpu_sample_count=2" in performance[0]["Notes"]
    assert run["Actual_Device"] == "CPU"
    assert run["Fallback_Observed"] == "False"
    assert run["K_Cache_Type"] == "f32"
    assert run["V_Cache_Type"] == "f32"
    assert run["CPU_Mean_Percent"] == "26"
    assert run["GPU_Engine_Mean_Percent"] == "4"
    assert run["GPU_Dedicated_Peak_Bytes"] == str(12 * 1024**2)
    assert run["GPU_Shared_Peak_Bytes"] == str(22 * 1024**2)
    assert device["CPU_Median_Percent"] == "26"
    assert device["GPU_Engine_Median_Percent"] == "4"


def test_accepted_runtime_rejects_missing_split_gpu_memory_receipt() -> None:
    release = complete_release()
    key = ComparisonKey("OV-11", 512)
    runtime = dict(release.runtime)
    outcome = runtime[key]
    samples = [dict(sample) for sample in outcome.samples]
    samples[1].pop("gpu_shared_memory_peak_mb")
    runtime[key] = replace(outcome, samples=tuple(samples))

    with pytest.raises(ValueError, match="gpu_shared_memory_peak_mb"):
        projected(replace(release, runtime=runtime))


def test_csv_slice_rewrite_preserves_header_and_unrelated_rows_and_is_idempotent(
    tmp_path: Path,
) -> None:
    path = tmp_path / "register.csv"
    path.write_bytes(b"ID,Value\r\nunrelated,keep\r\nowned,old\r\n")
    replacement = ({"ID": "owned", "Value": "new"},)

    first = rewrite_csv_slice_atomically(
        path,
        owned_key=lambda row: row["ID"] if row["ID"] == "owned" else None,
        replacement_rows=replacement,
    )
    path.write_bytes(first)
    second = rewrite_csv_slice_atomically(
        path,
        owned_key=lambda row: row["ID"] if row["ID"] == "owned" else None,
        replacement_rows=replacement,
    )

    assert first == second
    reader = csv.DictReader(io.StringIO(second.decode("utf-8-sig"), newline=""))
    assert reader.fieldnames == ["ID", "Value"]
    assert list(reader) == [
        {"ID": "unrelated", "Value": "keep"},
        {"ID": "owned", "Value": "new"},
    ]


def test_csv_slice_rejects_duplicate_owned_keys(tmp_path: Path) -> None:
    path = tmp_path / "register.csv"
    path.write_text("ID,Value\n", encoding="utf-8")
    with pytest.raises(ValueError, match="duplicate owned key"):
        rewrite_csv_slice_atomically(
            path,
            owned_key=lambda row: row["ID"],
            replacement_rows=(
                {"ID": "same", "Value": "one"},
                {"ID": "same", "Value": "two"},
            ),
        )


def test_csv_slice_rejects_duplicate_real_ids_outside_owned_slice(tmp_path: Path) -> None:
    path = tmp_path / "register.csv"
    path.write_text(
        "ID,Workbook_ID,Value\nshared,WB-99,foreign\n",
        encoding="utf-8",
    )
    with pytest.raises(ValueError, match="duplicate global key"):
        rewrite_csv_slice_atomically(
            path,
            owned_key=lambda row: row["ID"] if row["Workbook_ID"] == "WB-04" else None,
            unique_key=lambda row: row["ID"],
            replacement_rows=(
                {"ID": "shared", "Workbook_ID": "WB-04", "Value": "replacement"},
            ),
        )


def test_foreign_wr036_is_not_silently_owned_or_deleted(tmp_path: Path) -> None:
    repo, release_input = fixture_repo(tmp_path)
    revision = repo / "docs/testing/Workbook-Revision-Register.csv"
    with revision.open("a", encoding="utf-8", newline="") as handle:
        handle.write(
            "WR-036,WB-99,9.9,2026-08-01,Foreign,Foreign,Foreign,Foreign,Foreign,Foreign,Current,\r\n"
        )
    with pytest.raises(ValueError, match="duplicate global key.*WR-036"):
        build_publication_bundle(
            complete_release(),
            repo_root=repo,
            evidence_commit=EVIDENCE_COMMIT,
            matrix_cases=matrix_cases(),
            matrix_path=repo / MATRIX_PATH,
            matrix_sha256=MATRIX_SHA256,
            reconciliation_input_path=release_input,
            reconciliation_input_sha256=RECONCILIATION_SHA256,
            generate_docx=fake_generate,
            audit_docx_fn=fake_audit,
        )


def test_bundle_uses_actual_headers_and_replaces_matrix_and_metadata_once(
    tmp_path: Path,
) -> None:
    repo, _release_input, bundle = build_fixture_bundle(tmp_path)
    markdown = bundle.files[SOURCE_TEMPLATE].decode("utf-8")

    assert MATRIX_SHA256 in markdown
    assert markdown.count(MATRIX_SHA256) == 1
    assert HISTORICAL_MATRIX_SHA256 not in markdown
    assert "validated cases=5; declared contexts=25" in markdown
    for line in (
        "**Controlled filename:** `04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx`",
        "**Generated DOCX hash:** recorded in `Controlled-Workbook-Manifest.csv`",
        "**Original source:** `04_Official_OpenVINO_Editable_Test_Workbook.docx`",
        "**Original source SHA-256:** `e892a1a9ea2956e10f3eaa9dba89dcef59a7a6f89cb21b34e274aa3d72baa2b3`",
    ):
        assert markdown.count(line) == 1
    for relative in PUBLICATION_PATHS[:-1]:
        assert relative in bundle.files
    assert not (repo / "experiments/raw-results").exists()


def test_bundle_keeps_six_manifest_rows_and_only_replaces_wb04(tmp_path: Path) -> None:
    repo, _release_input, bundle = build_fixture_bundle(tmp_path)
    before = list(
        csv.DictReader(
            io.StringIO(
                (repo / "docs/testing/workbooks/Controlled-Workbook-Manifest.csv")
                .read_text(encoding="utf-8-sig")
            )
        )
    )
    after = list(
        csv.DictReader(
            io.StringIO(
                bundle.files[
                    "docs/testing/workbooks/Controlled-Workbook-Manifest.csv"
                ].decode("utf-8-sig")
            )
        )
    )

    assert [row["Workbook_ID"] for row in after] == [f"WB-{index:02d}" for index in range(1, 7)]
    assert [row for row in before if row["Workbook_ID"] != "WB-04"] == [
        row for row in after if row["Workbook_ID"] != "WB-04"
    ]
    wb04 = next(row for row in after if row["Workbook_ID"] == "WB-04")
    assert wb04["Revision"] == "1.9"
    assert wb04["Canonical_Template_SHA256"] == hashlib.sha256(
        bundle.files[SOURCE_TEMPLATE]
    ).hexdigest()
    assert wb04["Last_Validated_DOCX_SHA256"] == hashlib.sha256(
        bundle.files[GENERATED_DOCX]
    ).hexdigest()


def test_bundle_binds_completion_and_reconciliation_rows_to_explicit_checkpoint_path(
    tmp_path: Path,
) -> None:
    _repo, release_input, bundle = build_fixture_bundle(tmp_path)
    completion = list(
        csv.DictReader(
            io.StringIO(
                bundle.files["docs/testing/Workbook-Completion-Register.csv"].decode(
                    "utf-8-sig"
                )
            )
        )
    )
    evidence = list(
        csv.DictReader(
            io.StringIO(
                bundle.files["docs/testing/Evidence-Index.csv"].decode("utf-8-sig")
            )
        )
    )
    expected_state = (
        release_input.parent / "publication-state.json"
    ).relative_to(release_input.parents[3]).as_posix()
    assert {
        row["Source_Result_Path"]
        for row in completion
        if row["Workbook_ID"] == "WB-04"
    } == {expected_state}
    reconciliation = next(
        row for row in evidence if row["Evidence_Type"] == "reconciliation-input"
    )
    assert reconciliation["Repository_Path"] == release_input.relative_to(
        release_input.parents[3]
    ).as_posix()
    assert reconciliation["SHA256"] == RECONCILIATION_SHA256


def test_display_normalization_rejects_evidence_outside_explicit_repo_root(
    tmp_path: Path,
) -> None:
    repo, release_input = fixture_repo(tmp_path)
    release = complete_release()
    key = ComparisonKey("OV-11", 512)
    runtime = dict(release.runtime)
    runtime[key] = replace(
        runtime[key], evidence_path=(tmp_path.parent / "outside-runtime.json").resolve()
    )
    with pytest.raises(ValueError, match="outside repo_root"):
        build_publication_bundle(
            replace(release, runtime=runtime),
            repo_root=repo,
            evidence_commit=EVIDENCE_COMMIT,
            matrix_cases=matrix_cases(),
            matrix_path=repo / MATRIX_PATH,
            matrix_sha256=MATRIX_SHA256,
            reconciliation_input_path=release_input,
            reconciliation_input_sha256=RECONCILIATION_SHA256,
            generate_docx=fake_generate,
            audit_docx_fn=fake_audit,
        )


def test_path_loader_calls_task7_loader_once_and_reconciles_same_input(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    import scripts.testing.publish_official_openvino_comparison as publisher

    repo, release_input = fixture_repo(tmp_path)
    release_input.write_text(
        json.dumps({"release_input_sha256": RECONCILIATION_SHA256}),
        encoding="utf-8",
    )
    matrix = repo / MATRIX_PATH
    matrix.parent.mkdir(parents=True, exist_ok=True)
    matrix.write_text("{}\n", encoding="utf-8")
    loaded_paths: list[Path] = []
    reconciled_paths: list[Path] = []

    def load(path: Path):
        loaded_paths.append(path)
        return SimpleNamespace(matrix_path=matrix, matrix_sha256=MATRIX_SHA256)

    def reconcile(path: Path):
        reconciled_paths.append(path)
        return complete_release()

    monkeypatch.setattr(publisher, "load_comparison_release_input", load)
    monkeypatch.setattr(publisher, "load_adaptive_comparison_matrix", lambda path: matrix_cases())
    monkeypatch.setattr(publisher, "reconcile_comparison_release", reconcile)

    result = publisher._load_publication_inputs(release_input, repo)

    assert loaded_paths == [release_input.resolve()]
    assert reconciled_paths == [release_input.resolve()]
    assert result.matrix_path == matrix.resolve()


def test_checkpoint_is_idempotent_manifest_then_state_last_and_hash_bound(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    repo, release_input = fixture_repo(tmp_path)
    events: list[str] = []

    first = publish_fixture(
        repo, release_input, monkeypatch, replace_observer=events.append
    )
    first_bytes = hash_destinations(repo, release_input)
    second = publish_fixture(repo, release_input, monkeypatch)

    assert events[-2:] == ["Controlled-Workbook-Manifest.csv", "publication-state.json"]
    assert first["published_hashes"] == second["published_hashes"]
    assert first["publication_state_sha256"] == second["publication_state_sha256"]
    assert hash_destinations(repo, release_input) == first_bytes
    state = json.loads((release_input.parent / "publication-state.json").read_text(encoding="utf-8"))
    assert state["published_hashes"] == first["published_hashes"]
    assert "publication-state.json" not in state["published_hashes"]
    assert set(state["published_hashes"]) == set(PUBLICATION_PATHS[:-1])
    assert validate_published_bundle(repo, release_input.parent / "publication-state.json") == first


def test_every_replace_failure_rolls_back_exact_bytes_and_removes_new_state(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    for failure_position in range(1, len(PUBLICATION_PATHS) + 1):
        case_root = tmp_path / f"replace-{failure_position}"
        repo, release_input = fixture_repo(case_root)
        before = hash_destinations(repo, release_input)
        calls = 0

        def fail_at_position(_name: str) -> None:
            nonlocal calls
            calls += 1
            if calls == failure_position:
                raise RuntimeError(f"replace failure {failure_position}")

        with pytest.raises(RuntimeError, match="replace failure"):
            publish_fixture(
                repo,
                release_input,
                monkeypatch,
                replace_observer=fail_at_position,
            )
        assert hash_destinations(repo, release_input) == before
        assert not list(repo.rglob("*.tmp"))
        assert not list(repo.rglob("*.bak"))


def test_rollback_attempts_every_replaced_target_and_aggregates_all_failures(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    import scripts.testing.publish_official_openvino_comparison as publisher

    repo, release_input = fixture_repo(tmp_path)
    before = hash_destinations(repo, release_input)
    replaced_count = 5
    original_restore = publisher._atomic_restore
    path_names = {
        str((repo / relative).resolve()): relative
        for relative in PUBLICATION_PATHS[:-1]
    }
    path_names[str((release_input.parent / "publication-state.json").resolve())] = (
        "publication-state.json"
    )
    restore_failures = {PUBLICATION_PATHS[1], PUBLICATION_PATHS[3]}
    attempts: list[str] = []
    replacements: list[str] = []

    def fail_publication(name: str) -> None:
        replacements.append(name)
        if len(replacements) == replaced_count:
            raise RuntimeError("primary publication failure")

    def partially_failing_restore(path: Path, value: bytes | None) -> None:
        name = path_names[str(Path(path).resolve())]
        attempts.append(name)
        if name in restore_failures:
            raise OSError(f"restore failure: {name}")
        original_restore(path, value)

    monkeypatch.setattr(publisher, "_atomic_restore", partially_failing_restore)
    with pytest.raises(BaseExceptionGroup) as caught:
        publish_fixture(
            repo,
            release_input,
            monkeypatch,
            replace_observer=fail_publication,
        )

    def flattened(error: BaseException) -> list[str]:
        if isinstance(error, BaseExceptionGroup):
            return [
                message
                for child in error.exceptions
                for message in flattened(child)
            ]
        return [str(error)]

    assert attempts == list(reversed(PUBLICATION_PATHS[:replaced_count]))
    messages = flattened(caught.value)
    assert "primary publication failure" in messages
    assert {message for message in messages if message.startswith("restore failure:")} == {
        f"restore failure: {name}" for name in restore_failures
    }
    after = hash_destinations(repo, release_input)
    for name in PUBLICATION_PATHS[:replaced_count]:
        if name in restore_failures:
            assert after[name] != before[name]
        else:
            assert after[name] == before[name]
    for name in PUBLICATION_PATHS[replaced_count:]:
        assert after[name] == before[name]


def test_generation_or_docx_audit_failure_performs_zero_destination_writes(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    import scripts.testing.publish_official_openvino_comparison as publisher

    for mode in ("generation", "audit"):
        repo, release_input = fixture_repo(tmp_path / mode)
        loaded = publication_inputs(repo, release_input)
        monkeypatch.setattr(publisher, "_load_publication_inputs", lambda path, root, value=loaded: value)
        monkeypatch.setattr(
            publisher,
            "_runtime_matrix_identity_hashes",
            lambda _path, _sha, ids, value=loaded: {
                test_id: value.matrix_sha256 for test_id in ids
            },
        )
        before = hash_destinations(repo, release_input)

        def broken_generate(*_args) -> None:
            raise ValueError("DOCX generation refused")

        def broken_audit(*_args) -> dict[str, object]:
            raise ValueError("DOCX audit refused")

        with pytest.raises(ValueError, match="DOCX"):
            publish_reconciled_checkpoint(
                release_input,
                repo_root=repo,
                require_complete=False,
                evidence_commit=EVIDENCE_COMMIT,
                generate_docx=broken_generate if mode == "generation" else fake_generate,
                audit_docx_fn=broken_audit if mode == "audit" else fake_audit,
            )
        assert hash_destinations(repo, release_input) == before


def test_revision_control_failure_in_staged_overlay_performs_zero_writes(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    repo, release_input = fixture_repo(tmp_path)
    before = hash_destinations(repo, release_input)

    def reject(_overlay: Path) -> dict[str, object]:
        raise ValueError("staged revision control refused")

    with pytest.raises(ValueError, match="staged revision control refused"):
        publish_fixture(
            repo,
            release_input,
            monkeypatch,
            revision_validator=reject,
        )
    assert hash_destinations(repo, release_input) == before


def test_build_refuses_revision_validator_source_outside_explicit_repo_root(
    tmp_path: Path,
) -> None:
    repo, release_input = fixture_repo(tmp_path)
    (repo / "scripts/testing/Validate-Workbook-Revision-Control.py").unlink()
    with pytest.raises(ValueError, match="missing from the explicit repo_root"):
        build_publication_bundle(
            complete_release(),
            repo_root=repo,
            evidence_commit=EVIDENCE_COMMIT,
            matrix_cases=matrix_cases(),
            matrix_path=repo / MATRIX_PATH,
            matrix_sha256=MATRIX_SHA256,
            reconciliation_input_path=release_input,
            reconciliation_input_sha256=RECONCILIATION_SHA256,
            generate_docx=fake_generate,
            audit_docx_fn=fake_audit,
            revision_validator=fake_revision_validator,
        )


def test_post_publish_revision_control_failure_rolls_back_replaced_targets(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    repo, release_input = fixture_repo(tmp_path)
    before = hash_destinations(repo, release_input)
    calls: list[Path] = []

    def fail_second(root: Path) -> dict[str, object]:
        calls.append(root)
        if len(calls) == 2:
            raise ValueError("post-publish revision control refused")
        return {"accepted": True}

    with pytest.raises(ValueError, match="post-publish revision control refused"):
        publish_fixture(
            repo,
            release_input,
            monkeypatch,
            revision_validator=fail_second,
        )
    assert len(calls) == 2
    assert hash_destinations(repo, release_input) == before


def test_cleanup_failure_surfaces_alongside_primary_observer_failure(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    import scripts.testing.publish_official_openvino_comparison as publisher

    repo, release_input = fixture_repo(tmp_path)

    def primary(_staging: Path) -> None:
        raise ValueError("primary staging observer failure")

    def cleanup(*_args, **_kwargs) -> None:
        raise OSError("staging cleanup failure")

    monkeypatch.setattr(publisher.shutil, "rmtree", cleanup)
    with pytest.raises(BaseExceptionGroup) as caught:
        build_publication_bundle(
            complete_release(),
            repo_root=repo,
            evidence_commit=EVIDENCE_COMMIT,
            matrix_cases=matrix_cases(),
            matrix_path=repo / MATRIX_PATH,
            matrix_sha256=MATRIX_SHA256,
            reconciliation_input_path=release_input,
            reconciliation_input_sha256=RECONCILIATION_SHA256,
            generate_docx=fake_generate,
            audit_docx_fn=fake_audit,
            staging_observer=primary,
        )
    messages = {str(error) for error in caught.value.exceptions}
    assert messages == {
        "primary staging observer failure",
        "staging cleanup failure",
    }


def test_concurrent_destination_drift_is_rejected_before_first_replace(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    repo, release_input = fixture_repo(tmp_path)
    drift_path = repo / "docs/testing/Configuration-Register.csv"
    before_others = hash_destinations(repo, release_input)

    def drift() -> None:
        drift_path.write_bytes(drift_path.read_bytes() + b"external drift")

    with pytest.raises(RuntimeError, match="destination drift"):
        publish_fixture(repo, release_input, monkeypatch, before_replace=drift)

    after = hash_destinations(repo, release_input)
    assert after["docs/testing/Configuration-Register.csv"] != before_others[
        "docs/testing/Configuration-Register.csv"
    ]
    for path, digest in before_others.items():
        if path != "docs/testing/Configuration-Register.csv":
            assert after[path] == digest


def test_future_target_drift_after_first_replace_is_refused_without_clobbering_edit(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    repo, release_input = fixture_repo(tmp_path)
    before = hash_destinations(repo, release_input)
    future_name = PUBLICATION_PATHS[4]
    future = repo / future_name
    external = future.read_bytes() + b"external future-target edit"
    calls = 0

    def mutate_after_first(_name: str) -> None:
        nonlocal calls
        calls += 1
        if calls == 1:
            future.write_bytes(external)

    with pytest.raises(RuntimeError, match="destination drift"):
        publish_fixture(
            repo,
            release_input,
            monkeypatch,
            replace_observer=mutate_after_first,
        )

    after = hash_destinations(repo, release_input)
    assert future.read_bytes() == external
    assert after[future_name] != before[future_name]
    for name, digest in before.items():
        if name != future_name:
            assert after[name] == digest


def test_exclusive_publisher_lock_prevents_nested_interleaving(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    repo, release_input = fixture_repo(tmp_path)
    nested_errors: list[str] = []

    def try_nested(_staging: Path) -> None:
        try:
            publish_fixture(repo, release_input, monkeypatch)
        except RuntimeError as error:
            nested_errors.append(str(error))

    publish_fixture(
        repo,
        release_input,
        monkeypatch,
        staging_observer=try_nested,
    )
    assert nested_errors and "locked" in nested_errors[0]


def test_destination_containment_is_rechecked_after_link_swap_signal(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    import scripts.testing.publish_official_openvino_comparison as publisher

    repo, release_input = fixture_repo(tmp_path)
    before = hash_destinations(repo, release_input)
    original_inside = publisher._inside
    swapped = False

    def checked(path: Path, root: Path, label: str) -> Path:
        if swapped and label.startswith("publication destination"):
            raise ValueError("publication destination is outside repo_root after link swap")
        return original_inside(path, root, label)

    def signal_swap(_name: str) -> None:
        nonlocal swapped
        swapped = True

    monkeypatch.setattr(publisher, "_inside", checked)
    with pytest.raises(ValueError, match="outside repo_root after link swap"):
        publish_fixture(
            repo,
            release_input,
            monkeypatch,
            replace_observer=signal_swap,
        )
    assert hash_destinations(repo, release_input) == before


def test_staging_is_outside_raw_evidence_and_cleaned_on_success(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    repo, release_input = fixture_repo(tmp_path)
    observed: list[Path] = []

    publish_fixture(repo, release_input, monkeypatch, staging_observer=observed.append)

    assert len(observed) == 1
    assert repo / "experiments/raw-results" not in observed[0].parents
    assert not observed[0].exists()


def test_staging_observer_failure_still_cleans_unique_staging_directory(
    tmp_path: Path,
) -> None:
    repo, release_input = fixture_repo(tmp_path)
    observed: list[Path] = []

    def fail(staging: Path) -> None:
        observed.append(staging)
        raise RuntimeError("staging observer refused")

    with pytest.raises(RuntimeError, match="staging observer refused"):
        build_publication_bundle(
            complete_release(),
            repo_root=repo,
            evidence_commit=EVIDENCE_COMMIT,
            matrix_cases=matrix_cases(),
            matrix_path=repo / MATRIX_PATH,
            matrix_sha256=MATRIX_SHA256,
            reconciliation_input_path=release_input,
            reconciliation_input_sha256=RECONCILIATION_SHA256,
            generate_docx=fake_generate,
            audit_docx_fn=fake_audit,
            staging_observer=fail,
        )
    assert len(observed) == 1
    assert not observed[0].exists()


def test_published_bundle_validation_rejects_hash_drift_and_unknown_paths(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    repo, release_input = fixture_repo(tmp_path)
    publish_fixture(repo, release_input, monkeypatch)
    state_path = release_input.parent / "publication-state.json"
    markdown = repo / SOURCE_TEMPLATE
    markdown.write_bytes(markdown.read_bytes() + b"drift")
    with pytest.raises(ValueError, match="hash drift"):
        validate_published_bundle(repo, state_path)

    publish_fixture(repo, release_input, monkeypatch)
    state = json.loads(state_path.read_text(encoding="utf-8"))
    state["published_hashes"]["unknown.txt"] = "0" * 64
    state_path.write_text(json.dumps(state), encoding="utf-8")
    with pytest.raises(ValueError, match="unknown|extra"):
        validate_published_bundle(repo, state_path)


def test_published_bundle_reopens_and_hash_binds_matrix_and_task7_input(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    import scripts.testing.publish_official_openvino_comparison as publisher

    repo, release_input = fixture_repo(tmp_path)
    publish_fixture(repo, release_input, monkeypatch)
    state_path = release_input.parent / "publication-state.json"
    matrix = repo / MATRIX_PATH

    matrix_bytes = matrix.read_bytes()
    matrix.unlink()
    with pytest.raises(ValueError, match="matrix.*missing|matrix.*hash"):
        validate_published_bundle(repo, state_path)
    matrix.write_bytes(matrix_bytes + b"drift")
    with pytest.raises(ValueError, match="matrix.*hash"):
        validate_published_bundle(repo, state_path)
    matrix.write_bytes(matrix_bytes)

    monkeypatch.setattr(
        publisher,
        "load_comparison_release_input",
        lambda _path: (_ for _ in ()).throw(
            ValueError("comparison release input self-hash is invalid")
        ),
    )
    with pytest.raises(ValueError, match="self-hash"):
        validate_published_bundle(repo, state_path)

    inputs = publication_inputs(repo, release_input)
    wrong = repo / "experiments/synthetic/wrong-matrix.json"
    wrong.write_bytes(matrix_bytes)
    monkeypatch.setattr(
        publisher,
        "load_comparison_release_input",
        lambda _path: SimpleNamespace(
            matrix_path=wrong,
            matrix_sha256=inputs.matrix_sha256,
        ),
    )
    with pytest.raises(ValueError, match="matrix binding"):
        validate_published_bundle(repo, state_path)


def test_published_bundle_rejects_reconciliation_file_hash_drift_before_loader(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    repo, release_input = fixture_repo(tmp_path)
    publish_fixture(repo, release_input, monkeypatch)
    release_input.write_bytes(release_input.read_bytes() + b"drift")
    with pytest.raises(ValueError, match="reconciliation input.*hash"):
        validate_published_bundle(
            repo,
            release_input.parent / "publication-state.json",
        )


def test_projection_rejects_nonfinite_score_blank_terminal_and_matrix_disagreement() -> None:
    release = complete_release()
    quality = dict(release.quality)
    key = ComparisonKey("OV-11", 512)
    bad_scores = dict(quality[key].prompt_scores or {})
    bad_scores["P1"] = float("nan")
    quality[key] = replace(quality[key], prompt_scores=bad_scores)
    with pytest.raises(ValueError, match="finite"):
        projected(replace(release, quality=quality))

    terminal = dict(release.terminals)
    terminal_key = next(iter(terminal))
    terminal[terminal_key] = {**terminal[terminal_key], "principal_reason": ""}
    with pytest.raises(ValueError, match="principal reason"):
        projected(replace(release, terminals=terminal))

    runtime = dict(release.runtime)
    runtime[key] = replace(
        runtime[key], identity_hashes={**runtime[key].identity_hashes, "matrix_sha256": "f" * 64}
    )
    with pytest.raises(ValueError, match="disagrees"):
        projected(replace(release, runtime=runtime))


def test_projection_rejects_identical_but_wrong_runtime_matrix_identity_hashes() -> None:
    release = complete_release()
    runtime = {
        key: replace(
            outcome,
            identity_hashes={
                **outcome.identity_hashes,
                "matrix_sha256": "f" * 64,
            },
        )
        for key, outcome in release.runtime.items()
    }

    with pytest.raises(ValueError, match="matrix identity hash disagrees"):
        projected(replace(release, runtime=runtime))
    assert projected(release)["docs/testing/Test-Run-Register.csv"]


def test_csv_rewrite_rejects_incomplete_schema_and_preserves_reordered_header(
    tmp_path: Path,
) -> None:
    incomplete = tmp_path / "incomplete.csv"
    incomplete.write_text("ID\nold\n", encoding="utf-8")
    with pytest.raises(ValueError, match="compatible header"):
        rewrite_csv_slice_atomically(
            incomplete,
            owned_key=lambda row: row.get("ID"),
            replacement_rows=({"ID": "new", "Value": "required"},),
        )

    reordered = tmp_path / "reordered.csv"
    reordered.write_bytes(b"Value,ID\r\nkeep,unrelated\r\n")
    encoded = rewrite_csv_slice_atomically(
        reordered,
        owned_key=lambda row: row["ID"] if row["ID"] == "owned" else None,
        replacement_rows=({"ID": "owned", "Value": "new"},),
    )
    assert encoded.splitlines()[0] == b"Value,ID"


def test_failure_reason_round_trips_crlf_and_commonmark_without_row_corruption(
    tmp_path: Path,
) -> None:
    release = complete_release()
    key = next(iter(release.terminals))
    terminals = dict(release.terminals)
    reason = "RAM floor\r\n| pipe | **bold** `code`"
    terminals[key] = {**terminals[key], "principal_reason": reason}
    rows = projected(replace(release, terminals=terminals))
    replacement = rows["docs/testing/Failure-Register.csv"]
    repo, _ = fixture_repo(tmp_path)
    path = repo / "docs/testing/Failure-Register.csv"
    encoded = rewrite_csv_slice_atomically(
        path,
        owned_key=lambda row: row.get("Failure_ID") if row.get("Failure_ID", "").startswith("WB04-CMP-") else None,
        replacement_rows=replacement,
    )
    parsed = list(csv.DictReader(io.StringIO(encoded.decode("utf-8-sig"), newline="")))
    governed = next(
        row
        for row in parsed
        if row["Failure_ID"]
        == f"WB04-CMP-FAIL-{key.test_id}-{key.context_tokens}-RUNTIME"
    )
    assert governed["Observed_Symptom"] == reason


def test_same_key_changed_evidence_replaces_owned_slice_without_accumulating(
    tmp_path: Path,
) -> None:
    repo, _ = fixture_repo(tmp_path)
    path = repo / "docs/testing/Evidence-Index.csv"
    first = projected()["docs/testing/Evidence-Index.csv"]
    encoded = rewrite_csv_slice_atomically(
        path,
        owned_key=lambda row: row.get("Evidence_ID") if row.get("Evidence_ID", "").startswith("WB04-CMP-") else None,
        replacement_rows=first,
    )
    path.write_bytes(encoded)
    release = complete_release()
    key = ComparisonKey("OV-11", 512)
    runtime = dict(release.runtime)
    changed_digest = hashlib.sha256(b"changed evidence").hexdigest()
    runtime[key] = replace(runtime[key], evidence_sha256=changed_digest)
    second = projected(replace(release, runtime=runtime))["docs/testing/Evidence-Index.csv"]
    rewritten = rewrite_csv_slice_atomically(
        path,
        owned_key=lambda row: row.get("Evidence_ID") if row.get("Evidence_ID", "").startswith("WB04-CMP-") else None,
        replacement_rows=second,
    )
    parsed = list(csv.DictReader(io.StringIO(rewritten.decode("utf-8-sig"))))
    runtime_rows = [
        row for row in parsed
        if row["Test_ID"] == key.test_id and row["Evidence_Type"] == "runtime"
        and row["Run_ID"] == f"WB04-CMP-{key.test_id}-{key.context_tokens}-RUN"
    ]
    assert len(runtime_rows) == 1
    assert runtime_rows[0]["SHA256"] == changed_digest


def test_real_generator_revision_and_comparison_audit_run_only_in_staging(
    tmp_path: Path,
) -> None:
    repo, release_input = fixture_repo(tmp_path)
    destinations_before = hash_destinations(repo, release_input)

    bundle = build_publication_bundle(
        complete_release(),
        repo_root=repo,
        require_complete=False,
        evidence_commit=EVIDENCE_COMMIT,
        matrix_cases=matrix_cases(),
        matrix_path=repo / MATRIX_PATH,
        matrix_sha256=MATRIX_SHA256,
        reconciliation_input_path=release_input,
        reconciliation_input_sha256=RECONCILIATION_SHA256,
        revision_validator=fake_revision_validator,
    )

    assert hash_destinations(repo, release_input) == destinations_before
    docx_copy = tmp_path / "candidate.docx"
    docx_copy.write_bytes(bundle.files[GENERATED_DOCX])
    with zipfile.ZipFile(docx_copy) as archive:
        assert archive.testzip() is None
    assert bundle.files[GENERATED_DOCX] != b"previous fixture DOCX bytes"


def complete_revision_fixture_repo(tmp_path: Path) -> tuple[Path, Path]:
    repo = tmp_path / "complete-revision-fixture"
    docs_source = ROOT / "docs/testing"
    docs_target = repo / "docs/testing"
    for source in docs_source.rglob("*"):
        if not source.is_file():
            continue
        relative = source.relative_to(docs_source)
        if relative.as_posix() == (
            "workbooks/generated/"
            "04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx"
        ):
            continue
        destination = docs_target / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination)
    docx = repo / GENERATED_DOCX
    docx.parent.mkdir(parents=True, exist_ok=True)
    docx.write_bytes(b"previous fixture DOCX bytes")
    validator = repo / "scripts/testing/Validate-Workbook-Revision-Control.py"
    validator.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(
        ROOT / "scripts/testing/Validate-Workbook-Revision-Control.py", validator
    )
    release_input = repo / "experiments/synthetic/checkpoint/reconciliation-input.json"
    release_input.parent.mkdir(parents=True, exist_ok=True)
    release_input.write_text("{}\n", encoding="utf-8")
    matrix = repo / MATRIX_PATH
    matrix.parent.mkdir(parents=True, exist_ok=True)
    matrix.write_bytes(b"fixture adaptive comparison matrix\n")
    return repo, release_input


def test_complete_published_fixture_passes_copied_revision_validator(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    import scripts.testing.publish_official_openvino_comparison as publisher

    repo, release_input = complete_revision_fixture_repo(tmp_path)
    loaded = publication_inputs(repo, release_input)
    monkeypatch.setattr(publisher, "_load_publication_inputs", lambda path, root: loaded)
    monkeypatch.setattr(
        publisher,
        "_runtime_matrix_identity_hashes",
        lambda _path, _sha, ids: {test_id: loaded.matrix_sha256 for test_id in ids},
    )
    monkeypatch.setattr(
        publisher,
        "load_comparison_release_input",
        lambda _path: SimpleNamespace(
            matrix_path=loaded.matrix_path,
            matrix_sha256=loaded.matrix_sha256,
        ),
    )

    publish_reconciled_checkpoint(
        release_input,
        repo_root=repo,
        require_complete=False,
        evidence_commit=EVIDENCE_COMMIT,
    )
    completed = subprocess.run(
        [sys.executable, str(repo / "scripts/testing/Validate-Workbook-Revision-Control.py")],
        cwd=repo,
        text=True,
        capture_output=True,
        check=False,
    )

    assert completed.returncode == 0, completed.stdout + completed.stderr
    assert "WORKBOOK REVISION CONTROL: PASS" in completed.stdout


def test_real_task7_path_release_publishes_reopens_and_rejects_authority_tamper(
    tmp_path: Path,
) -> None:
    from scripts.testing.tests.test_official_openvino_comparison_reconcile import (
        _path as task7_path,
        _project_authoritative_release,
        valid_release_input,
    )

    repo, _synthetic_input = complete_revision_fixture_repo(tmp_path)
    authority = repo / "experiments/real-task7-authority"
    authority.mkdir(parents=True)
    mapping = valid_release_input(authority)
    release_input = _project_authoritative_release(
        mapping,
        authority / "comparison-release-input.json",
    )

    published = publish_reconciled_checkpoint(
        release_input,
        repo_root=repo,
        require_complete=False,
        evidence_commit=EVIDENCE_COMMIT,
        generate_docx=fake_generate,
        audit_docx_fn=fake_audit,
        revision_validator=fake_revision_validator,
    )
    state_path = release_input.parent / "publication-state.json"
    assert validate_published_bundle(repo, state_path) == published
    evidence = list(
        csv.DictReader(
            io.StringIO(
                (repo / "docs/testing/Evidence-Index.csv").read_text(
                    encoding="utf-8-sig"
                )
            )
        )
    )
    reconciliation = next(
        row for row in evidence if row["Evidence_Type"] == "reconciliation-input"
    )
    assert reconciliation["Repository_Path"] == release_input.relative_to(repo).as_posix()
    assert str(repo.resolve()) not in reconciliation["Repository_Path"]

    campaign_state = task7_path(mapping, mapping["campaign_state"])
    campaign_state.write_bytes(campaign_state.read_bytes() + b"tamper")
    with pytest.raises(ValueError, match="campaign state|hash drift|unreadable"):
        validate_published_bundle(repo, state_path)


def test_publisher_cli_requires_explicit_arguments_and_forwards_normal_mode(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    import scripts.testing.publish_official_openvino_comparison as publisher

    with pytest.raises(SystemExit):
        publisher.parse_args([])
    calls: list[tuple[Path, dict[str, object]]] = []

    def publish(path: Path, **keywords: object) -> dict[str, object]:
        calls.append((path, keywords))
        return {
            "published_hashes": {"target": "1" * 64},
            "publication_state_path": str(tmp_path / "publication-state.json"),
            "publication_state_sha256": "2" * 64,
        }

    monkeypatch.setattr(publisher, "publish_reconciled_checkpoint", publish)
    release = tmp_path / "release.json"
    release.write_text("{}\n", encoding="utf-8")
    result = publisher.main(
        [
            "--release-input", str(release),
            "--repo-root", str(tmp_path),
            "--evidence-commit", "a" * 40,
            "--require-complete",
        ]
    )
    payload = json.loads(capsys.readouterr().out)
    assert result == 0
    assert payload["publication_state_sha256"] == "2" * 64
    assert calls == [
        (
            release.resolve(),
            {
                "repo_root": tmp_path.resolve(),
                "require_complete": True,
                "evidence_commit": "a" * 40,
            },
        )
    ]


def test_publisher_cli_check_only_builds_full_bundle_without_destination_writes(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    import scripts.testing.publish_official_openvino_comparison as publisher

    repo, release_input = fixture_repo(tmp_path)
    loaded = publication_inputs(repo, release_input)
    monkeypatch.setattr(publisher, "_load_publication_inputs", lambda _path, _root: loaded)
    monkeypatch.setattr(
        publisher,
        "_runtime_matrix_identity_hashes",
        lambda _path, _sha, ids: {test_id: loaded.matrix_sha256 for test_id in ids},
    )
    monkeypatch.setattr(publisher, "_generate_real_docx", fake_generate)
    monkeypatch.setattr(publisher, "_audit_real_docx", fake_audit)
    monkeypatch.setattr(publisher, "_run_revision_validator", fake_revision_validator)
    before = hash_destinations(repo, release_input)
    result = publisher.main(
        [
            "--release-input", str(release_input),
            "--repo-root", str(repo),
            "--evidence-commit", EVIDENCE_COMMIT,
            "--check-only",
        ]
    )
    payload = json.loads(capsys.readouterr().out)
    assert result == 0
    assert payload["check_only"] is True
    assert set(payload["published_hashes"]) == set(PUBLICATION_PATHS[:-1])
    assert len(payload["publication_state_sha256"]) == 64
    assert hash_destinations(repo, release_input) == before

    assert publisher.main(
        [
            "--release-input", str(release_input),
            "--repo-root", str(repo),
            "--evidence-commit", "A" * 40,
        ]
    ) != 0
