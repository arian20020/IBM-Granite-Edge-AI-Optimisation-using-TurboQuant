from __future__ import annotations

import csv
from dataclasses import replace
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys

import pytest


AUTHORITATIVE_REPO_ROOT = Path(__file__).resolve().parents[4]
REPO_ROOT = AUTHORITATIVE_REPO_ROOT
sys.path.insert(0, str(REPO_ROOT))

from scripts.testing.reporting.models import Status
from scripts.testing.reporting.evidence import (
    repo_relative,
    resolve_repository_path,
)
import scripts.testing.reporting.openvino_adapter as openvino_adapter
from scripts.testing.tests.integration.canonical_fixture import (
    materialize_exact_retained_alias,
    materialize_openvino_canonical_repository,
)
from scripts.testing.reporting.openvino_adapter import (
    build_official_bundle,
    build_official_validation_receipts,
    write_official_route,
)


FV1 = Path("experiments/raw-results/openvino-official-upstream/2026-08-30/fv1")
FV2 = Path(
    "experiments/raw-results/openvino-official-upstream/2026-08-30/"
    "fv2-missing-model-attempts"
)
V1_WORKBOOK = Path(
    "outputs/openvino-official-upstream-results/"
    "Granite_Official_OpenVINO_TurboQuant_Results_2026-08-30.xlsx"
)
V2_WORKBOOK = Path(
    "outputs/openvino-official-upstream-results/"
    "Granite_Official_OpenVINO_TurboQuant_Results_2026-08-30_v2_Missing_Attempts.xlsx"
)
ROUTE = REPO_ROOT / "docs/testing/final-results/05-openvino-official-upstream"


def _canonical_source_path(repo_root: Path, relative: Path) -> Path:
    return resolve_repository_path(repo_root, relative, prefer_migrated=True)


def _canonical_relative(repo_root: Path, relative: Path) -> str:
    return repo_relative(repo_root, _canonical_source_path(repo_root, relative))


