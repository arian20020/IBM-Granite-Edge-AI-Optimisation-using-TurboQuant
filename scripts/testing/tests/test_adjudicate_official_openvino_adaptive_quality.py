import hashlib
import json
import os
import subprocess
import sys
from pathlib import Path
from unittest.mock import Mock

import pytest

from scripts.testing.measure_official_openvino import (
    build_campaign_identity,
    run_measurement_sequence,
)
from scripts.testing import adjudicate_official_openvino_adaptive_quality as adaptive_adjudicator
from scripts.testing.adjudicate_official_openvino_adaptive_quality import (
    adjudicate_adaptive_quality,
    build_adaptive_blind_bundle,
    main,
    parse_args,
)
from scripts.testing.adjudicate_official_openvino_quality import (
    _prompt_controls,
    deterministic_gate,
)
from scripts.testing.tests.test_official_openvino_adaptive_quality import (
    MIB,
    RecordingGuardRunner,
    _canonical as _task5_canonical,
)
from scripts.testing.tests.test_official_openvino_quality_campaign import (
    _accepted_input,
)
from scripts.testing.tests.test_measure_official_openvino_sequence import (
    _record as _measurement_record,
    _setup_campaign,
)
from scripts.testing.tests.test_run_official_openvino_quality import COMPLETE_OUTPUTS


ROOT = Path(__file__).resolve().parents[3]
PROMPT_SET = (
    ROOT
    / "experiments"
    / "granite_turboquant_intel"
    / "prompts"
    / "fixed-feasibility-prompt-set-v1.json"
)
RUBRIC = (
    ROOT
    / "experiments"
    / "granite_turboquant_intel"
    / "rubrics"
    / "quality-rubric-v1.json"
)
PROMPT_IDS = ("P1", "P2", "P3", "P4", "P5", "P6")
DIMENSIONS = (
    "correctness_and_grounding",
    "instruction_and_format_adherence",
    "completeness_and_fact_retention",
    "relevance_clarity_and_coherence",
    "stability_and_output_integrity",
)


def _canonical_hash(value: dict, hash_field: str) -> str:
    unsigned = {key: item for key, item in value.items() if key != hash_field}
    return hashlib.sha256(
        json.dumps(
            unsigned,
            ensure_ascii=False,
            allow_nan=False,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")
    ).hexdigest()


def _canonical_sha256(value) -> str:
    return hashlib.sha256(
        json.dumps(
            value,
            ensure_ascii=False,
            allow_nan=False,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")
    ).hexdigest()


def _privacy_surface(value):
    if isinstance(value, dict):
        return {
            key: (
                "<opaque>"
                if "sha256" in key.casefold() or key == "blind_label"
                else _privacy_surface(item)
            )
            for key, item in value.items()
        }
    if isinstance(value, list):
        return [_privacy_surface(item) for item in value]
    return value


class PassingGuardRunner(RecordingGuardRunner):
    def __call__(self, **kwargs):
        evidence = super().__call__(**kwargs)
        spec_path = Path(kwargs["bound_inputs"]["quality_worker_spec"])
        spec = json.loads(spec_path.read_text(encoding="utf-8"))
        result_path = Path(kwargs["command"][-1])
        result = json.loads(result_path.read_text(encoding="utf-8"))
        prompt_id = spec["prompt_id"]
        if prompt_id == self.failed_prompt:
            return evidence
        outputs = (
            ["SAVED", COMPLETE_OUTPUTS["P6"]]
            if prompt_id == "P6"
            else [COMPLETE_OUTPUTS[prompt_id]]
        )
        for index, (outcome, output) in enumerate(
            zip(result["outcomes"], outputs, strict=True)
        ):
            outcome["raw_output"] = output
            outcome["raw_output_sha256"] = hashlib.sha256(
                output.encode("utf-8")
            ).hexdigest()
            if prompt_id == "P6" and index == 1:
                raw_prompt = (
                    f"User: {spec['turns'][0]['prompt'].strip()}\n"
                    "Assistant: SAVED\n"
                    f"User: {spec['turns'][1]['prompt'].strip()}"
                )
                outcome["raw_prompt"] = raw_prompt
                outcome["raw_prompt_sha256"] = hashlib.sha256(
                    raw_prompt.encode("utf-8")
                ).hexdigest()
                outcome["history_source_sha256"] = hashlib.sha256(
                    b"SAVED"
                ).hexdigest()
        unsigned = {
            key: item
            for key, item in result.items()
            if key != "worker_result_sha256"
        }
        result["worker_result_sha256"] = hashlib.sha256(
            _task5_canonical(unsigned)
        ).hexdigest()
        result_path.write_bytes(_task5_canonical(result))
        return evidence


def _install_standard_sampler(source) -> None:
    sampler = (
        source.repo_root
        / "scripts"
        / "testing"
        / "collect_openvino_runtime_utilization.ps1"
    )
    sampler.parent.mkdir(parents=True, exist_ok=True)
    sampler.write_bytes(source.sampler_script.read_bytes())


def _write_task5_pretty_json(path: Path, value) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )


def _accepted_input_variant(
    tmp_path: Path,
    *,
    test_id: str,
    context_tokens: int,
):
    from scripts.testing.campaigns.openvino.quality_campaign import (
        QualityCampaignInput,
    )

    values = _setup_campaign(tmp_path)
    spec = json.loads(values["spec_path"].read_text(encoding="utf-8"))
    spec["controlled_test_id"] = test_id
    spec["context"] = context_tokens
    spec["expected_input_tokens"] = context_tokens
    _write_task5_pretty_json(values["spec_path"], spec)
    matrix = json.loads(values["matrix_path"].read_text(encoding="utf-8"))
    matrix["cases"][0]["test_id"] = test_id
    matrix["cases"][0]["contexts"] = [context_tokens]
    _write_task5_pretty_json(values["matrix_path"], matrix)
    controller = (
        values["repo_root"]
        / "scripts"
        / "testing"
        / "measure_official_openvino.py"
    )
    controller.parent.mkdir(parents=True, exist_ok=True)
    controller.write_text("CONTROLLER = 1\n", encoding="utf-8")
    identity = build_campaign_identity(
        **{
            field: values[field]
            for field in (
                "spec_path",
                "matrix_path",
                "artifact_manifest_path",
                "build_provenance_path",
                "build_root",
                "repo_root",
                "python_executable",
                "python_site_packages",
                "openvino_libraries",
            )
        }
    )
    campaign_root = values["campaign_root"]
    _write_task5_pretty_json(campaign_root / "campaign-identity.json", identity)
    _write_task5_pretty_json(
        campaign_root / "measurement-summary.json",
        {
            "schema": "official-openvino-wb04-measurement-summary/v1",
            "status": "measured",
            "accepted": True,
            "sample_count": 3,
            "sources": [
                {"sample_id": f"sample-{number}"} for number in range(1, 4)
            ],
            "activation": {"fallback": False},
            "cleanup_process_count": 0,
            "test_id": test_id,
            "context_tokens": context_tokens,
            "campaign_identity_sha256": identity["campaign_identity_sha256"],
            "runtime_config_sha256": _canonical_sha256(
                identity["identity"]["config"]
            ),
        },
    )
    source = QualityCampaignInput(
        campaign_root=campaign_root,
        spec_path=values["spec_path"],
        matrix_path=values["matrix_path"],
        artifact_manifest_path=values["artifact_manifest_path"],
        build_provenance_path=values["build_provenance_path"],
        build_root=values["build_root"],
        repo_root=values["repo_root"],
        python_executable=values["python_executable"],
        python_site_packages=values["python_site_packages"],
        openvino_libraries=values["openvino_libraries"],
        sampler_script=values["sampler_script"],
        prompt_set_path=PROMPT_SET,
        rendered_root=PROMPT_SET.parent / "rendered",
        rubric_path=RUBRIC,
        output_root=tmp_path / "quality-output",
        timeout_seconds=1800.0,
    )
    _install_standard_sampler(source)
    roles = ("pilot", "warmup", "sample-1", "sample-2", "sample-3")

    def fake_measurement(**run_kwargs):
        role = run_kwargs["role"]
        role_spec = json.loads(
            run_kwargs["spec_path"].read_text(encoding="utf-8")
        )
        record = _measurement_record(role, roles.index(role), role_spec)
        _write_task5_pretty_json(run_kwargs["output_dir"] / "attempt.json", record)
        return record

    run_measurement_sequence(
        spec_path=source.spec_path,
        campaign_root=source.campaign_root,
        matrix_path=source.matrix_path,
        artifact_manifest_path=source.artifact_manifest_path,
        build_provenance_path=source.build_provenance_path,
        build_root=source.build_root,
        repo_root=source.repo_root,
        python_executable=source.python_executable,
        python_site_packages=source.python_site_packages,
        openvino_libraries=source.openvino_libraries,
        sampler_script=source.sampler_script,
        run_measurement=fake_measurement,
    )
    return source


def _configuration_capture(
    tmp_path: Path,
    monkeypatch,
    *,
    test_id: str,
    context_tokens: int,
) -> Path:
    from scripts.testing.campaigns.openvino import adaptive_quality

    source = _accepted_input_variant(
        tmp_path,
        test_id=test_id,
        context_tokens=context_tokens,
    )
    monkeypatch.setattr(adaptive_quality, "available_ram_bytes", lambda: 4096 * MIB)
    result = adaptive_quality.capture_isolated_quality_campaign(
        source,
        resume=False,
        run_command=PassingGuardRunner(),
    )
    return Path(result["capture_summary_path"])


def _capture_summary(
    tmp_path: Path,
    monkeypatch,
    *,
    failed_prompt=None,
    runner=None,
) -> Path:
    from scripts.testing.campaigns.openvino import adaptive_quality

    source = _accepted_input(tmp_path)
    _install_standard_sampler(source)
    monkeypatch.setattr(adaptive_quality, "available_ram_bytes", lambda: 4096 * MIB)
    result = adaptive_quality.capture_isolated_quality_campaign(
        source,
        resume=False,
        run_command=runner or RecordingGuardRunner(failed_prompt=failed_prompt),
    )
    return Path(result["capture_summary_path"])


def _recovered_p4_summary(tmp_path: Path, monkeypatch) -> tuple[Path, object]:
    from scripts.testing.campaigns.openvino import adaptive_quality

    source = _accepted_input(tmp_path)
    _install_standard_sampler(source)
    monkeypatch.setattr(adaptive_quality, "available_ram_bytes", lambda: 4096 * MIB)
    adaptive_quality.capture_isolated_quality_campaign(
        source,
        resume=False,
        run_command=RecordingGuardRunner(failed_prompt="P4"),
    )
    result = adaptive_quality.capture_isolated_quality_campaign(
        source,
        resume=True,
        run_command=PassingGuardRunner(),
    )
    return Path(result["capture_summary_path"]), source


def _three_summary_history(tmp_path: Path, monkeypatch) -> tuple[Path, object]:
    from scripts.testing.campaigns.openvino import adaptive_quality

    source = _accepted_input(tmp_path)
    _install_standard_sampler(source)
    monkeypatch.setattr(adaptive_quality, "available_ram_bytes", lambda: 4096 * MIB)
    adaptive_quality.capture_isolated_quality_campaign(
        source,
        resume=False,
        run_command=RecordingGuardRunner(failed_prompt="P2"),
    )
    adaptive_quality.capture_isolated_quality_campaign(
        source,
        resume=True,
        run_command=PassingGuardRunner(failed_prompt="P3"),
    )
    result = adaptive_quality.capture_isolated_quality_campaign(
        source,
        resume=True,
        run_command=PassingGuardRunner(),
    )
    return Path(result["capture_summary_path"]), source


def _recovered_terminal_p1_summary(
    tmp_path: Path,
    monkeypatch,
) -> tuple[Path, object]:
    from scripts.testing.campaigns.openvino import adaptive_quality

    source = _accepted_input(tmp_path)
    _install_standard_sampler(source)
    monkeypatch.setattr(adaptive_quality, "available_ram_bytes", lambda: 4096 * MIB)

    def failing_guard(**_kwargs):
        raise RuntimeError("synthetic guard admission failure")

    adaptive_quality.capture_isolated_quality_campaign(
        source,
        resume=False,
        run_command=failing_guard,
    )
    result = adaptive_quality.capture_isolated_quality_campaign(
        source,
        resume=True,
        run_command=PassingGuardRunner(),
    )
    return Path(result["capture_summary_path"]), source


def _resign_task5_summary(path: Path, summary: dict) -> None:
    unsigned = {
        key: item for key, item in summary.items() if key != "capture_summary_sha256"
    }
    summary["capture_summary_sha256"] = hashlib.sha256(
        _task5_canonical(unsigned)
    ).hexdigest()
    path.write_bytes(_task5_canonical(summary))


def _passing_inputs(tmp_path: Path, monkeypatch):
    summary_path = _capture_summary(
        tmp_path, monkeypatch, runner=PassingGuardRunner()
    )
    return build_adaptive_blind_bundle(
        [summary_path],
        prompt_set_path=PROMPT_SET,
        rubric_path=RUBRIC,
    )


def _score_sheet(public: dict, judge_id: str, score: float) -> dict:
    unsigned = {
        "schema_version": 1,
        "artifact_type": "openvino-adaptive-quality-blind-scores",
        "judge_id": judge_id,
        "prompt_set_id": public["prompt_set_id"],
        "rubric_id": public["rubric_id"],
        "scoring_input_sha256": public["scoring_input_sha256"],
        "adjudications": [
            {
                "blind_label": row["blind_label"],
                "prompt_id": row["prompt_id"],
                "response_sha256": row["response_sha256"],
                "dimensions": {name: score for name in DIMENSIONS},
                "manual_critical_caps": [],
                "manual_cap_reasons": [],
                "unsupported_statements_count": 0,
                "reviewer": judge_id,
                "notes": "Independent blind review of the response evidence.",
            }
            for row in public["responses"]
        ],
    }
    return {
        **unsigned,
        "score_sheet_sha256": _canonical_hash(
            {**unsigned, "score_sheet_sha256": "0" * 64},
            "score_sheet_sha256",
        ),
    }


def _set_prompt_score(sheet: dict, prompt_id: str, score: float) -> dict:
    sheet = json.loads(json.dumps(sheet))
    row = next(
        row for row in sheet["adjudications"] if row["prompt_id"] == prompt_id
    )
    row["dimensions"] = {name: score for name in DIMENSIONS}
    sheet["score_sheet_sha256"] = _canonical_hash(
        sheet, "score_sheet_sha256"
    )
    return sheet


def _set_prompt_manual_caps(
    sheet: dict,
    prompt_id: str,
    caps: list[float],
) -> dict:
    sheet = json.loads(json.dumps(sheet))
    row = next(
        row for row in sheet["adjudications"] if row["prompt_id"] == prompt_id
    )
    row["manual_critical_caps"] = caps
    row["manual_cap_reasons"] = [
        f"Judge identified controlling critical-cap condition {index}."
        for index, _cap in enumerate(caps, start=1)
    ]
    sheet["score_sheet_sha256"] = _canonical_hash(
        sheet, "score_sheet_sha256"
    )
    return sheet


def _set_prompt_dimensions(
    sheet: dict,
    prompt_id: str,
    dimensions: dict[str, float],
    *,
    blind_label: str | None = None,
) -> dict:
    sheet = json.loads(json.dumps(sheet))
    row = next(
        row
        for row in sheet["adjudications"]
        if row["prompt_id"] == prompt_id
        and (blind_label is None or row["blind_label"] == blind_label)
    )
    row["dimensions"] = dimensions
    sheet["score_sheet_sha256"] = _canonical_hash(
        sheet, "score_sheet_sha256"
    )
    return sheet


def _empty_pairwise(public: dict) -> dict:
    unsigned = {
        "schema": "official-openvino-adaptive-pairwise-reviews-v1",
        "scoring_input_sha256": public["scoring_input_sha256"],
        "reviews": [],
    }
    return {
        **unsigned,
        "pairwise_reviews_sha256": _canonical_hash(
            {**unsigned, "pairwise_reviews_sha256": "0" * 64},
            "pairwise_reviews_sha256",
        ),
    }


def _empty_manual(public: dict) -> dict:
    unsigned = {
        "schema": "official-openvino-manual-adjudications-v1",
        "scoring_input_sha256": public["scoring_input_sha256"],
        "records": [],
    }
    return {
        **unsigned,
        "manual_adjudications_sha256": _canonical_hash(
            {**unsigned, "manual_adjudications_sha256": "0" * 64},
            "manual_adjudications_sha256",
        ),
    }


def _two_configuration_inputs(tmp_path: Path, monkeypatch):
    summaries = [
        _configuration_capture(
            tmp_path / suffix,
            monkeypatch,
            test_id=test_id,
            context_tokens=4096,
        )
        for suffix, test_id in (
            ("one", "OV-TQ-PAIR-01"),
            ("two", "OV-TQ-PAIR-02"),
        )
    ]
    return build_adaptive_blind_bundle(
        summaries,
        prompt_set_path=PROMPT_SET,
        rubric_path=RUBRIC,
    )


def _four_configuration_inputs(tmp_path: Path, monkeypatch):
    summaries = [
        _configuration_capture(
            tmp_path / suffix,
            monkeypatch,
            test_id=test_id,
            context_tokens=context_tokens,
        )
        for suffix, test_id, context_tokens in (
            ("a", "OV-TQ-PRIVACY-A", 8192),
            ("b", "OV-TQ-PRIVACY-B", 4096),
            ("c", "OV-TQ-PRIVACY-C", 8192),
            ("d", "OV-TQ-PRIVACY-D", 4096),
        )
    ]
    return build_adaptive_blind_bundle(
        summaries,
        prompt_set_path=PROMPT_SET,
        rubric_path=RUBRIC,
    )


def _pairwise_artifact(
    public: dict,
    *,
    omit_order: str | None = None,
    ranking_reversal: bool = False,
) -> dict:
    reviews = []
    for pair in public["pairwise_pairs"]:
        first, second = pair["blind_labels"]
        for order, left, right, winner in (
            ("AB", first, second, "A"),
            ("BA", second, first, "A" if ranking_reversal else "B"),
        ):
            if order == omit_order:
                continue
            unsigned_review = {
                "pair_id": pair["pair_id"],
                "order": order,
                "left_blind_label": left,
                "right_blind_label": right,
                "winner": winner,
                "reviewer": "pairwise-judge",
                "reason": "The selected response better satisfies the rubric.",
            }
            reviews.append(
                {
                    **unsigned_review,
                    "review_sha256": _canonical_hash(
                        {**unsigned_review, "review_sha256": "0" * 64},
                        "review_sha256",
                    ),
                }
            )
    unsigned = {
        "schema": "official-openvino-adaptive-pairwise-reviews-v1",
        "scoring_input_sha256": public["scoring_input_sha256"],
        "reviews": reviews,
    }
    return {
        **unsigned,
        "pairwise_reviews_sha256": _canonical_hash(
            {**unsigned, "pairwise_reviews_sha256": "0" * 64},
            "pairwise_reviews_sha256",
        ),
    }


def _rehash_scoring_and_private(public: dict, private: dict) -> None:
    public["scoring_input_sha256"] = _canonical_hash(
        public, "scoring_input_sha256"
    )
    private["scoring_input_sha256"] = public["scoring_input_sha256"]
    private["private_map_sha256"] = _canonical_hash(
        private, "private_map_sha256"
    )


def _with_critical_gate(public: dict, private: dict) -> tuple[dict, dict]:
    public = json.loads(json.dumps(public))
    private = json.loads(json.dumps(private))
    row = next(row for row in public["responses"] if row["prompt_id"] == "P1")
    row["output"] = ""
    row["output_sha256"] = hashlib.sha256(b"").hexdigest()
    row["turn_outputs"][-1]["output"] = ""
    row["turn_outputs"][-1]["output_sha256"] = row["output_sha256"]
    row["response_sha256"] = hashlib.sha256(
        json.dumps(
            {"prompt_id": "P1", "turn_outputs": row["turn_outputs"]},
            ensure_ascii=False,
            allow_nan=False,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")
    ).hexdigest()
    row["deterministic_gate"] = deterministic_gate(
        "P1", "", row["turn_outputs"], _prompt_controls(PROMPT_SET)["P1"]
    )
    _rehash_scoring_and_private(public, private)
    return public, private


def _with_p1_cap_four(public: dict, private: dict) -> tuple[dict, dict]:
    public = json.loads(json.dumps(public))
    private = json.loads(json.dumps(private))
    row = next(row for row in public["responses"] if row["prompt_id"] == "P1")
    output = "A nonempty response that violates the exact P1 format."
    output_sha256 = hashlib.sha256(output.encode("utf-8")).hexdigest()
    row["output"] = output
    row["output_sha256"] = output_sha256
    row["turn_outputs"][-1]["output"] = output
    row["turn_outputs"][-1]["output_sha256"] = output_sha256
    row["response_sha256"] = _canonical_sha256(
        {"prompt_id": "P1", "turn_outputs": row["turn_outputs"]}
    )
    row["deterministic_gate"] = deterministic_gate(
        "P1", output, row["turn_outputs"], _prompt_controls(PROMPT_SET)["P1"]
    )
    assert row["deterministic_gate"]["critical_caps"] == [4.0]
    _rehash_scoring_and_private(public, private)
    return public, private


def _prompt_manual_record(
    public: dict,
    sheets: list[dict],
    *,
    prompt_id: str,
    flags: list[str],
    rubric_score: float,
) -> dict:
    row = next(row for row in public["responses"] if row["prompt_id"] == prompt_id)
    judge_rows = []
    for sheet in sorted(sheets, key=lambda item: item["judge_id"]):
        adjudication = next(
            item
            for item in sheet["adjudications"]
            if item["blind_label"] == row["blind_label"]
            and item["prompt_id"] == prompt_id
        )
        judge_rows.append(
            {
                "judge_id": sheet["judge_id"],
                "score_sheet_sha256": sheet["score_sheet_sha256"],
                "manual_critical_caps": [
                    float(cap) for cap in adjudication["manual_critical_caps"]
                ],
                "manual_cap_reasons": adjudication["manual_cap_reasons"],
            }
        )
    caps = [
        float(cap) for cap in row["deterministic_gate"]["critical_caps"]
    ] + [
        float(cap)
        for judge in judge_rows
        for cap in judge["manual_critical_caps"]
    ]
    final_score = min([rubric_score, *caps]) if caps else rubric_score
    unsigned = {
        "record_type": "prompt",
        "blind_label": row["blind_label"],
        "prompt_id": prompt_id,
        "response_sha256": row["response_sha256"],
        "flags": flags,
        "evidence": {
            "deterministic_gate_sha256": _canonical_sha256(
                row["deterministic_gate"]
            ),
            "judge_manual_critical_caps": judge_rows,
        },
        "rubric_basis": {name: rubric_score for name in DIMENSIONS},
        "final_score": final_score,
        "final_reason": "The final rubric review resolves every named flag.",
        "adjudicator": "manual-adjudicator",
    }
    return {
        **unsigned,
        "record_sha256": _canonical_hash(
            {**unsigned, "record_sha256": "0" * 64}, "record_sha256"
        ),
    }


def _pair_manual_record(public: dict, pairwise: dict) -> dict:
    pair = public["pairwise_pairs"][0]
    reviews = {
        row["order"]: row
        for row in pairwise["reviews"]
        if row["pair_id"] == pair["pair_id"]
    }
    unsigned = {
        "record_type": "pair",
        "pair_id": pair["pair_id"],
        "flags": ["ranking-reversal"],
        "evidence": {
            "AB_review_sha256": reviews["AB"]["review_sha256"],
            "BA_review_sha256": reviews["BA"]["review_sha256"],
        },
        "selected_winner": pair["blind_labels"][0],
        "rubric_basis": "The final review applies the controlling rubric.",
        "final_reason": "The order-sensitive reversal is resolved manually.",
        "adjudicator": "manual-adjudicator",
    }
    return {
        **unsigned,
        "record_sha256": _canonical_hash(
            {**unsigned, "record_sha256": "0" * 64}, "record_sha256"
        ),
    }


def _manual_artifact(public: dict, records: list[dict]) -> dict:
    unsigned = {
        "schema": "official-openvino-manual-adjudications-v1",
        "scoring_input_sha256": public["scoring_input_sha256"],
        "records": records,
    }
    return {
        **unsigned,
        "manual_adjudications_sha256": _canonical_hash(
            {**unsigned, "manual_adjudications_sha256": "0" * 64},
            "manual_adjudications_sha256",
        ),
    }


def _write_json(path: Path, value) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, ensure_ascii=False, allow_nan=False, sort_keys=True),
        encoding="utf-8",
    )