def _rows(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def _write_rows(path: Path, rows: list[dict[str, str]]) -> None:
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=tuple(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def _isolated_official_repo(tmp_path: Path) -> Path:
    native_repo = (tmp_path / "repo").resolve()
    repo = Path(f"\\\\?\\{native_repo}") if os.name == "nt" else native_repo
    (repo / FV1.parent).mkdir(parents=True)
    shutil.copytree(_canonical_source_path(REPO_ROOT, FV1), repo / FV1)
    shutil.copytree(_canonical_source_path(REPO_ROOT, FV2), repo / FV2)
    for relative in (
        FV2 / "source-models.json",
        FV2 / "guarded-retry-001/source-models.json",
        FV2 / "guarded-retry-002/source-models.json",
    ):
        target = repo / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        canonical = _canonical_source_path(REPO_ROOT, relative)
        if canonical.is_file():
            shutil.copyfile(canonical, target)
        else:
            materialize_exact_retained_alias(
                AUTHORITATIVE_REPO_ROOT, repo, relative.as_posix()
            )
    for relative in (V1_WORKBOOK, V2_WORKBOOK):
        target = repo / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(_canonical_source_path(REPO_ROOT, relative), target)
    return repo


@pytest.fixture(scope="module", autouse=True)
def _canonical_official_repository(tmp_path_factory):
    """Keep direct bundle/write tests off the cleaned implementation tree."""
    global REPO_ROOT, ROUTE
    original_root, original_route = REPO_ROOT, ROUTE
    fixture = materialize_openvino_canonical_repository(
        AUTHORITATIVE_REPO_ROOT,
        tmp_path_factory.mktemp("canonical-official"),
    )
    REPO_ROOT = fixture
    ROUTE = fixture / "docs/testing/final-results/05-openvino-official-upstream"
    try:
        yield
    finally:
        REPO_ROOT, ROUTE = original_root, original_route


def _rewrite_json(repo: Path, relative: Path, mutate) -> None:
    path = repo / relative
    payload = json.loads(path.read_text(encoding="utf-8"))
    mutate(payload)
    path.write_text(
        json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")),
        encoding="utf-8",
    )


def _rewrite_raw(repo: Path, case_id: str, mutate) -> None:
    relative = FV1 / "raw" / f"{case_id}.json"
    _rewrite_json(repo, relative, mutate)
    raw_path = repo / relative
    digest = hashlib.sha256(raw_path.read_bytes()).hexdigest()
    detailed_path = repo / FV1 / "official-openvino-detailed-results.csv"
    detailed = _rows(detailed_path)
    next(row for row in detailed if row["case_id"] == case_id)[
        "raw_result_sha256"
    ] = digest
    _write_rows(detailed_path, detailed)
    fv2_path = repo / FV2 / "consolidated/official-openvino-detailed-results.csv"
    fv2 = _rows(fv2_path)
    next(row for row in fv2 if row["case_id"] == case_id)[
        "raw_result_sha256"
    ] = digest
    _write_rows(fv2_path, fv2)


def test_official_campaign_joins_fv2_statuses_to_only_fv1_passed_evidence():
    bundle = build_official_bundle(REPO_ROOT)

    assert bundle.route_id == "openvino-official-upstream"
    assert bundle.campaign_id == "fv2-2026-08-30"
    assert len(bundle.attempts) == 45
    assert sum(item.status is Status.PASSED for item in bundle.attempts) == 15
    assert sum(item.status is Status.FAILED for item in bundle.attempts) == 5
    assert sum(item.status is Status.BLOCKED for item in bundle.attempts) == 25
    assert sum(item.executed for item in bundle.attempts) == 15
    assert len(bundle.measurements) == 45
    assert len(bundle.summaries) == 45
    assert len(bundle.quality) == 2_160
    assert len(bundle.failures) == 30

    passed = {item.test_case_id for item in bundle.attempts if item.status is Status.PASSED}
    assert {item.test_case_id for item in bundle.measurements} == passed
    assert {item.test_case_id for item in bundle.summaries} == passed
    assert {item.test_case_id for item in bundle.quality} == passed
    assert not ({item.test_case_id for item in bundle.failures} & passed)

    cache_ids = {item.cache_format_id for item in bundle.attempts}
    assert {"tbq3", "tbq4"} <= cache_ids
    assert not cache_ids & {"polar3", "polar4", "qjl3", "qjl4"}
    assert all("polar" not in value and "qjl" not in value for value in cache_ids)

    failed = next(
        item for item in bundle.failures
        if item.test_case_id == "granite-3b__fp16__tbq3"
    )
    assert failed.status is Status.FAILED
    assert failed.source_status == "conversion_failed"
    assert failed.stage == "model_conversion"
    assert failed.reason == "emergency_ram_floor_reached"
    blocked = next(
        item for item in bundle.failures
        if item.test_case_id == "granite-30b__int4__tbq4"
    )
    assert blocked.status is Status.BLOCKED
    assert blocked.source_status == "hardware_preflight_blocked"
    assert blocked.stage == "conversion_preflight"
    assert blocked.reason == (
        "official source metadata proves conversion cannot preserve the 2 GiB "
        "emergency RAM floor on this host"
    )
    assert any(
        item.role == "missing-model-attempt-manifest"
        and item.relative_path.endswith("attempts/granite-30b__int4/manifest.json")
        and item.evidence_id in blocked.evidence_ids
        for item in bundle.evidence
    )


def test_official_bundle_publishes_source_derived_identities_and_lineage():
    bundle = build_official_bundle(REPO_ROOT)
    case_id = "granite-3b__int4__tbq3"
    attempt = next(item for item in bundle.attempts if item.test_case_id == case_id)
    assert attempt.attempt_id == f"{case_id}--attempt-001"
    measurements = [item for item in bundle.measurements if item.test_case_id == case_id]
    assert [item.measurement_id for item in measurements] == [
        f"{case_id}--benchmark-repetition-001",
        f"{case_id}--benchmark-repetition-002",
        f"{case_id}--benchmark-repetition-003",
    ]
    assert [item.run_id for item in measurements] == [f"{case_id}--benchmark"] * 3
    assert [item.repetition_id for item in measurements] == ["001", "002", "003"]
    raw = json.loads(
        _canonical_source_path(REPO_ROOT, FV1 / "raw" / f"{case_id}.json").read_text()
    )
    assert [item.latency_ms for item in measurements] == [
        float(run["result"]["ttft_ms"]) for run in raw["benchmark_runs"]
    ]
    assert [item.generation_tokens_per_second for item in measurements] == [
        float(run["result"]["decode_tps"]) for run in raw["benchmark_runs"]
    ]
    lineage = tuple(item.measurement_id for item in measurements)
    summaries = {item.metric_name: item for item in bundle.summaries if item.test_case_id == case_id}
    assert set(summaries) == {
        "generation_tokens_per_second",
        "time_to_first_token",
        "peak_working_set_bytes",
    }
    assert all(item.source_measurement_ids == lineage for item in summaries.values())
    assert summaries["generation_tokens_per_second"].value == 17.751513
    assert summaries["time_to_first_token"].value == 4413.813965
    assert summaries["peak_working_set_bytes"].value == 3_635_527_000

    quality = next(
        item for item in bundle.quality
        if item.test_case_id == case_id and item.prompt_id == "Q01"
        and item.criterion_id == "safety:primary"
    )
    assert quality.quality_id == f"{case_id}--Q01--safety--primary"
    assert quality.prompt_suite_id == "OPENVINO-SECTOR-EXPERIENCE-QUALITY-v3"
    assert quality.rubric_id == "objective-quality-weighted-5-3-2-output-health-gate"
    assert quality.scoring_version == "experimental-openvino-objective-quality/v2"


def test_official_generation_writes_common_route_and_copies_only_primary_workbook():
    bundle = write_official_route(REPO_ROOT)

    expected = {
        "README.md",
        "data/route.json",
        "reproduction/protocol/intended-test-matrix.csv",
        "reproduction/system/repository.json",
        "reproduction/system/hardware.json",
        "reproduction/system/software.json",
        "reproduction/system/model-artifacts.csv",
        "data/attempts.csv",
        "data/measurements.csv",
        "data/summaries.csv",
        "data/availability-matrix.csv",
        f"evidence/source/{V2_WORKBOOK.name}",
        "reproduction/quality/prompt-suite.csv",
        "data/quality.csv",
        "reproduction/quality/outputs-index.csv",
        "data/failures.csv",
        "evidence/evidence-index.csv",
        "evidence/source-locations.csv",
        "evidence/claim-evidence-map.csv",
        "evidence/manifest-sha256.txt",
        "reproduction/README.md",
        "validation/validation.json",
        "validation/validation.md",
        "reports/openvino-official-upstream-report.md",
        "reports/openvino-official-upstream-report.docx",
        "reports/openvino-official-upstream-report.pdf",
        "reports/openvino-official-upstream-results.xlsx",
        "reports/openvino-official-upstream-results-provenance.json",
    }
    actual = {path.relative_to(ROUTE).as_posix() for path in ROUTE.rglob("*") if path.is_file()}
    assert actual == expected
    reproduction = (ROUTE / "reproduction/README.md").read_text(encoding="utf-8")
    assert "python -m scripts.testing.cli.validate_results --route official-openvino" in reproduction
    assert "do not rerun" in reproduction.casefold()
    assert "do not modify evidence" in reproduction.casefold()
    assert "scripts.testing.reporting" not in reproduction
    assert "write_official_route" not in reproduction
    copied = ROUTE / "evidence/source" / V2_WORKBOOK.name
    assert copied.read_bytes() == (REPO_ROOT / V2_WORKBOOK).read_bytes()
    assert not (ROUTE / "evidence/source" / V1_WORKBOOK.name).exists()
    assert any(
        item.role == "indexed-prior-workbook"
        and item.relative_path == V1_WORKBOOK.as_posix()
        and item.sha256 == "b3e26eae69c3854dec26536c6d141943572292f8a9ea331cb4de1d88c76b32b4"
        for item in bundle.evidence
    )
    primary = next(item for item in bundle.evidence if item.role == "source-workbook")
    assert primary.relative_path == V2_WORKBOOK.as_posix()
    assert primary.sha256 == "1d5fc2893e0c7f412140b3fa1a26c4a0c18e3c65ecfa356e80549dc4cd10aff7"

    validation = json.loads((ROUTE / "validation/validation.json").read_text())
    coverage = validation["checks"]["coverage"]
    data = validation["checks"]["data"]
    assert coverage["valid"] is True
    assert data["valid"] is True
    assert all(check["passed"] for check in coverage["checks"].values())
    assert all(check["passed"] for check in data["checks"].values())


def test_official_evidence_inventory_covers_logs_aliases_and_preflight_inputs():
    bundle = write_official_route(REPO_ROOT)
    by_path = {item.relative_path: item for item in bundle.evidence}
    conversion_path = _canonical_relative(
        REPO_ROOT,
        FV2 / "guarded-retry-002/attempts/granite-3b__fp16/conversion.log",
    )
    conversion = by_path[conversion_path]
    assert conversion.role == "conversion-log"
    assert conversion.sha256 == "36d15156288619f17c972b5f0af9bc5380fe363cab90a8356feac4e5ca579d65"
    assert all(
        conversion.evidence_id in failure.evidence_ids
        for failure in bundle.failures
        if failure.test_case_id.startswith("granite-3b__fp16__")
    )

    preflight_paths = {
        _canonical_relative(
            REPO_ROOT,
            FV1 / "preflight/inputs/314ad142957febe390cc7223b4deb1d1b21c187f84f6e7257a23fe46c27fcae3.txt",
        ): "314ad142957febe390cc7223b4deb1d1b21c187f84f6e7257a23fe46c27fcae3",
        _canonical_relative(
            REPO_ROOT,
            FV1 / "preflight/inputs/ca101275d196803be37cb8fae1b81f1a7b2db733b7c1629293aae61465b2b3a0.txt",
        ): "ca101275d196803be37cb8fae1b81f1a7b2db733b7c1629293aae61465b2b3a0",
    }
    preflight_ids = []
    for path, digest in preflight_paths.items():
        item = by_path[path]
        assert item.role == "preflight-input"
        assert item.sha256 == digest
        preflight_ids.append(item.evidence_id)
    receipt = next(item for item in bundle.evidence if item.role == "source-preflight")
    assert set(receipt.input_evidence_ids) == set(preflight_ids)

    benchmark_path = _canonical_relative(
        REPO_ROOT,
        FV1
        / "inputs/f2b9e8f0f10053f3a04e1532ecd1e66d026ba1f37e7cde636bc2b5e2748c301c.txt",
    )
    benchmark = by_path[benchmark_path]
    assert benchmark.role == "benchmark-prompt-input"
    assert benchmark.sha256 == benchmark_path.rsplit("/", 1)[-1].removesuffix(".txt")
    raw_records = [item for item in bundle.evidence if item.role == "raw-case-result"]
    assert raw_records
    assert all(benchmark.evidence_id in item.input_evidence_ids for item in raw_records)

    source_model_paths = {
        _canonical_relative(REPO_ROOT, FV2 / "source-models.json"),
        _canonical_relative(
            REPO_ROOT, FV2 / "guarded-retry-001/source-models.json"
        ),
        _canonical_relative(
            REPO_ROOT, FV2 / "guarded-retry-002/source-models.json"
        ),
    }
    locations = _rows(ROUTE / "evidence/source-locations.csv")
    aliases = [
        row for row in locations
        if row["role"] == "missing-model-source-inventory"
    ]
    assert {row["relative_path"] for row in aliases} == source_model_paths
    assert {row["sha256"] for row in aliases} == {
        "5f3981b202e5b968533739c66f1dc998bb50311dc89ada3ef3da13db9c2afae4"
    }
    assert {int(row["size_bytes"]) for row in aliases} == {7_932}
    assert len({row["evidence_id"] for row in aliases}) == 1
    assert all(row["source_label"] for row in aliases)
    content_id = aliases[0]["evidence_id"]
    assert any(item.evidence_id == content_id for item in bundle.evidence)


def test_official_validation_expectations_do_not_call_bundle_builder(monkeypatch):
    bundle = build_official_bundle(REPO_ROOT)

    def forbidden_builder(_repo_root):
        raise AssertionError("validation must not call build_official_bundle")

    monkeypatch.setattr(openvino_adapter, "build_official_bundle", forbidden_builder)
    coverage, data = build_official_validation_receipts(REPO_ROOT, bundle)

    assert coverage["valid"] is True
    assert data["valid"] is True


def test_official_rejects_missing_required_log_alias_or_preflight_input(tmp_path):
    paths = (
        FV2 / "guarded-retry-002/attempts/granite-3b__fp16/conversion.log",
        FV2 / "source-models.json",
        FV1 / "preflight/inputs/314ad142957febe390cc7223b4deb1d1b21c187f84f6e7257a23fe46c27fcae3.txt",
        FV1 / "inputs/f2b9e8f0f10053f3a04e1532ecd1e66d026ba1f37e7cde636bc2b5e2748c301c.txt",
    )
    for index, relative in enumerate(paths):
        repo = _isolated_official_repo(tmp_path / str(index))
        (repo / relative).unlink()
        with pytest.raises((FileNotFoundError, ValueError), match="evidence|conversion|source-models|preflight"):
            build_official_bundle(repo)


def test_official_rejects_changed_shared_benchmark_input_bytes(tmp_path):
    repo = _isolated_official_repo(tmp_path)
    relative = (
        FV1
        / "inputs/f2b9e8f0f10053f3a04e1532ecd1e66d026ba1f37e7cde636bc2b5e2748c301c.txt"
    )
    path = repo / relative
    path.write_bytes(path.read_bytes() + b"\nchanged benchmark input")

    with pytest.raises(ValueError, match="benchmark input.*hash|hash.*benchmark input"):
        build_official_bundle(repo)


@pytest.mark.parametrize(
    "name",
    (
        "314ad142957febe390cc7223b4deb1d1b21c187f84f6e7257a23fe46c27fcae3.txt",
        "ca101275d196803be37cb8fae1b81f1a7b2db733b7c1629293aae61465b2b3a0.txt",
    ),
)
def test_official_rejects_changed_preflight_input_bytes(tmp_path, name):
    repo = _isolated_official_repo(tmp_path)
    path = repo / FV1 / "preflight/inputs" / name
    path.write_bytes(path.read_bytes() + b"\nchanged preflight input")

    with pytest.raises(ValueError, match="preflight input.*hash|hash.*preflight input"):
        build_official_bundle(repo)


def test_official_rejects_changed_source_model_alias_bytes(tmp_path):
    repo = _isolated_official_repo(tmp_path)
    path = repo / FV2 / "guarded-retry-001/source-models.json"
    path.write_bytes(path.read_bytes() + b" ")

    with pytest.raises(ValueError, match="source-model alias.*conflict"):
        build_official_bundle(repo)


@pytest.mark.parametrize(
    ("case_id", "algorithm", "precision"),
    (
        ("granite-3b__int4__tbq3", "SCALAR", "u3"),
        ("granite-3b__int4__tbq4", "SCALAR", "u4"),
        ("granite-3b__int4__u4", "TURBO", "u4"),
        ("granite-3b__int4__u8", "SCALAR", "u4"),
        ("granite-3b__int4__f16", "SCALAR", "f16"),
    ),
)
def test_official_rejects_synchronized_cache_activation_mislabeling(
    tmp_path, case_id, algorithm, precision
):
    repo = _isolated_official_repo(tmp_path)

    def mutate_raw(payload):
        properties = {"ATTENTION_BACKEND": "SDPA"}
        properties.update(
            {
                "KEY_CACHE_QUANT_ALG": algorithm,
                "VALUE_CACHE_QUANT_ALG": algorithm,
                "KEY_CACHE_PRECISION": precision,
                "VALUE_CACHE_PRECISION": precision,
            }
        )
        payload["runtime_properties"] = properties
        for run in payload["benchmark_runs"] + payload["quality_runs"]:
            run["result"]["runtime_properties"] = dict(properties)
            run["stdout"] = json.dumps(run["result"], separators=(",", ":")) + "\n"
        payload["benchmark"] = next(
            run for run in payload["benchmark_runs"]
            if run["result"]["decode_tps"]
            == sorted(item["result"]["decode_tps"] for item in payload["benchmark_runs"])[1]
        )

    _rewrite_raw(repo, case_id, mutate_raw)
    for relative in (
        FV1 / "official-openvino-detailed-results.csv",
        FV2 / "consolidated/official-openvino-detailed-results.csv",
    ):
        path = repo / relative
        rows = _rows(path)
        target = next(row for row in rows if row["case_id"] == case_id)
        target["key_cache_algorithm"] = algorithm
        target["value_cache_algorithm"] = algorithm
        target["key_cache_precision"] = precision
        target["value_cache_precision"] = precision
        _write_rows(path, rows)

    with pytest.raises(ValueError, match="cache activation semantics"):
        build_official_bundle(repo)


def test_official_validation_rejects_synchronized_prompt_identity_mapping():
    bundle = build_official_bundle(REPO_ROOT)
    first, second = "Q01", "Q02"
    remap = {first: second, second: first}
    quality = tuple(
        replace(
            item,
            prompt_id=remap.get(str(item.prompt_id), item.prompt_id),
            quality_id=(
                item.quality_id.replace(f"--{item.prompt_id}--", f"--{remap[item.prompt_id]}--")
                if item.prompt_id in remap
                else item.quality_id
            ),
        )
        for item in bundle.quality
    )
    prompts = _rows(ROUTE / "reproduction/quality/prompt-suite.csv")
    outputs = _rows(ROUTE / "reproduction/quality/outputs-index.csv")
    for row in prompts:
        row["prompt_id"] = remap.get(row["prompt_id"], row["prompt_id"])
    for row in outputs:
        old = row["prompt_id"]
        row["prompt_id"] = remap.get(old, old)
        if old in remap:
            row["output_id"] = row["output_id"].replace(
                f"--{old}--", f"--{remap[old]}--"
            )

    _, data = build_official_validation_receipts(
        REPO_ROOT,
        replace(bundle, quality=quality),
        prompt_rows=prompts,
        output_rows=outputs,
    )

    assert data["valid"] is False
    assert data["checks"]["prompt_entities"]["passed"] is False
    assert data["checks"]["output_entities"]["passed"] is False
    assert data["checks"]["quality_entities"]["passed"] is False


def test_official_validation_rejects_full_entity_and_typed_cross_case_mutations():
    bundle = build_official_bundle(REPO_ROOT)
    attempt = list(bundle.attempts)
    target = next(i for i, item in enumerate(attempt) if item.status is Status.BLOCKED)
    attempt[target] = replace(attempt[target], reason="fabricated reason")
    coverage, data = build_official_validation_receipts(
        REPO_ROOT, replace(bundle, attempts=tuple(attempt))
    )
    assert coverage["valid"] is False or data["valid"] is False
    assert data["checks"]["attempt_entities"]["passed"] is False

    measurements = list(bundle.measurements)
    measurements[0] = replace(
        measurements[0], attempt_id=next(
            item.attempt_id
            for item in bundle.attempts
            if item.status is Status.PASSED
            and item.test_case_id != measurements[0].test_case_id
        )
    )
    _, data = build_official_validation_receipts(
        REPO_ROOT, replace(bundle, measurements=tuple(measurements))
    )
    assert data["valid"] is False
    assert data["checks"]["measurement_entities"]["passed"] is False

    summaries = list(bundle.summaries)
    summaries[0] = replace(summaries[0], aggregation="mean")
    _, data = build_official_validation_receipts(
        REPO_ROOT, replace(bundle, summaries=tuple(summaries))
    )
    assert data["valid"] is False
    assert data["checks"]["summary_entities"]["passed"] is False

    evidence = list(bundle.evidence)
    raw_index = next(i for i, item in enumerate(evidence) if item.role == "raw-case-result")
    evidence[raw_index] = replace(evidence[raw_index], role="source-results")
    _, data = build_official_validation_receipts(
        REPO_ROOT, replace(bundle, evidence=tuple(evidence))
    )
    assert data["valid"] is False
    assert data["checks"]["evidence_entities"]["passed"] is False


def test_official_validation_rejects_each_published_entity_type_mutation():
    bundle = write_official_route(REPO_ROOT)

    quality = list(bundle.quality)
    quality[0] = replace(quality[0], score=float(quality[0].score) + 0.5)
    _, data = build_official_validation_receipts(
        REPO_ROOT, replace(bundle, quality=tuple(quality))
    )
    assert data["checks"]["quality_entities"]["passed"] is False

    failures = list(bundle.failures)
    failures[0] = replace(failures[0], stage="fabricated-stage")
    _, data = build_official_validation_receipts(
        REPO_ROOT, replace(bundle, failures=tuple(failures))
    )
    assert data["checks"]["failure_entities"]["passed"] is False

    summaries = list(bundle.summaries)
    summaries[0] = replace(summaries[0], value=float(summaries[0].value) + 1.0)
    _, data = build_official_validation_receipts(
        REPO_ROOT, replace(bundle, summaries=tuple(summaries))
    )
    assert data["checks"]["summary_entities"]["passed"] is False
    summaries[0] = replace(
        bundle.summaries[0],
        source_measurement_ids=tuple(reversed(bundle.summaries[0].source_measurement_ids)),
    )
    _, data = build_official_validation_receipts(
        REPO_ROOT, replace(bundle, summaries=tuple(summaries))
    )
    assert data["checks"]["summary_entities"]["passed"] is False

    prompt_rows = _rows(ROUTE / "reproduction/quality/prompt-suite.csv")
    prompt_rows[0]["domain"] = "fabricated-domain"
    _, data = build_official_validation_receipts(
        REPO_ROOT, bundle, prompt_rows=prompt_rows
    )
    assert data["checks"]["prompt_entities"]["passed"] is False

    output_rows = _rows(ROUTE / "reproduction/quality/outputs-index.csv")
    output_rows[0]["output_sha256"] = "0" * 64
    _, data = build_official_validation_receipts(
        REPO_ROOT, bundle, output_rows=output_rows
    )
    assert data["checks"]["output_entities"]["passed"] is False

    availability_rows = _rows(ROUTE / "data/availability-matrix.csv")
    availability_rows[0]["reason"] = "fabricated reason"
    _, data = build_official_validation_receipts(
        REPO_ROOT, bundle, availability_rows=availability_rows
    )
    assert data["checks"]["availability_entities"]["passed"] is False

    artifact_rows = _rows(ROUTE / "reproduction/system/model-artifacts.csv")
    artifact_rows[0]["executed_case_count"] = "999"
    _, data = build_official_validation_receipts(
        REPO_ROOT, bundle, model_artifact_rows=artifact_rows
    )
    assert data["checks"]["model_artifact_entities"]["passed"] is False

    source_locations = _rows(ROUTE / "evidence/source-locations.csv")
    alias = next(
        row for row in source_locations
        if row["relative_path"].endswith("guarded-retry-001/source-models.json")
    )
    alias["sha256"] = "0" * 64
    _, data = build_official_validation_receipts(
        REPO_ROOT, bundle, source_location_rows=source_locations
    )
    assert data["checks"]["source_location_entities"]["passed"] is False

    evidence = list(bundle.evidence)
    preflight_index = next(
        i for i, item in enumerate(evidence) if item.role == "source-preflight"
    )
    evidence[preflight_index] = replace(
        evidence[preflight_index], input_evidence_ids=()
    )
    _, data = build_official_validation_receipts(
        REPO_ROOT, replace(bundle, evidence=tuple(evidence))
    )
    assert data["checks"]["evidence_entities"]["passed"] is False

    failures = list(bundle.failures)
    conversion_index = next(
        i for i, item in enumerate(failures)
        if item.test_case_id.startswith("granite-3b__fp16__")
    )
    conversion_evidence_id = next(
        item.evidence_id for item in bundle.evidence if item.role == "conversion-log"
    )
    failures[conversion_index] = replace(
        failures[conversion_index],
        evidence_ids=tuple(
            item for item in failures[conversion_index].evidence_ids
            if item != conversion_evidence_id
        ),
    )
    coverage, data = build_official_validation_receipts(
        REPO_ROOT, replace(bundle, failures=tuple(failures))
    )
    assert coverage["checks"]["conversion_log_coverage"]["passed"] is False
    assert data["checks"]["failure_entities"]["passed"] is False


@pytest.mark.parametrize(
    ("field", "replacement"),
    (
        ("evidence_id", "openvino-official-upstream-36d151562886"),
        ("role", "conversion-log"),
        ("sha256", "0" * 64),
        ("size_bytes", "7933"),
        (
            "relative_path",
            (
                FV1
                / "inputs/f2b9e8f0f10053f3a04e1532ecd1e66d026ba1f37e7cde636bc2b5e2748c301c.txt"
            ).as_posix(),
        ),
    ),
)
def test_official_validation_rejects_source_location_evidence_mismatch(
    field, replacement
):
    bundle = build_official_bundle(REPO_ROOT)
    rows = _rows(ROUTE / "evidence/source-locations.csv")
    alias = next(
        row for row in rows
        if row["relative_path"].endswith("guarded-retry-001/source-models.json")
    )
    alias[field] = replacement

    _, data = build_official_validation_receipts(
        REPO_ROOT, bundle, source_location_rows=rows
    )

    assert data["checks"]["source_location_evidence_consistency"]["passed"] is False


def test_official_rejects_a_passed_fv2_case_without_fv1_raw_evidence(tmp_path):
    repo = _isolated_official_repo(tmp_path)
    detailed_path = repo / FV1 / "official-openvino-detailed-results.csv"
    rows = _rows(detailed_path)
    target = next(row for row in rows if row["case_id"] == "granite-3b__int4__tbq3")
    target["raw_result_path"] = ""
    target["raw_result_sha256"] = ""
    _write_rows(detailed_path, rows)

    with pytest.raises(ValueError, match="passed fv2.*raw evidence"):
        build_official_bundle(repo)


@pytest.mark.parametrize("field", ("decode_tps", "quality_score", "raw_result_path"))
def test_official_rejects_nonpassed_fv2_published_observations(tmp_path, field):
    repo = _isolated_official_repo(tmp_path)
    path = repo / FV2 / "consolidated/official-openvino-detailed-results.csv"
    rows = _rows(path)
    target = next(row for row in rows if row["status"] == "hardware_preflight_blocked")
    target[field] = "1" if field != "raw_result_path" else "fabricated.json"
    _write_rows(path, rows)
    if field in {"decode_tps", "quality_score"}:
        comparison_path = repo / FV2 / "consolidated/official-openvino-comparison.csv"
        comparison = _rows(comparison_path)
        compared = next(
            row for row in comparison
            if (row["model"], row["weight_precision"], row["cache_codec"])
            == (target["model"], target["weight_precision"], target["cache_codec"])
        )
        compared[field] = target[field]
        _write_rows(comparison_path, comparison)

    with pytest.raises(ValueError, match="non-passed.*published"):
        build_official_bundle(repo)


def test_official_rejects_a_changed_final_status_or_failure_reason(tmp_path):
    repo = _isolated_official_repo(tmp_path)
    path = repo / FV2 / "consolidated/official-openvino-detailed-results.csv"
    rows = _rows(path)
    target = next(row for row in rows if row["status"] == "hardware_preflight_blocked")
    target["failure_reason"] = "generic block"
    _write_rows(path, rows)
    comparison_path = repo / FV2 / "consolidated/official-openvino-comparison.csv"
    comparison = _rows(comparison_path)
    next(
        row for row in comparison
        if (row["model"], row["weight_precision"], row["cache_codec"])
        == (target["model"], target["weight_precision"], target["cache_codec"])
    )["failure_reason"] = "generic block"
    _write_rows(comparison_path, comparison)
    rows_path = repo / FV2 / "consolidated/rows.json"
    payload = json.loads(rows_path.read_text(encoding="utf-8"))
    next(row for row in payload["rows"] if row["case_id"] == target["case_id"])[
        "failure_reason"
    ] = "generic block"
    rows_path.write_text(json.dumps(payload), encoding="utf-8")

    with pytest.raises(ValueError, match="manifest.*failure reason|failure reason.*manifest"):
        build_official_bundle(repo)


def test_official_rejects_missing_or_conflicting_missing_model_manifest(tmp_path):
    repo = _isolated_official_repo(tmp_path)
    manifest = repo / FV2 / "attempts/granite-30b__int4/manifest.json"
    manifest.unlink()
    with pytest.raises((FileNotFoundError, ValueError), match="manifest"):
        build_official_bundle(repo)

    repo = _isolated_official_repo(tmp_path / "conflict")
    _rewrite_json(
        repo,
        FV2 / "attempts/granite-30b__int4/manifest.json",
        lambda payload: payload.__setitem__("failure_stage", "wrong_stage"),
    )
    with pytest.raises(ValueError, match="manifest.*failure stage|failure stage.*manifest"):
        build_official_bundle(repo)


def test_official_rejects_fv1_comparison_and_repetition_conflicts(tmp_path):
    repo = _isolated_official_repo(tmp_path)
    comparison_path = repo / FV1 / "official-openvino-comparison.csv"
    rows = _rows(comparison_path)
    target = next(
        row for row in rows
        if (row["model"], row["weight_precision"], row["cache_codec"])
        == ("granite-3b", "int4", "tbq3")
    )
    target["decode_tps"] = "99"
    _write_rows(comparison_path, rows)
    with pytest.raises(ValueError, match="fv1 comparison"):
        build_official_bundle(repo)

    repo = _isolated_official_repo(tmp_path / "stdout")

    def mutate_nonselected_repetition(payload):
        target = next(
            run for run in payload["benchmark_runs"] if run != payload["benchmark"]
        )
        target["result"]["input_tokens"] += 1

    _rewrite_raw(
        repo,
        "granite-3b__int4__tbq3",
        mutate_nonselected_repetition,
    )
    with pytest.raises(ValueError, match="stdout/result conflict"):
        build_official_bundle(repo)


def test_official_rejects_quality_criterion_prompt_and_output_conflicts(tmp_path):
    repo = _isolated_official_repo(tmp_path)
    _rewrite_raw(
        repo,
        "granite-3b__int4__tbq3",
        lambda payload: payload["quality_runs"][0]["quality"]["criteria"][0].__setitem__(
            "weight", 4.5
        ),
    )
    with pytest.raises(ValueError, match="quality criterion"):
        build_official_bundle(repo)

    repo = _isolated_official_repo(tmp_path / "prompt")
    _rewrite_raw(
        repo,
        "granite-3b__int4__tbq3",
        lambda payload: payload["quality_runs"][0].__setitem__("domain", "education"),
    )
    with pytest.raises(ValueError, match="quality prompt"):
        build_official_bundle(repo)

    repo = _isolated_official_repo(tmp_path / "output")
    _rewrite_raw(
        repo,
        "granite-3b__int4__tbq3",
        lambda payload: payload["quality_runs"][0]["result"].__setitem__(
            "text", payload["quality_runs"][0]["result"]["text"] + " altered"
        ),
    )
    with pytest.raises(ValueError, match="quality output"):
        build_official_bundle(repo)


def test_official_route_generation_preserves_an_identical_existing_manifest():
    write_official_route(REPO_ROOT)
    manifest = ROUTE / "evidence/manifest-sha256.txt"
    preserved_timestamp_ns = 1_700_000_000_000_000_000
    os.utime(manifest, ns=(preserved_timestamp_ns, preserved_timestamp_ns))

    write_official_route(REPO_ROOT)

    assert manifest.stat().st_mtime_ns == preserved_timestamp_ns