def _directory_alias_or_skip(alias: Path, target: Path) -> None:
    if os.name == "nt":
        command = Path(os.environ["SystemRoot"]) / "System32" / "cmd.exe"
        created = subprocess.run(
            [
                str(command),
                "/d",
                "/c",
                "mklink",
                "/J",
                str(alias),
                str(target),
            ],
            capture_output=True,
            text=True,
            check=False,
        )
        if created.returncode != 0:
            pytest.skip(
                "directory junction creation unavailable: "
                f"{created.stdout}{created.stderr}"
            )
    else:
        try:
            alias.symlink_to(target, target_is_directory=True)
        except OSError as error:
            pytest.skip(f"directory symlink creation unavailable: {error}")


def _remove_directory_alias(alias: Path) -> None:
    if not os.path.lexists(alias):
        return
    if os.name == "nt":
        alias.rmdir()
    else:
        alias.unlink()


def _capture_index(summary_paths: list[Path]) -> dict:
    unsigned = {
        "schema": "official-openvino-adaptive-quality-capture-index-v1",
        "capture_summaries": [str(path.resolve()) for path in summary_paths],
    }
    return {
        **unsigned,
        "capture_index_sha256": _canonical_hash(
            {**unsigned, "capture_index_sha256": "0" * 64},
            "capture_index_sha256",
        ),
    }


def test_adaptive_adjudicator_exposes_the_approved_public_api() -> None:
    assert callable(build_adaptive_blind_bundle)
    assert callable(adjudicate_adaptive_quality)


def test_build_bundle_projects_complete_task5_capture_without_private_identity(
    tmp_path: Path,
    monkeypatch,
) -> None:
    summary_path = _capture_summary(tmp_path, monkeypatch)

    public, private = build_adaptive_blind_bundle(
        [summary_path],
        prompt_set_path=PROMPT_SET,
        rubric_path=RUBRIC,
    )

    assert public["scoring_input_sha256"] == _canonical_hash(
        public, "scoring_input_sha256"
    )
    assert private["private_map_sha256"] == _canonical_hash(
        private, "private_map_sha256"
    )
    assert [row["prompt_id"] for row in public["responses"]] == list(PROMPT_IDS)
    assert len({row["blind_label"] for row in public["responses"]}) == 1
    assert set(private["mapping"]) == {
        public["responses"][0]["blind_label"]
    }

    encoded = json.dumps(
        _privacy_surface(public), ensure_ascii=False, sort_keys=True
    ).casefold()
    for forbidden in (
        "ov-tq-03",
        "tbq3",
        "tbq4",
        "u4",
        "u8",
        "f16",
        "quality-output",
        "worker-spec.json",
        "runtime_property",
        "private_controller",
    ):
        assert forbidden not in encoded


def test_public_projection_hashes_exact_task5_outputs_including_both_p6_turns(
    tmp_path: Path,
    monkeypatch,
) -> None:
    summary_path = _capture_summary(tmp_path, monkeypatch)

    public, _private = build_adaptive_blind_bundle(
        [summary_path],
        prompt_set_path=PROMPT_SET,
        rubric_path=RUBRIC,
    )

    expected_fields = {
        "blind_label",
        "prompt_id",
        "prompt_sha256",
        "request_sha256",
        "response_sha256",
        "output",
        "output_sha256",
        "turn_outputs",
        "deterministic_gate",
    }
    assert all(set(row) == expected_fields for row in public["responses"])
    p6 = next(row for row in public["responses"] if row["prompt_id"] == "P6")
    assert [turn["turn_id"] for turn in p6["turn_outputs"]] == [
        "P6-turn-1",
        "P6-turn-2",
    ]
    assert p6["response_sha256"] == hashlib.sha256(
        json.dumps(
            {"prompt_id": "P6", "turn_outputs": p6["turn_outputs"]},
            ensure_ascii=False,
            allow_nan=False,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")
    ).hexdigest()


def test_build_bundle_rejects_incomplete_task5_capture(
    tmp_path: Path,
    monkeypatch,
) -> None:
    summary_path = _capture_summary(tmp_path, monkeypatch, failed_prompt="P5")

    with pytest.raises(ValueError, match="exactly P1 through P6"):
        build_adaptive_blind_bundle(
            [summary_path],
            prompt_set_path=PROMPT_SET,
            rubric_path=RUBRIC,
        )


def test_build_bundle_reopens_superseded_failed_prompt_evidence(
    tmp_path: Path,
    monkeypatch,
) -> None:
    latest_summary, source = _recovered_p4_summary(tmp_path, monkeypatch)
    superseded_log = source.output_root / "P4" / "worker.log"
    superseded_log.write_bytes(superseded_log.read_bytes() + b"tampered\n")

    with pytest.raises(ValueError, match="(history|evidence|hash)"):
        build_adaptive_blind_bundle(
            [latest_summary],
            prompt_set_path=PROMPT_SET,
            rubric_path=RUBRIC,
        )


def test_build_bundle_rejects_recovery_rewritten_as_primary_summary(
    tmp_path: Path,
    monkeypatch,
) -> None:
    recovery_path, source = _recovered_p4_summary(tmp_path, monkeypatch)
    primary_path = source.output_root / "capture-summary.json"
    rewritten = json.loads(recovery_path.read_text(encoding="utf-8"))
    rewritten["capture_summary_path"] = str(primary_path.resolve())
    rewritten["previous_capture_summary_path"] = None
    rewritten["previous_capture_summary_sha256"] = None
    _resign_task5_summary(primary_path, rewritten)
    recovery_path.unlink()

    with pytest.raises(ValueError, match="(primary|history|recovery)"):
        build_adaptive_blind_bundle(
            [primary_path],
            prompt_set_path=PROMPT_SET,
            rubric_path=RUBRIC,
        )


def test_build_bundle_rejects_schema_less_subset_guard_record(
    tmp_path: Path,
    monkeypatch,
) -> None:
    summary_path = _capture_summary(
        tmp_path,
        monkeypatch,
        runner=PassingGuardRunner(),
    )
    summary = json.loads(summary_path.read_text(encoding="utf-8"))
    receipt = summary["prompt_receipts"][0]
    guard_path = Path(receipt["guard_evidence_path"])
    original = json.loads(guard_path.read_text(encoding="utf-8"))
    forged = {
        "cleanup_process_count": 0,
        "exit_code": 0,
        "log_sha256": original["log_sha256"],
        "low_memory_stop": False,
        "run_id": original["run_id"],
        "timed_out": False,
        "valid": True,
    }
    guard_path.write_text(
        json.dumps(forged, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    receipt["guard_evidence_sha256"] = hashlib.sha256(
        guard_path.read_bytes()
    ).hexdigest()
    _resign_task5_summary(summary_path, summary)

    with pytest.raises(ValueError, match="guard"):
        build_adaptive_blind_bundle(
            [summary_path],
            prompt_set_path=PROMPT_SET,
            rubric_path=RUBRIC,
        )


def test_build_bundle_rejects_lexical_capture_summary_alias(
    tmp_path: Path,
    monkeypatch,
) -> None:
    summary_path = _capture_summary(
        tmp_path / "capture",
        monkeypatch,
        runner=PassingGuardRunner(),
    )
    alias_root = tmp_path / "capture-root-alias"
    _directory_alias_or_skip(alias_root, summary_path.parent)
    try:
        with pytest.raises(ValueError, match="(alias|reparse|link)"):
            build_adaptive_blind_bundle(
                [alias_root / summary_path.name],
                prompt_set_path=PROMPT_SET,
                rubric_path=RUBRIC,
            )
    finally:
        _remove_directory_alias(alias_root)


def test_capture_index_reader_rejects_lexical_alias(tmp_path: Path) -> None:
    real_root = tmp_path / "real-index-root"
    index_path = real_root / "capture-index.json"
    _write_json(index_path, _capture_index([tmp_path / "capture-summary.json"]))
    alias_root = tmp_path / "index-root-alias"
    _directory_alias_or_skip(alias_root, real_root)
    try:
        with pytest.raises(ValueError, match="(alias|reparse|link)"):
            adaptive_adjudicator._read_capture_index(
                alias_root / index_path.name
            )
    finally:
        _remove_directory_alias(alias_root)


def test_worker_spec_embedded_alias_rejects_before_task5_resolver(
    tmp_path: Path,
    monkeypatch,
) -> None:
    from scripts.testing.campaigns.openvino import adaptive_quality

    source = _accepted_input(tmp_path / "capture")
    _install_standard_sampler(source)
    monkeypatch.setattr(adaptive_quality, "available_ram_bytes", lambda: 4096 * MIB)
    result = adaptive_quality.capture_isolated_quality_campaign(
        source,
        resume=False,
        run_command=PassingGuardRunner(),
    )
    summary_path = Path(result["capture_summary_path"])
    relocated = tmp_path / "relocated-campaign"
    source.campaign_root.rename(relocated)
    _directory_alias_or_skip(source.campaign_root, relocated)
    resolver_calls = []

    def forbidden_resolver(spec):
        resolver_calls.append(spec)
        raise AssertionError("embedded aliases must reject before Task 5 resolution")

    monkeypatch.setattr(
        adaptive_adjudicator,
        "_validate_prompt_worker_spec",
        forbidden_resolver,
    )
    try:
        with pytest.raises(ValueError, match="(alias|reparse|link)"):
            build_adaptive_blind_bundle(
                [summary_path],
                prompt_set_path=PROMPT_SET,
                rubric_path=RUBRIC,
            )
    finally:
        _remove_directory_alias(source.campaign_root)

    assert resolver_calls == []


def test_previous_summary_alias_rejects_before_task5_history_resolver(
    tmp_path: Path,
    monkeypatch,
) -> None:
    latest_summary, source = _recovered_p4_summary(tmp_path, monkeypatch)
    alias_root = tmp_path / "quality-output-alias"
    _directory_alias_or_skip(alias_root, source.output_root)
    latest = json.loads(latest_summary.read_text(encoding="utf-8"))
    latest["previous_capture_summary_path"] = str(
        (alias_root / "capture-summary.json").absolute()
    )
    _resign_task5_summary(latest_summary, latest)
    history_calls = []

    def forbidden_history(*args, **kwargs):
        history_calls.append((args, kwargs))
        raise AssertionError("previous aliases must reject before Task 5 history")

    monkeypatch.setattr(
        adaptive_adjudicator,
        "_validate_task5_history",
        forbidden_history,
    )
    try:
        with pytest.raises(ValueError, match="(alias|reparse|link)"):
            build_adaptive_blind_bundle(
                [latest_summary],
                prompt_set_path=PROMPT_SET,
                rubric_path=RUBRIC,
            )
    finally:
        _remove_directory_alias(alias_root)

    assert history_calls == []


def test_fresh_output_preflight_rejects_broken_lexical_alias(tmp_path: Path) -> None:
    real_root = tmp_path / "real-output-root"
    real_root.mkdir()
    alias_root = tmp_path / "output-root-alias"
    _directory_alias_or_skip(alias_root, real_root)
    try:
        with pytest.raises(
            (FileExistsError, ValueError), match="(overwrite|alias|reparse|link)"
        ):
            adaptive_adjudicator._require_fresh_outputs(
                [alias_root / "new-output.json"]
            )
    finally:
        _remove_directory_alias(alias_root)


def test_build_bundle_rejects_deleted_oldest_summary_in_two_recovery_history(
    tmp_path: Path,
    monkeypatch,
) -> None:
    latest_summary, source = _three_summary_history(tmp_path, monkeypatch)
    (source.output_root / "capture-summary.json").unlink()

    with pytest.raises(ValueError, match="(history|recovery|original|chain)"):
        build_adaptive_blind_bundle(
            [latest_summary],
            prompt_set_path=PROMPT_SET,
            rubric_path=RUBRIC,
        )


def test_build_bundle_rejects_recovery_summary_numbering_gap(
    tmp_path: Path,
    monkeypatch,
) -> None:
    latest_summary, source = _three_summary_history(tmp_path, monkeypatch)
    gap_path = source.output_root / "capture-summary-recovery-004.json"
    gap_path.write_bytes(latest_summary.read_bytes())

    with pytest.raises(ValueError, match="(numbering|gap|history)"):
        build_adaptive_blind_bundle(
            [latest_summary],
            prompt_set_path=PROMPT_SET,
            rubric_path=RUBRIC,
        )


def test_build_bundle_reopens_superseded_terminal_guard_evidence(
    tmp_path: Path,
    monkeypatch,
) -> None:
    latest_summary, source = _recovered_terminal_p1_summary(tmp_path, monkeypatch)
    terminal_guard = source.output_root / "P1" / "guard-evidence.json"
    terminal_guard.write_bytes(terminal_guard.read_bytes() + b"tampered\n")

    with pytest.raises(ValueError, match="(terminal|history|evidence|hash)"):
        build_adaptive_blind_bundle(
            [latest_summary],
            prompt_set_path=PROMPT_SET,
            rubric_path=RUBRIC,
        )


def test_build_bundle_rechecks_projected_sources_at_final_task5_boundary(
    tmp_path: Path,
    monkeypatch,
) -> None:
    summary_path = _capture_summary(
        tmp_path,
        monkeypatch,
        runner=PassingGuardRunner(),
    )
    real_validate = adaptive_adjudicator._validate_task5_result

    def mutate_after_projection(result, *, spec, worker_spec_sha256):
        projected = real_validate(
            result,
            spec=spec,
            worker_spec_sha256=worker_spec_sha256,
        )
        if spec["prompt_id"] == "P6":
            log_path = summary_path.parent / "P1" / "worker.log"
            log_path.write_bytes(log_path.read_bytes() + b"late mutation\n")
        return projected

    monkeypatch.setattr(
        adaptive_adjudicator,
        "_validate_task5_result",
        mutate_after_projection,
    )

    with pytest.raises(ValueError, match="(evidence|hash|guard|receipt)"):
        build_adaptive_blind_bundle(
            [summary_path],
            prompt_set_path=PROMPT_SET,
            rubric_path=RUBRIC,
        )


def test_unblinding_reader_is_not_called_when_score_sheet_hash_is_invalid(
    tmp_path: Path,
    monkeypatch,
) -> None:
    public, private = _passing_inputs(tmp_path, monkeypatch)
    first = _score_sheet(public, "judge-a", 8.0)
    second = _score_sheet(public, "judge-b", 8.0)
    second["score_sheet_sha256"] = "0" * 64
    reader = Mock(return_value=private)

    with pytest.raises(ValueError, match="score sheet hash"):
        adjudicate_adaptive_quality(
            scoring_input=public,
            judge_score_sheets=[first, second],
            pairwise_reviews=_empty_pairwise(public),
            manual_adjudications=_empty_manual(public),
            blind_map_reader=reader,
            rubric_path=RUBRIC,
            prompt_set_path=PROMPT_SET,
        )

    reader.assert_not_called()


def test_complete_two_judge_scores_publish_exact_p1_p6_and_four_aggregates(
    tmp_path: Path,
    monkeypatch,
) -> None:
    public, private = _passing_inputs(tmp_path, monkeypatch)
    reader = Mock(return_value=private)

    result = adjudicate_adaptive_quality(
        scoring_input=public,
        judge_score_sheets=[
            _score_sheet(public, "judge-a", 8.5),
            _score_sheet(public, "judge-b", 9.5),
        ],
        pairwise_reviews=_empty_pairwise(public),
        manual_adjudications=_empty_manual(public),
        blind_map_reader=reader,
        rubric_path=RUBRIC,
        prompt_set_path=PROMPT_SET,
    )

    reader.assert_called_once_with()
    assert result["private_map_sha256"] == private["private_map_sha256"]
    row = result["configurations"][0]
    assert set(row["prompt_scores"]) == set(PROMPT_IDS)
    assert row["prompt_scores"] == {prompt_id: 9.0 for prompt_id in PROMPT_IDS}
    assert row["aggregates"] == {
        "mean": 9.0,
        "median": 9.0,
        "minimum": 9.0,
        "maximum": 9.0,
    }


def test_blind_bundle_schedules_each_same_context_pair_without_private_identity(
    tmp_path: Path,
    monkeypatch,
) -> None:
    public, _private = _two_configuration_inputs(tmp_path, monkeypatch)

    assert len(public["pairwise_pairs"]) == 1
    pair = public["pairwise_pairs"][0]
    assert set(pair) == {"pair_id", "blind_labels"}
    assert len(set(pair["blind_labels"])) == 2
    assert pair["pair_id"] != _canonical_sha256(
        {"blind_labels": sorted(pair["blind_labels"])}
    )
    assert set(pair["blind_labels"]) == {
        row["blind_label"] for row in public["responses"]
    }
    assert public["scoring_input_sha256"] == _canonical_hash(
        public, "scoring_input_sha256"
    )


def test_blind_bundle_randomizes_configuration_groups_pair_schedule_and_orientation(
    tmp_path: Path,
    monkeypatch,
) -> None:
    monkeypatch.setattr(
        adaptive_adjudicator,
        "_secure_shuffle",
        lambda values: list(reversed(values)),
        raising=False,
    )
    monkeypatch.setattr(adaptive_adjudicator.secrets, "randbelow", lambda _limit: 1)
    opaque_ids = iter(("a" * 64, "b" * 64))
    monkeypatch.setattr(
        adaptive_adjudicator.secrets,
        "token_hex",
        lambda length: next(opaque_ids) if length == 32 else "f" * (length * 2),
    )

    public, private = _four_configuration_inputs(tmp_path, monkeypatch)

    response_labels = [
        public["responses"][offset]["blind_label"]
        for offset in range(0, len(public["responses"]), len(PROMPT_IDS))
    ]
    assert [private["mapping"][label]["test_id"] for label in response_labels] == [
        "OV-TQ-PRIVACY-D",
        "OV-TQ-PRIVACY-C",
        "OV-TQ-PRIVACY-B",
        "OV-TQ-PRIVACY-A",
    ]
    assert [pair["pair_id"] for pair in public["pairwise_pairs"]] == [
        "a" * 64,
        "b" * 64,
    ]
    assert [
        [private["mapping"][label]["test_id"] for label in pair["blind_labels"]]
        for pair in public["pairwise_pairs"]
    ] == [
        ["OV-TQ-PRIVACY-A", "OV-TQ-PRIVACY-C"],
        ["OV-TQ-PRIVACY-B", "OV-TQ-PRIVACY-D"],
    ]
    assert all(
        pair["pair_id"]
        != _canonical_sha256({"blind_labels": sorted(pair["blind_labels"])})
        for pair in public["pairwise_pairs"]
    )


def test_every_pair_requires_one_review_in_each_presentation_order_before_unblind(
    tmp_path: Path,
    monkeypatch,
) -> None:
    public, private = _two_configuration_inputs(tmp_path, monkeypatch)
    reader = Mock(return_value=private)

    with pytest.raises(ValueError, match="AB and BA"):
        adjudicate_adaptive_quality(
            scoring_input=public,
            judge_score_sheets=[
                _score_sheet(public, "judge-a", 8.0),
                _score_sheet(public, "judge-b", 8.0),
            ],
            pairwise_reviews=_pairwise_artifact(public, omit_order="BA"),
            manual_adjudications=_empty_manual(public),
            blind_map_reader=reader,
            rubric_path=RUBRIC,
            prompt_set_path=PROMPT_SET,
        )

    reader.assert_not_called()


def test_pairwise_reviews_publish_ab_ba_orders_without_changing_numeric_scores(
    tmp_path: Path,
    monkeypatch,
) -> None:
    public, private = _two_configuration_inputs(tmp_path, monkeypatch)

    result = adjudicate_adaptive_quality(
        scoring_input=public,
        judge_score_sheets=[
            _score_sheet(public, "judge-a", 8.5),
            _score_sheet(public, "judge-b", 9.5),
        ],
        pairwise_reviews=_pairwise_artifact(public),
        manual_adjudications=_empty_manual(public),
        blind_map_reader=Mock(return_value=private),
        rubric_path=RUBRIC,
        prompt_set_path=PROMPT_SET,
    )

    assert result["pairwise_reviews"] == [
        {
            "pair_id": public["pairwise_pairs"][0]["pair_id"],
            "orders": ["AB", "BA"],
            "normalized_winners": [
                public["pairwise_pairs"][0]["blind_labels"][0],
                public["pairwise_pairs"][0]["blind_labels"][0],
            ],
        }
    ]
    assert all(
        row["prompt_scores"] == {prompt_id: 9.0 for prompt_id in PROMPT_IDS}
        for row in result["configurations"]
    )


def test_private_contexts_must_match_blind_pair_roster_after_late_unblind(
    tmp_path: Path,
    monkeypatch,
) -> None:
    public, private = _two_configuration_inputs(tmp_path, monkeypatch)
    changed_label = sorted(private["mapping"])[1]
    private["mapping"][changed_label]["context_tokens"] = 8192
    private["private_map_sha256"] = _canonical_hash(
        private, "private_map_sha256"
    )
    reader = Mock(return_value=private)

    with pytest.raises(ValueError, match="private blind map pairwise schedule"):
        adjudicate_adaptive_quality(
            scoring_input=public,
            judge_score_sheets=[
                _score_sheet(public, "judge-a", 8.5),
                _score_sheet(public, "judge-b", 9.5),
            ],
            pairwise_reviews=_pairwise_artifact(public),
            manual_adjudications=_empty_manual(public),
            blind_map_reader=reader,
            rubric_path=RUBRIC,
            prompt_set_path=PROMPT_SET,
        )

    reader.assert_called_once_with()


@pytest.mark.parametrize(
    "condition",
    ["critical-gate", "judge-disagreement-over-one", "ranking-reversal"],
)
def test_required_manual_adjudication_cannot_be_omitted_before_unblind(
    condition: str,
    tmp_path: Path,
    monkeypatch,
) -> None:
    if condition == "ranking-reversal":
        public, private = _two_configuration_inputs(tmp_path, monkeypatch)
        pairwise = _pairwise_artifact(public, ranking_reversal=True)
        sheets = [
            _score_sheet(public, "judge-a", 8.0),
            _score_sheet(public, "judge-b", 8.0),
        ]
    else:
        public, private = _passing_inputs(tmp_path, monkeypatch)
        if condition == "critical-gate":
            public, private = _with_critical_gate(public, private)
            sheets = [
                _score_sheet(public, "judge-a", 8.0),
                _score_sheet(public, "judge-b", 8.0),
            ]
        else:
            sheets = [
                _score_sheet(public, "judge-a", 8.0),
                _score_sheet(public, "judge-b", 10.0),
            ]
        pairwise = _empty_pairwise(public)
    reader = Mock(return_value=private)

    with pytest.raises(ValueError, match="manual adjudication"):
        adjudicate_adaptive_quality(
            scoring_input=public,
            judge_score_sheets=sheets,
            pairwise_reviews=pairwise,
            manual_adjudications=_empty_manual(public),
            blind_map_reader=reader,
            rubric_path=RUBRIC,
            prompt_set_path=PROMPT_SET,
        )

    reader.assert_not_called()


def test_judge_critical_cap_requires_manual_record_even_without_score_difference(
    tmp_path: Path,
    monkeypatch,
) -> None:
    public, private = _passing_inputs(tmp_path, monkeypatch)
    sheets = [
        _set_prompt_manual_caps(
            _score_sheet(public, judge_id, 8.0), "P1", [4.0]
        )
        for judge_id in ("judge-a", "judge-b")
    ]
    reader = Mock(return_value=private)

    with pytest.raises(ValueError, match="manual adjudication"):
        adjudicate_adaptive_quality(
            scoring_input=public,
            judge_score_sheets=sheets,
            pairwise_reviews=_empty_pairwise(public),
            manual_adjudications=_empty_manual(public),
            blind_map_reader=reader,
            rubric_path=RUBRIC,
            prompt_set_path=PROMPT_SET,
        )

    reader.assert_not_called()


def test_manual_replacement_cannot_exceed_strictest_judge_critical_cap(
    tmp_path: Path,
    monkeypatch,
) -> None:
    public, private = _passing_inputs(tmp_path, monkeypatch)
    public, private = _with_p1_cap_four(public, private)
    sheets = [
        _set_prompt_manual_caps(
            _score_sheet(public, judge_id, 8.0), "P1", [2.0]
        )
        for judge_id in ("judge-a", "judge-b")
    ]
    record = _prompt_manual_record(
        public,
        sheets,
        prompt_id="P1",
        flags=["critical-gate", "judge-critical-cap"],
        rubric_score=8.0,
    )
    assert record["final_score"] == 2.0
    record["final_score"] = 4.0
    record["record_sha256"] = _canonical_hash(record, "record_sha256")
    reader = Mock(return_value=private)

    with pytest.raises(ValueError, match="capped"):
        adjudicate_adaptive_quality(
            scoring_input=public,
            judge_score_sheets=sheets,
            pairwise_reviews=_empty_pairwise(public),
            manual_adjudications=_manual_artifact(public, [record]),
            blind_map_reader=reader,
            rubric_path=RUBRIC,
            prompt_set_path=PROMPT_SET,
        )

    reader.assert_not_called()


def test_critical_gate_manual_record_supplies_deterministically_capped_replacement(
    tmp_path: Path,
    monkeypatch,
) -> None:
    public, private = _passing_inputs(tmp_path, monkeypatch)
    public, private = _with_critical_gate(public, private)
    sheets = [
        _set_prompt_score(_score_sheet(public, "judge-a", 8.0), "P1", 1.0),
        _set_prompt_score(_score_sheet(public, "judge-b", 8.0), "P1", 10.0),
    ]
    record = _prompt_manual_record(
        public,
        sheets,
        prompt_id="P1",
        flags=["critical-gate"],
        rubric_score=9.0,
    )
    reader = Mock(return_value=private)

    result = adjudicate_adaptive_quality(
        scoring_input=public,
        judge_score_sheets=sheets,
        pairwise_reviews=_empty_pairwise(public),
        manual_adjudications=_manual_artifact(public, [record]),
        blind_map_reader=reader,
        rubric_path=RUBRIC,
        prompt_set_path=PROMPT_SET,
    )

    reader.assert_called_once_with()
    assert record["final_score"] == 0.0
    assert result["configurations"][0]["prompt_scores"]["P1"] == 0.0


def test_disagreement_manual_record_replaces_only_the_flagged_prompt_mean(
    tmp_path: Path,
    monkeypatch,
) -> None:
    public, private = _passing_inputs(tmp_path, monkeypatch)
    sheets = [
        _score_sheet(public, "judge-a", 8.0),
        _set_prompt_score(_score_sheet(public, "judge-b", 8.0), "P1", 10.0),
    ]
    record = _prompt_manual_record(
        public,
        sheets,
        prompt_id="P1",
        flags=["judge-disagreement-over-one"],
        rubric_score=7.0,
    )

    result = adjudicate_adaptive_quality(
        scoring_input=public,
        judge_score_sheets=sheets,
        pairwise_reviews=_empty_pairwise(public),
        manual_adjudications=_manual_artifact(public, [record]),
        blind_map_reader=Mock(return_value=private),
        rubric_path=RUBRIC,
        prompt_set_path=PROMPT_SET,
    )

    assert result["configurations"][0]["prompt_scores"] == {
        "P1": 7.0,
        "P2": 8.0,
        "P3": 8.0,
        "P4": 8.0,
        "P5": 8.0,
        "P6": 8.0,
    }


def test_one_prompt_record_resolves_combined_gate_and_disagreement_flags(
    tmp_path: Path,
    monkeypatch,
) -> None:
    public, private = _passing_inputs(tmp_path, monkeypatch)
    public, private = _with_p1_cap_four(public, private)
    sheets = [
        _set_prompt_score(_score_sheet(public, "judge-a", 8.0), "P1", 0.0),
        _set_prompt_score(_score_sheet(public, "judge-b", 8.0), "P1", 10.0),
    ]
    record = _prompt_manual_record(
        public,
        sheets,
        prompt_id="P1",
        flags=["critical-gate", "judge-disagreement-over-one"],
        rubric_score=9.0,
    )

    result = adjudicate_adaptive_quality(
        scoring_input=public,
        judge_score_sheets=sheets,
        pairwise_reviews=_empty_pairwise(public),
        manual_adjudications=_manual_artifact(public, [record]),
        blind_map_reader=Mock(return_value=private),
        rubric_path=RUBRIC,
        prompt_set_path=PROMPT_SET,
    )

    assert record["final_score"] == 4.0
    assert result["configurations"][0]["prompt_scores"]["P1"] == 4.0


def test_pair_row_and_wrapper_hash_tampering_reject_before_unblind(
    tmp_path: Path,
    monkeypatch,
) -> None:
    public, private = _two_configuration_inputs(tmp_path, monkeypatch)
    sheets = [
        _score_sheet(public, "judge-a", 8.5),
        _score_sheet(public, "judge-b", 9.5),
    ]
    valid = _pairwise_artifact(public)
    cases = []
    row_tampered = json.loads(json.dumps(valid))
    row_tampered["reviews"][0]["review_sha256"] = "0" * 64
    row_tampered["pairwise_reviews_sha256"] = _canonical_hash(
        row_tampered, "pairwise_reviews_sha256"
    )
    cases.append((row_tampered, "pairwise review hash"))
    wrapper_tampered = json.loads(json.dumps(valid))
    wrapper_tampered["pairwise_reviews_sha256"] = "0" * 64
    cases.append((wrapper_tampered, "pairwise reviews hash"))

    for artifact, message in cases:
        reader = Mock(return_value=private)
        with pytest.raises(ValueError, match=message):
            adjudicate_adaptive_quality(
                scoring_input=public,
                judge_score_sheets=sheets,
                pairwise_reviews=artifact,
                manual_adjudications=_empty_manual(public),
                blind_map_reader=reader,
                rubric_path=RUBRIC,
                prompt_set_path=PROMPT_SET,
            )
        reader.assert_not_called()


def test_manual_wrapper_hash_tampering_rejects_before_unblind(
    tmp_path: Path,
    monkeypatch,
) -> None:
    public, private = _passing_inputs(tmp_path, monkeypatch)
    sheets = [
        _score_sheet(public, "judge-a", 8.0),
        _set_prompt_score(_score_sheet(public, "judge-b", 8.0), "P1", 10.0),
    ]
    record = _prompt_manual_record(
        public,
        sheets,
        prompt_id="P1",
        flags=["judge-disagreement-over-one"],
        rubric_score=7.0,
    )
    manual = _manual_artifact(public, [record])
    manual["manual_adjudications_sha256"] = "0" * 64
    reader = Mock(return_value=private)

    with pytest.raises(ValueError, match="manual adjudication artifact hash"):
        adjudicate_adaptive_quality(
            scoring_input=public,
            judge_score_sheets=sheets,
            pairwise_reviews=_empty_pairwise(public),
            manual_adjudications=manual,
            blind_map_reader=reader,
            rubric_path=RUBRIC,
            prompt_set_path=PROMPT_SET,
        )

    reader.assert_not_called()


def test_judge_sheet_cardinality_duplicate_and_reviewer_identity_reject_pre_unblind(
    tmp_path: Path,
    monkeypatch,
) -> None:
    public, private = _passing_inputs(tmp_path, monkeypatch)
    first = _score_sheet(public, "judge-a", 8.5)
    second = _score_sheet(public, "judge-b", 9.5)
    mismatched = json.loads(json.dumps(second))
    mismatched["adjudications"][0]["reviewer"] = "judge-a"
    mismatched["score_sheet_sha256"] = _canonical_hash(
        mismatched, "score_sheet_sha256"
    )
    cases = [
        ([first], "exactly two"),
        ([first, second, first], "exactly two"),
        ([first, first], "independent"),
        ([first, mismatched], "reviewer"),
    ]

    for sheets, message in cases:
        reader = Mock(return_value=private)
        with pytest.raises(ValueError, match=message):
            adjudicate_adaptive_quality(
                scoring_input=public,
                judge_score_sheets=sheets,
                pairwise_reviews=_empty_pairwise(public),
                manual_adjudications=_empty_manual(public),
                blind_map_reader=reader,
                rubric_path=RUBRIC,
                prompt_set_path=PROMPT_SET,
            )
        reader.assert_not_called()


def test_pair_manual_record_resolves_reversal_without_changing_numeric_scores(
    tmp_path: Path,
    monkeypatch,
) -> None:
    public, private = _two_configuration_inputs(tmp_path, monkeypatch)
    sheets = [
        _score_sheet(public, "judge-a", 8.0),
        _score_sheet(public, "judge-b", 8.0),
    ]
    pairwise = _pairwise_artifact(public, ranking_reversal=True)
    record = _pair_manual_record(public, pairwise)

    result = adjudicate_adaptive_quality(
        scoring_input=public,
        judge_score_sheets=sheets,
        pairwise_reviews=pairwise,
        manual_adjudications=_manual_artifact(public, [record]),
        blind_map_reader=Mock(return_value=private),
        rubric_path=RUBRIC,
        prompt_set_path=PROMPT_SET,
    )

    assert all(
        row["prompt_scores"] == {prompt_id: 8.0 for prompt_id in PROMPT_IDS}
        for row in result["configurations"]
    )
    assert result["pairwise_reviews"][0]["manual_resolution"] == record[
        "selected_winner"
    ]


@pytest.mark.parametrize(
    ("failure", "message"),
    [
        ("record-hash", "record hash"),
        ("stale-evidence", "evidence is stale"),
        ("missing-reason", "final reason"),
        ("duplicate", "duplicate manual adjudication"),
        ("unrequired", "unexpected manual adjudication"),
        ("above-cap", "final score"),
    ],
)
def test_invalid_stale_extra_manual_records_are_rejected_before_unblind(
    failure: str,
    message: str,
    tmp_path: Path,
    monkeypatch,
) -> None:
    public, private = _passing_inputs(tmp_path, monkeypatch)
    if failure == "above-cap":
        public, private = _with_critical_gate(public, private)
        sheets = [
            _score_sheet(public, "judge-a", 8.0),
            _score_sheet(public, "judge-b", 8.0),
        ]
        record = _prompt_manual_record(
            public,
            sheets,
            prompt_id="P1",
            flags=["critical-gate"],
            rubric_score=9.0,
        )
        record["final_score"] = 1.0
    elif failure == "unrequired":
        sheets = [
            _score_sheet(public, "judge-a", 8.0),
            _score_sheet(public, "judge-b", 8.0),
        ]
        record = _prompt_manual_record(
            public,
            sheets,
            prompt_id="P1",
            flags=["judge-disagreement-over-one"],
            rubric_score=7.0,
        )
    else:
        sheets = [
            _score_sheet(public, "judge-a", 8.0),
            _set_prompt_score(
                _score_sheet(public, "judge-b", 8.0), "P1", 10.0
            ),
        ]
        record = _prompt_manual_record(
            public,
            sheets,
            prompt_id="P1",
            flags=["judge-disagreement-over-one"],
            rubric_score=7.0,
        )
        if failure == "record-hash":
            record["record_sha256"] = "0" * 64
        elif failure == "stale-evidence":
            record["evidence"]["deterministic_gate_sha256"] = "0" * 64
        elif failure == "missing-reason":
            record["final_reason"] = ""
    if failure not in {"record-hash", "duplicate", "unrequired"}:
        record["record_sha256"] = _canonical_hash(record, "record_sha256")
    records = [record, json.loads(json.dumps(record))] if failure == "duplicate" else [record]
    reader = Mock(return_value=private)

    with pytest.raises(ValueError, match=message):
        adjudicate_adaptive_quality(
            scoring_input=public,
            judge_score_sheets=sheets,
            pairwise_reviews=_empty_pairwise(public),
            manual_adjudications=_manual_artifact(public, records),
            blind_map_reader=reader,
            rubric_path=RUBRIC,
            prompt_set_path=PROMPT_SET,
        )

    reader.assert_not_called()


def test_exactly_one_point_judge_difference_does_not_require_manual_record(
    tmp_path: Path,
    monkeypatch,
) -> None:
    public, private = _passing_inputs(tmp_path, monkeypatch)
    sheets = [
        _score_sheet(public, "judge-a", 8.0),
        _set_prompt_score(_score_sheet(public, "judge-b", 8.0), "P1", 9.0),
    ]

    result = adjudicate_adaptive_quality(
        scoring_input=public,
        judge_score_sheets=sheets,
        pairwise_reviews=_empty_pairwise(public),
        manual_adjudications=_empty_manual(public),
        blind_map_reader=Mock(return_value=private),
        rubric_path=RUBRIC,
        prompt_set_path=PROMPT_SET,
    )

    assert result["configurations"][0]["prompt_scores"]["P1"] == 8.5


def test_weight_literals_and_six_prompt_aggregates_are_exact(
    tmp_path: Path,
    monkeypatch,
) -> None:
    public, private = _passing_inputs(tmp_path, monkeypatch)
    dimensions = {
        "correctness_and_grounding": 10.0,
        "instruction_and_format_adherence": 8.0,
        "completeness_and_fact_retention": 6.0,
        "relevance_clarity_and_coherence": 4.0,
        "stability_and_output_integrity": 2.0,
    }
    sheets = [
        _set_prompt_dimensions(
            _score_sheet(public, judge_id, 8.0), "P1", dimensions
        )
        for judge_id in ("judge-a", "judge-b")
    ]

    result = adjudicate_adaptive_quality(
        scoring_input=public,
        judge_score_sheets=sheets,
        pairwise_reviews=_empty_pairwise(public),
        manual_adjudications=_empty_manual(public),
        blind_map_reader=Mock(return_value=private),
        rubric_path=RUBRIC,
        prompt_set_path=PROMPT_SET,
    )

    row = result["configurations"][0]
    assert row["prompt_scores"]["P1"] == 7.0
    assert row["aggregates"] == {
        "mean": 7.8333,
        "median": 8.0,
        "minimum": 7.0,
        "maximum": 8.0,
    }


def test_identical_content_cannot_receive_label_dependent_scores(
    tmp_path: Path,
    monkeypatch,
) -> None:
    public, private = _two_configuration_inputs(tmp_path, monkeypatch)
    labels = sorted({row["blind_label"] for row in public["responses"]})
    first = _set_prompt_dimensions(
        _score_sheet(public, "judge-a", 8.0),
        "P1",
        {name: 9.0 for name in DIMENSIONS},
        blind_label=labels[1],
    )
    reader = Mock(return_value=private)

    with pytest.raises(ValueError, match="label-dependent"):
        adjudicate_adaptive_quality(
            scoring_input=public,
            judge_score_sheets=[first, _score_sheet(public, "judge-b", 8.0)],
            pairwise_reviews=_pairwise_artifact(public),
            manual_adjudications=_empty_manual(public),
            blind_map_reader=reader,
            rubric_path=RUBRIC,
            prompt_set_path=PROMPT_SET,
        )

    reader.assert_not_called()


def test_judge_sheet_order_does_not_change_configuration_scores(
    tmp_path: Path,
    monkeypatch,
) -> None:
    public, private = _passing_inputs(tmp_path, monkeypatch)
    sheets = [
        _score_sheet(public, "judge-a", 8.5),
        _score_sheet(public, "judge-b", 9.5),
    ]

    forward = adjudicate_adaptive_quality(
        scoring_input=public,
        judge_score_sheets=sheets,
        pairwise_reviews=_empty_pairwise(public),
        manual_adjudications=_empty_manual(public),
        blind_map_reader=Mock(return_value=private),
        rubric_path=RUBRIC,
        prompt_set_path=PROMPT_SET,
    )
    reverse = adjudicate_adaptive_quality(
        scoring_input=public,
        judge_score_sheets=list(reversed(sheets)),
        pairwise_reviews=_empty_pairwise(public),
        manual_adjudications=_empty_manual(public),
        blind_map_reader=Mock(return_value=private),
        rubric_path=RUBRIC,
        prompt_set_path=PROMPT_SET,
    )

    assert forward["configurations"] == reverse["configurations"]


def test_manual_evidence_and_result_are_normalized_by_judge_identity(
    tmp_path: Path,
    monkeypatch,
) -> None:
    public, private = _passing_inputs(tmp_path, monkeypatch)
    sheets = [
        _score_sheet(public, "judge-a", 8.0),
        _set_prompt_score(_score_sheet(public, "judge-b", 8.0), "P1", 10.0),
    ]
    record = _prompt_manual_record(
        public,
        sheets,
        prompt_id="P1",
        flags=["judge-disagreement-over-one"],
        rubric_score=7.0,
    )
    manual = _manual_artifact(public, [record])

    forward = adjudicate_adaptive_quality(
        scoring_input=public,
        judge_score_sheets=sheets,
        pairwise_reviews=_empty_pairwise(public),
        manual_adjudications=manual,
        blind_map_reader=Mock(return_value=private),
        rubric_path=RUBRIC,
        prompt_set_path=PROMPT_SET,
    )
    reverse = adjudicate_adaptive_quality(
        scoring_input=public,
        judge_score_sheets=list(reversed(sheets)),
        pairwise_reviews=_empty_pairwise(public),
        manual_adjudications=manual,
        blind_map_reader=Mock(return_value=private),
        rubric_path=RUBRIC,
        prompt_set_path=PROMPT_SET,
    )

    assert [
        item["judge_id"]
        for item in record["evidence"]["judge_manual_critical_caps"]
    ] == ["judge-a", "judge-b"]
    assert forward == reverse


def test_build_bundle_cli_writes_fresh_artifacts_and_metadata_only_stdout(
    tmp_path: Path,
    monkeypatch,
    capsys,
) -> None:
    summary = _capture_summary(
        tmp_path / "capture", monkeypatch, runner=PassingGuardRunner()
    )
    capture_index = tmp_path / "capture-index.json"
    public_output = tmp_path / "out" / "scoring-input.json"
    private_output = tmp_path / "out" / "private-map.json"
    _write_json(capture_index, _capture_index([summary]))

    exit_code = main(
        [
            "build-bundle",
            "--capture-index",
            str(capture_index),
            "--prompt-set",
            str(PROMPT_SET),
            "--rubric",
            str(RUBRIC),
            "--public-output",
            str(public_output),
            "--private-map-output",
            str(private_output),
        ]
    )

    assert exit_code == 0
    public = json.loads(public_output.read_text(encoding="utf-8"))
    private = json.loads(private_output.read_text(encoding="utf-8"))
    stdout = capsys.readouterr().out
    metadata = json.loads(stdout)
    assert metadata == {
        "private_map_output": str(private_output.resolve()),
        "private_map_sha256": private["private_map_sha256"],
        "public_output": str(public_output.resolve()),
        "scoring_input_sha256": public["scoring_input_sha256"],
    }
    assert "responses" not in metadata and "mapping" not in metadata
    assert COMPLETE_OUTPUTS["P1"] not in stdout
    assert "OV-TQ-03" not in stdout
    with pytest.raises(FileExistsError, match="overwrite"):
        main(
            [
                "build-bundle",
                "--capture-index",
                str(capture_index),
                "--prompt-set",
                str(PROMPT_SET),
                "--rubric",
                str(RUBRIC),
                "--public-output",
                str(public_output),
                "--private-map-output",
                str(private_output),
            ]
        )


def test_build_cli_stages_both_artifacts_before_publishing_either(
    tmp_path: Path,
    monkeypatch,
    capsys,
) -> None:
    summary = _capture_summary(
        tmp_path / "capture", monkeypatch, runner=PassingGuardRunner()
    )
    capture_index = tmp_path / "capture-index.json"
    public_output = tmp_path / "out" / "scoring-input.json"
    private_output = tmp_path / "out" / "private-map.json"
    _write_json(capture_index, _capture_index([summary]))
    real_write = adaptive_adjudicator.atomic_write_json

    def fail_private_write(path, value):
        if (
            value.get("artifact_type")
            == "openvino-adaptive-quality-private-blind-map"
        ):
            raise OSError("injected private write failure")
        return real_write(path, value)

    monkeypatch.setattr(
        adaptive_adjudicator,
        "atomic_write_json",
        fail_private_write,
    )

    with pytest.raises(OSError, match="injected private write failure"):
        main(
            [
                "build-bundle",
                "--capture-index",
                str(capture_index),
                "--prompt-set",
                str(PROMPT_SET),
                "--rubric",
                str(RUBRIC),
                "--public-output",
                str(public_output),
                "--private-map-output",
                str(private_output),
            ]
        )

    assert not public_output.exists()
    assert not private_output.exists()
    assert not public_output.parent.exists() or not list(public_output.parent.iterdir())
    assert capsys.readouterr().out == ""


def test_build_cli_publishes_private_first_and_preserves_private_racer(
    tmp_path: Path,
    monkeypatch,
    capsys,
) -> None:
    summary = _capture_summary(
        tmp_path / "capture", monkeypatch, runner=PassingGuardRunner()
    )
    capture_index = tmp_path / "capture-index.json"
    public_output = tmp_path / "out" / "scoring-input.json"
    private_output = tmp_path / "out" / "private-map.json"
    _write_json(capture_index, _capture_index([summary]))
    real_link = adaptive_adjudicator.os.link
    sentinel = b"concurrent private evidence"
    destinations = []

    def race_private_commit(source, destination, *args, **kwargs):
        if Path(destination) not in {public_output, private_output}:
            return real_link(source, destination, *args, **kwargs)
        destinations.append(Path(destination))
        if Path(destination) == private_output:
            private_output.write_bytes(sentinel)
            raise FileExistsError("injected private commit race")
        return real_link(source, destination, *args, **kwargs)

    monkeypatch.setattr(adaptive_adjudicator.os, "link", race_private_commit)

    with pytest.raises(FileExistsError, match="overwrite"):
        main(
            [
                "build-bundle",
                "--capture-index",
                str(capture_index),
                "--prompt-set",
                str(PROMPT_SET),
                "--rubric",
                str(RUBRIC),
                "--public-output",
                str(public_output),
                "--private-map-output",
                str(private_output),
            ]
        )

    assert destinations == [private_output]
    assert private_output.read_bytes() == sentinel
    assert not public_output.exists()
    assert not any(
        path.name.startswith(".adaptive-quality-stage-")
        for path in private_output.parent.iterdir()
    )
    assert capsys.readouterr().out == ""


def test_build_cli_leaves_committed_private_and_preserves_public_racer(
    tmp_path: Path,
    monkeypatch,
    capsys,
) -> None:
    summary = _capture_summary(
        tmp_path / "capture", monkeypatch, runner=PassingGuardRunner()
    )
    capture_index = tmp_path / "capture-index.json"
    public_output = tmp_path / "out" / "scoring-input.json"
    private_output = tmp_path / "out" / "private-map.json"
    _write_json(capture_index, _capture_index([summary]))
    real_link = adaptive_adjudicator.os.link
    public_sentinel = b"concurrent public replacement"
    destinations = []

    def race_public_commit(source, destination, *args, **kwargs):
        if Path(destination) not in {public_output, private_output}:
            return real_link(source, destination, *args, **kwargs)
        destinations.append(Path(destination))
        if Path(destination) == public_output:
            public_output.write_bytes(public_sentinel)
            raise FileExistsError("injected public commit race")
        return real_link(source, destination, *args, **kwargs)

    monkeypatch.setattr(
        adaptive_adjudicator.os,
        "link",
        race_public_commit,
    )

    with pytest.raises(FileExistsError, match="overwrite"):
        main(
            [
                "build-bundle",
                "--capture-index",
                str(capture_index),
                "--prompt-set",
                str(PROMPT_SET),
                "--rubric",
                str(RUBRIC),
                "--public-output",
                str(public_output),
                "--private-map-output",
                str(private_output),
            ]
        )

    assert destinations == [private_output, public_output]
    private = json.loads(private_output.read_text(encoding="utf-8"))
    assert private["artifact_type"] == "openvino-adaptive-quality-private-blind-map"
    assert public_output.read_bytes() == public_sentinel
    assert not any(
        path.name.startswith(".adaptive-quality-stage-")
        for path in private_output.parent.iterdir()
    )
    assert capsys.readouterr().out == ""


def test_build_cli_detects_public_replacement_before_recording_ownership(
    tmp_path: Path,
    monkeypatch,
    capsys,
) -> None:
    summary = _capture_summary(
        tmp_path / "capture", monkeypatch, runner=PassingGuardRunner()
    )
    capture_index = tmp_path / "capture-index.json"
    public_output = tmp_path / "out" / "scoring-input.json"
    private_output = tmp_path / "out" / "private-map.json"
    _write_json(capture_index, _capture_index([summary]))
    real_link = adaptive_adjudicator.os.link
    public_sentinel = b"replacement inside public link call"
    destinations = []

    def replace_before_public_link_returns(source, destination, *args, **kwargs):
        if Path(destination) not in {public_output, private_output}:
            return real_link(source, destination, *args, **kwargs)
        destinations.append(Path(destination))
        result = real_link(source, destination, *args, **kwargs)
        if Path(destination) == public_output:
            public_output.unlink()
            public_output.write_bytes(public_sentinel)
        return result

    monkeypatch.setattr(
        adaptive_adjudicator.os,
        "link",
        replace_before_public_link_returns,
    )

    with pytest.raises(FileExistsError, match="overwrite"):
        main(
            [
                "build-bundle",
                "--capture-index",
                str(capture_index),
                "--prompt-set",
                str(PROMPT_SET),
                "--rubric",
                str(RUBRIC),
                "--public-output",
                str(public_output),
                "--private-map-output",
                str(private_output),
            ]
        )

    assert destinations == [private_output, public_output]
    private = json.loads(private_output.read_text(encoding="utf-8"))
    assert private["artifact_type"] == "openvino-adaptive-quality-private-blind-map"
    assert public_output.read_bytes() == public_sentinel
    assert not any(
        path.name.startswith(".adaptive-quality-stage-")
        for path in public_output.parent.iterdir()
    )
    assert capsys.readouterr().out == ""


def test_build_cli_rejects_public_parent_alias_inserted_during_link(
    tmp_path: Path,
    monkeypatch,
    capsys,
) -> None:
    summary = _capture_summary(
        tmp_path / "capture", monkeypatch, runner=PassingGuardRunner()
    )
    capture_index = tmp_path / "capture-index.json"
    public_parent = tmp_path / "public-out"
    public_output = public_parent / "scoring-input.json"
    private_output = tmp_path / "private-out" / "private-map.json"
    alias_target = tmp_path / "public-racer-target"
    alias_artifact = alias_target / public_output.name
    _write_json(capture_index, _capture_index([summary]))
    real_link = adaptive_adjudicator.os.link
    destinations = []

    def insert_parent_alias(source, destination, *args, **kwargs):
        if Path(destination) not in {public_output, private_output}:
            return real_link(source, destination, *args, **kwargs)
        destinations.append(Path(destination))
        result = real_link(source, destination, *args, **kwargs)
        if Path(destination) == public_output:
            alias_target.mkdir()
            real_link(source, alias_artifact)
            stage_root = Path(source).parent
            stage_root.rename(alias_target / stage_root.name)
            public_output.unlink()
            public_parent.rmdir()
            _directory_alias_or_skip(public_parent, alias_target)
        return result

    monkeypatch.setattr(adaptive_adjudicator.os, "link", insert_parent_alias)

    try:
        with pytest.raises(ValueError, match="alias"):
            main(
                [
                    "build-bundle",
                    "--capture-index",
                    str(capture_index),
                    "--prompt-set",
                    str(PROMPT_SET),
                    "--rubric",
                    str(RUBRIC),
                    "--public-output",
                    str(public_output),
                    "--private-map-output",
                    str(private_output),
                ]
            )

        assert destinations == [private_output, public_output]
        assert private_output.is_file()
        assert public_output.read_bytes() == alias_artifact.read_bytes()
        assert capsys.readouterr().out == ""
    finally:
        _remove_directory_alias(public_parent)


def test_build_cli_detects_in_place_private_hash_mutation_before_publication(
    tmp_path: Path,
    monkeypatch,
    capsys,
) -> None:
    summary = _capture_summary(
        tmp_path / "capture", monkeypatch, runner=PassingGuardRunner()
    )
    capture_index = tmp_path / "capture-index.json"
    public_output = tmp_path / "out" / "scoring-input.json"
    private_output = tmp_path / "out" / "private-map.json"
    _write_json(capture_index, _capture_index([summary]))
    real_link = adaptive_adjudicator.os.link
    tampered_private = b"in-place private mutation"
    destinations = []

    def mutate_private_link(source, destination, *args, **kwargs):
        if Path(destination) not in {public_output, private_output}:
            return real_link(source, destination, *args, **kwargs)
        destinations.append(Path(destination))
        result = real_link(source, destination, *args, **kwargs)
        if Path(destination) == private_output:
            private_output.write_bytes(tampered_private)
        return result

    monkeypatch.setattr(adaptive_adjudicator.os, "link", mutate_private_link)

    with pytest.raises(ValueError, match="hash"):
        main(
            [
                "build-bundle",
                "--capture-index",
                str(capture_index),
                "--prompt-set",
                str(PROMPT_SET),
                "--rubric",
                str(RUBRIC),
                "--public-output",
                str(public_output),
                "--private-map-output",
                str(private_output),
            ]
        )

    assert destinations == [private_output]
    assert private_output.read_bytes() == tampered_private
    assert not public_output.exists()
    assert capsys.readouterr().out == ""


def test_build_cli_never_unlinks_final_paths_on_publication_failure(
    tmp_path: Path,
    monkeypatch,
    capsys,
) -> None:
    summary = _capture_summary(
        tmp_path / "capture", monkeypatch, runner=PassingGuardRunner()
    )
    capture_index = tmp_path / "capture-index.json"
    public_output = tmp_path / "out" / "scoring-input.json"
    private_output = tmp_path / "out" / "private-map.json"
    _write_json(capture_index, _capture_index([summary]))
    real_link = adaptive_adjudicator.os.link
    real_unlink = Path.unlink
    private_sentinel = b"concurrent private evidence"
    final_unlinks = []

    def fail_private_commit(source, destination, *args, **kwargs):
        if Path(destination) == private_output:
            private_output.write_bytes(private_sentinel)
            raise FileExistsError("injected private commit race")
        return real_link(source, destination, *args, **kwargs)

    def observe_final_unlink(path, *args, **kwargs):
        if path in {public_output, private_output}:
            final_unlinks.append(path)
        return real_unlink(path, *args, **kwargs)

    monkeypatch.setattr(adaptive_adjudicator.os, "link", fail_private_commit)
    monkeypatch.setattr(Path, "unlink", observe_final_unlink)

    with pytest.raises(FileExistsError, match="overwrite"):
        main(
            [
                "build-bundle",
                "--capture-index",
                str(capture_index),
                "--prompt-set",
                str(PROMPT_SET),
                "--rubric",
                str(RUBRIC),
                "--public-output",
                str(public_output),
                "--private-map-output",
                str(private_output),
            ]
        )

    assert final_unlinks == []
    assert private_output.read_bytes() == private_sentinel
    assert not public_output.exists()
    assert capsys.readouterr().out == ""


def test_cli_script_is_directly_executable_from_repository_root() -> None:
    script = ROOT / "scripts" / "testing" / "adjudicate_official_openvino_adaptive_quality.py"

    completed = subprocess.run(
        [sys.executable, str(script), "--help"],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=False,
    )

    assert completed.returncode == 0, completed.stderr
    assert "build-bundle" in completed.stdout
    assert "adjudicate" in completed.stdout


def test_adjudicate_cli_requires_exactly_two_sheets_and_writes_complete_result(
    tmp_path: Path,
    monkeypatch,
    capsys,
) -> None:
    public, private = _passing_inputs(tmp_path / "capture", monkeypatch)
    paths = {
        "scoring": tmp_path / "scoring.json",
        "judge_a": tmp_path / "judge-a.json",
        "judge_b": tmp_path / "judge-b.json",
        "pairwise": tmp_path / "pairwise.json",
        "manual": tmp_path / "manual.json",
        "private": tmp_path / "private.json",
        "output": tmp_path / "adjudication.json",
    }
    _write_json(paths["scoring"], public)
    _write_json(paths["judge_a"], _score_sheet(public, "judge-a", 8.5))
    _write_json(paths["judge_b"], _score_sheet(public, "judge-b", 9.5))
    _write_json(paths["pairwise"], _empty_pairwise(public))
    _write_json(paths["manual"], _empty_manual(public))
    _write_json(paths["private"], private)
    base_args = [
        "adjudicate",
        "--scoring-input",
        str(paths["scoring"]),
        "--pairwise-reviews",
        str(paths["pairwise"]),
        "--manual-adjudications",
        str(paths["manual"]),
        "--private-map",
        str(paths["private"]),
        "--prompt-set",
        str(PROMPT_SET),
        "--rubric",
        str(RUBRIC),
        "--output",
        str(paths["output"]),
    ]
    with pytest.raises(SystemExit) as wrong_count:
        parse_args(
            base_args
            + ["--judge-score-sheet", str(paths["judge_a"])]
        )
    assert wrong_count.value.code != 0
    assert not paths["output"].exists()
    assert capsys.readouterr().out == ""
    with pytest.raises(SystemExit) as too_many:
        parse_args(
            base_args
            + [
                "--judge-score-sheet",
                str(paths["judge_a"]),
                "--judge-score-sheet",
                str(paths["judge_b"]),
                "--judge-score-sheet",
                str(paths["judge_a"]),
            ]
        )
    assert too_many.value.code != 0
    assert not paths["output"].exists()
    assert capsys.readouterr().out == ""

    exit_code = main(
        base_args
        + [
            "--judge-score-sheet",
            str(paths["judge_a"]),
            "--judge-score-sheet",
            str(paths["judge_b"]),
        ]
    )

    assert exit_code == 0
    result = json.loads(paths["output"].read_text(encoding="utf-8"))
    assert result["adjudication_sha256"] == _canonical_hash(
        result, "adjudication_sha256"
    )
    assert result["configurations"][0]["prompt_scores"] == {
        prompt_id: 9.0 for prompt_id in PROMPT_IDS
    }
    stdout = capsys.readouterr().out
    assert json.loads(stdout) == {
        "adjudication_sha256": result["adjudication_sha256"],
        "output": str(paths["output"].resolve()),
    }
    assert COMPLETE_OUTPUTS["P1"] not in stdout
    assert "OV-TQ-03" not in stdout


@pytest.mark.parametrize("occupied", ("public", "private"))
def test_build_cli_preflights_both_outputs_before_creating_either(
    occupied: str,
    tmp_path: Path,
) -> None:
    public_output = tmp_path / "public.json"
    private_output = tmp_path / "private.json"
    occupied_path = public_output if occupied == "public" else private_output
    other_path = private_output if occupied == "public" else public_output
    sentinel = b"existing evidence must remain unchanged"
    occupied_path.write_bytes(sentinel)

    with pytest.raises(FileExistsError, match="overwrite"):
        main(
            [
                "build-bundle",
                "--capture-index",
                str(tmp_path / "missing-index.json"),
                "--prompt-set",
                str(PROMPT_SET),
                "--rubric",
                str(RUBRIC),
                "--public-output",
                str(public_output),
                "--private-map-output",
                str(private_output),
            ]
        )

    assert occupied_path.read_bytes() == sentinel
    assert not other_path.exists()


def test_build_cli_rejects_tampered_capture_index_without_partial_outputs(
    tmp_path: Path,
) -> None:
    capture_index = _capture_index([tmp_path / "missing-summary.json"])
    capture_index["capture_index_sha256"] = "0" * 64
    index_path = tmp_path / "capture-index.json"
    public_output = tmp_path / "public.json"
    private_output = tmp_path / "private.json"
    _write_json(index_path, capture_index)

    with pytest.raises(ValueError, match="capture index hash"):
        main(
            [
                "build-bundle",
                "--capture-index",
                str(index_path),
                "--prompt-set",
                str(PROMPT_SET),
                "--rubric",
                str(RUBRIC),
                "--public-output",
                str(public_output),
                "--private-map-output",
                str(private_output),
            ]
        )

    assert not public_output.exists()
    assert not private_output.exists()


@pytest.mark.parametrize(
    ("tampered_artifact", "message"),
    [("scoring-input", "scoring input hash"), ("private-map", "private blind map hash")],
)
def test_adjudicate_cli_rejects_tampered_hashes_without_numeric_output(
    tampered_artifact: str,
    message: str,
    tmp_path: Path,
    monkeypatch,
    capsys,
) -> None:
    public, private = _passing_inputs(tmp_path / "capture", monkeypatch)
    if tampered_artifact == "scoring-input":
        public["scoring_input_sha256"] = "0" * 64
    else:
        private["private_map_sha256"] = "0" * 64
    paths = {
        "scoring": tmp_path / "scoring.json",
        "judge_a": tmp_path / "judge-a.json",
        "judge_b": tmp_path / "judge-b.json",
        "pairwise": tmp_path / "pairwise.json",
        "manual": tmp_path / "manual.json",
        "private": tmp_path / "private.json",
        "output": tmp_path / "result.json",
    }
    scoring_for_reviews = (
        {**public, "scoring_input_sha256": private["scoring_input_sha256"]}
        if tampered_artifact == "scoring-input"
        else public
    )
    _write_json(paths["scoring"], public)
    _write_json(paths["judge_a"], _score_sheet(scoring_for_reviews, "judge-a", 8.5))
    _write_json(paths["judge_b"], _score_sheet(scoring_for_reviews, "judge-b", 9.5))
    _write_json(paths["pairwise"], _empty_pairwise(scoring_for_reviews))
    _write_json(paths["manual"], _empty_manual(scoring_for_reviews))
    _write_json(paths["private"], private)

    with pytest.raises(ValueError, match=message):
        main(
            [
                "adjudicate",
                "--scoring-input",
                str(paths["scoring"]),
                "--judge-score-sheet",
                str(paths["judge_a"]),
                "--judge-score-sheet",
                str(paths["judge_b"]),
                "--pairwise-reviews",
                str(paths["pairwise"]),
                "--manual-adjudications",
                str(paths["manual"]),
                "--private-map",
                str(paths["private"]),
                "--prompt-set",
                str(PROMPT_SET),
                "--rubric",
                str(RUBRIC),
                "--output",
                str(paths["output"]),
            ]
        )

    assert not paths["output"].exists()
    assert capsys.readouterr().out == ""
