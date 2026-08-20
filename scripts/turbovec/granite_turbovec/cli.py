"""Privacy-safe, offline command-line orchestration for TurboVec research."""

from __future__ import annotations

import argparse
import contextlib
import hashlib
import importlib.metadata
import json
import io
import math
import os
import platform
import re
import sys
import time
import traceback
import uuid
from dataclasses import dataclass
from pathlib import Path, PurePosixPath, PureWindowsPath
from typing import Any, Callable, Mapping, Sequence

from .benchmark import (
    build_matched_suite,
    canonical_json as benchmark_json,
    load_evaluation_fixture,
    render_markdown,
    summarize_cold_warm,
)
from .contracts import Chunk, ResearchError
from .embedding import FastEmbedder, MODEL_IDENTITY
from .indexes import Float32Index, TurboVecIndex, validate_ids, validate_vectors
from .manifest import (
    ArtifactRecord,
    ChunkRecord,
    IndexIdentity,
    IndexManifest,
    SourceRecord,
    canonical_sha256 as manifest_sha256,
    load_and_validate_manifest,
    promote_staged_index,
    _unlink_owned_file,
    _require_plain_directory,
    _require_same_directory,
)
from .text_pipeline import chunk_document, discover_documents
from .tvim_container import extract_validated_payload, write_container


SCHEMA_VERSION = 1
MAX_QUERY_CHARS = 8_192
MAX_CHUNKS_BYTES = 64 * 1024 * 1024
MAX_MODEL_FILES = 4_096
MAX_MODEL_BYTES = 16 * 1024 * 1024 * 1024
REQUIREMENTS_LOCK = Path(__file__).resolve().parents[1] / "requirements.lock.txt"
try:
    LOCKED_VERSIONS = {
        name.casefold(): version
        for name, version in (
            line.split("==", 1)
            for line in REQUIREMENTS_LOCK.read_text(encoding="utf-8").splitlines()
            if line
        )
    }
except Exception:
    LOCKED_VERSIONS = {}
TURBOVEC_COMMIT = "ccab9f325e6ce2a270a87daf01ae4e443bcf2d49"
APPROVED_LOCK_SHA256 = "438b20b685916055f02e9a207d6bb1466ed30669cb0404bbfa1661e409bd7b35"
APPROVAL_KEYS = {
    "schema_version", "approval_status", "python_version", "turbovec_version",
    "turbovec_source_commit", "turbovec_wheel_sha256", "embedding_model",
    "embedding_model_manifest_sha256", "embedding_model_license", "model_cache_root",
}
_HASH = re.compile(r"[0-9a-f]{64}\Z")


# Every fixed code produced by the current research modules is deliberately
# categorized here. New/unknown codes fail closed via DEFAULT_EXPECTED_EXIT.
ERROR_EXIT = {
    # input / parsing
    "approved-input-invalid": 20, "input-invalid-parameters": 20,
    "input-not-found": 20, "input-read-failed": 20,
    "input-unsupported-type": 20, "input-not-file-or-directory": 20,
    "input-empty": 20, "input-invalid-utf8": 20, "input-unsafe-link": 20,
    "input-race-detected": 20, "query-invalid": 20, "query-empty": 20,
    "query-too-long": 20, "query-control-character": 20,
    "query-top-k-invalid": 20,
    "evaluation-fixture-invalid": 20,
    # extraction and hard limits
    "input-file-count-limit": 21, "input-file-size-limit": 21,
    "input-total-size-limit": 21, "chunk-limit-exceeded": 21,
    "chunks-artifact-limit": 21, "chunk-id-collision": 21,
    "chunk-invalid-parameters": 21,
    # no-overwrite destinations
    "index-destination-exists": 22, "evidence-destination-exists": 22,
    # approved environment/dependencies
    "approval-mismatch": 30, "environment-mismatch": 30,
    "dependency-mismatch": 30, "dependency-unavailable": 30,
    "embedding-cache-invalid": 30, "embedding-dependency-unavailable": 30,
    "embedding-model-load-failed": 30, "index-dependency-unavailable": 30,
    "index-promotion-unsupported": 30,
    # index / manifest / corruption
    "index-manifest-invalid": 31, "index-manifest-corrupt": 31,
    "index-package-mismatch": 31,
    "index-route-mismatch": 31,
    "index-manifest-write-failed": 31, "index-embedding-mismatch": 31,
    "index-dimension-mismatch": 31, "index-chunking-mismatch": 31,
    "index-backend-mismatch": 31, "index-format-mismatch": 31,
    "index-bit-width-mismatch": 31, "index-python-mismatch": 31,
    "index-turbovec-mismatch": 31, "index-dependency-mismatch": 31,
    "index-provider-mismatch": 31, "index-source-mismatch": 31,
    "index-artifact-invalid": 31, "index-artifact-mismatch": 31,
    "index-path-invalid": 31, "index-promotion-failed": 31,
    "index-quarantine-failed": 31, "index-maintenance-required": 31,
    "index-directory-fsync-unsupported": 31, "index-durability-failed": 31,
    "index-operation-id-invalid": 31, "index-staging-limit-exceeded": 31,
    "chunks-artifact-invalid": 31,
    "ids-shape-invalid": 31, "ids-count-invalid": 31,
    "ids-empty": 31, "ids-type-invalid": 31, "ids-duplicate": 31,
    "search-k-invalid": 20,
    # embedding runtime
    "embedding-failed": 32, "embedding-row-count-invalid": 32,
    "embedding-type-invalid": 32, "embedding-shape-invalid": 32,
    "embedding-dimension-invalid": 32, "embedding-nonfinite": 32,
    "embedding-input-invalid": 32,
    "vectors-shape-invalid": 32, "vectors-dimension-invalid": 32,
    "vectors-type-invalid": 32, "vectors-count-invalid": 32,
    "vectors-empty": 32, "vectors-nonfinite": 32, "vector-dtype-invalid": 32,
    "query-shape-invalid": 32, "query-dimension-invalid": 32,
    "query-nonfinite": 32, "query-type-invalid": 32,
    # TurboVec runtime
    "index-create-failed": 33, "index-add-failed": 33,
    "index-search-failed": 33, "index-write-failed": 33,
    "index-bits-invalid": 33, "index-dimension-invalid": 33,
    "index-load-failed": 33, "index-search-result-invalid": 33,
    # benchmark
    "benchmark-gate-failed": 40, "benchmark-evidence-invalid": 40,
    "timing-samples-invalid": 40, "timing-sample-count-insufficient": 40,
    "timing-warm-count-insufficient": 40, "gate-metrics-invalid": 40,
    "metric-ratio-invalid": 40, "metric-baseline-zero": 40,
    "rankings-k-invalid": 40, "rankings-type-invalid": 40,
    "rankings-empty": 40, "rankings-query-count-mismatch": 40,
    "rankings-relevance-count-mismatch": 40, "rankings-k-out-of-bounds": 40,
    "ranking-row-invalid": 40, "ranking-id-invalid": 40,
    "ranking-id-duplicate": 40, "relevance-set-invalid": 40,
    "relevance-id-invalid": 40, "storage-bytes-invalid": 40,
}
EXIT_CODES = ERROR_EXIT
FIXED_RESEARCH_ERROR_CODES = frozenset(ERROR_EXIT) | {"research-argument-error"}
DEFAULT_EXPECTED_EXIT = 30


@dataclass
class CliDependencies:
    python_version: Callable[[], str]
    package_versions: Callable[[], Mapping[str, str]]
    dependency_lock_sha256: Callable[[], str]
    wheel_sha256: Callable[[], str]
    model_manifest_sha256: Callable[[Path], str]
    platform_info: Callable[[], Mapping[str, str]]
    make_embedder: Callable[[Path], Any]
    make_turbovec: Callable[[int, int], Any]
    load_turbovec: Callable[[Path], Any]
    now_utc: Callable[[], str]
    perf_counter: Callable[[], float]
    peak_working_set: Callable[[], int]


@dataclass(frozen=True)
class CliResult:
    exit_code: int
    payload: Mapping[str, Any]
    success: bool


def default_dependencies() -> CliDependencies:
    return CliDependencies(
        python_version=lambda: platform.python_version(),
        package_versions=_installed_locked_versions,
        dependency_lock_sha256=lambda: _sha256_file(REQUIREMENTS_LOCK),
        wheel_sha256=_configured_wheel_sha256,
        model_manifest_sha256=_model_manifest_sha256,
        platform_info=lambda: {
            "platform": platform.system().casefold(),
            "architecture": platform.machine(),
            "processor": platform.processor() or "unknown",
        },
        make_embedder=lambda cache: FastEmbedder(cache),
        make_turbovec=lambda dimension, bits: TurboVecIndex(dimension, bits=bits),
        load_turbovec=lambda path: TurboVecIndex.load(path),
        now_utc=lambda: time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        perf_counter=time.perf_counter,
        peak_working_set=_peak_working_set_bytes,
    )


class _JsonArgumentParser(argparse.ArgumentParser):
    def error(self, message: str) -> None:
        del message
        raise ResearchError("research-argument-error")


def build_parser() -> argparse.ArgumentParser:
    parser = _JsonArgumentParser(prog="granite-turbovec", add_help=False)
    commands = parser.add_subparsers(dest="command", required=True, parser_class=_JsonArgumentParser)
    doctor = commands.add_parser("doctor")
    doctor.add_argument("--approved-input", required=True)
    index = commands.add_parser("index")
    index.add_argument("--approved-input", required=True)
    index.add_argument("--input", required=True)
    index.add_argument("--output", required=True)
    index.add_argument("--bits", nargs="+", choices=("2", "4"), default=["2", "4"])
    query = commands.add_parser("query")
    query.add_argument("--approved-input", required=True)
    query.add_argument("--index", required=True)
    query.add_argument("--text", required=True)
    query.add_argument("--top-k", type=int, default=5)
    query.add_argument("--route", choices=("float32", "2bit", "4bit"), default="4bit")
    benchmark = commands.add_parser("benchmark")
    benchmark.add_argument("--approved-input", required=True)
    benchmark.add_argument("--fixture", required=True)
    benchmark.add_argument("--index", required=True)
    benchmark.add_argument("--output", required=True)
    return parser


def run_cli(args: Sequence[str], *, dependencies: CliDependencies | None = None) -> CliResult:
    dependencies = dependencies or default_dependencies()
    try:
        namespace = build_parser().parse_args(list(args))
        # Dependencies sometimes use progress/logging libraries. Their output is
        # intentionally contained so stdout/stderr remain protocol streams.
        with _suppress_dependency_output():
            if namespace.command == "doctor":
                payload = _doctor(namespace, dependencies)
            elif namespace.command == "index":
                payload = _index(namespace, dependencies)
            elif namespace.command == "query":
                payload = _query(namespace, dependencies)
            else:
                payload, passed = _benchmark(namespace, dependencies)
                if not passed:
                    return CliResult(40, _diagnostic("benchmark-gate-failed", Path(namespace.output).name), False)
        _json_bytes(payload)
        return CliResult(0, payload, True)
    except ResearchError as error:
        code = 2 if error.code == "research-argument-error" else ERROR_EXIT.get(error.code, DEFAULT_EXPECTED_EXIT)
        return CliResult(code, _diagnostic(error.code), False)
    except KeyboardInterrupt:
        return CliResult(130, _diagnostic("research-cancelled"), False)


def main(args: Sequence[str] | None = None, *, dependencies: CliDependencies | None = None) -> int:
    try:
        result = run_cli(sys.argv[1:] if args is None else args, dependencies=dependencies)
        stream = sys.stdout if result.success else sys.stderr
        stream.write(_json_bytes(result.payload).decode("utf-8") + "\n")
        return result.exit_code
    except KeyboardInterrupt:
        sys.stderr.write(_json_bytes(_diagnostic("research-cancelled")).decode() + "\n")
        return 130
    except BaseException:
        if os.environ.get("GRANITE_TURBOVEC_DEBUG") == "1":
            traceback.print_exc(file=sys.stderr)
        sys.stderr.write(_json_bytes(_diagnostic("research-unexpected-failure")).decode() + "\n")
        return 70


def _doctor(namespace: argparse.Namespace, deps: CliDependencies) -> Mapping[str, Any]:
    approval, actual = _validate_approved_input(Path(namespace.approved_input), deps)
    info = deps.platform_info()
    embedder = _validated_embedder(approval, deps)
    providers = list(getattr(embedder, "providers", ()))
    return {
        "schema_version": 1,
        "python": {"version": actual["python_version"]},
        "packages": dict(sorted(actual["packages"].items())),
        "platform": {"name": _safe_machine_label(info.get("platform")), "architecture": _safe_machine_label(info.get("architecture")), "processor": _safe_machine_label(info.get("processor"))},
        "providers": {"requested": ["CPUExecutionProvider"], "actual": providers},
        "embedding_model": {"requested_identity": approval["embedding_model"], "actual_identity": str(getattr(embedder, "model_identity", "unknown")), "license": approval["embedding_model_license"], "manifest_sha256": actual["model_manifest_sha256"]},
        "turbovec": {"version": approval["turbovec_version"], "source_commit": approval["turbovec_source_commit"], "wheel_sha256": actual["wheel_sha256"], "license": "MIT"},
        "dependency_lock_sha256": actual["dependency_lock_sha256"],
    }


def _index(namespace: argparse.Namespace, deps: CliDependencies) -> Mapping[str, Any]:
    index_start = deps.perf_counter()
    widths = [int(value) for value in namespace.bits]
    if len(widths) != len(set(widths)):
        raise ResearchError("research-argument-error")
    bits = tuple(sorted(widths))
    approval, actual = _validate_approved_input(Path(namespace.approved_input), deps)
    destination = Path(namespace.output)
    if destination.exists() or destination.is_symlink():
        raise ResearchError("index-destination-exists")
    documents = discover_documents(namespace.input)
    chunks = tuple(chunk for document in documents for chunk in chunk_document(document))
    if not chunks:
        raise ResearchError("input-empty")
    embedder = _validated_embedder(approval, deps)
    embedding_start = deps.perf_counter()
    vectors = validate_vectors(embedder.embed_documents([chunk.text for chunk in chunks]), dimension=embedder.dimension, expected_count=len(chunks))
    document_embedding_seconds = deps.perf_counter() - embedding_start
    import numpy as np
    ids = validate_ids(np.asarray([chunk.chunk_id for chunk in chunks], dtype=np.uint64), expected_count=len(chunks))
    float_build_start = deps.perf_counter()
    Float32Index(vectors, ids)
    float32_index_build_seconds = deps.perf_counter() - float_build_start
    identity = _identity(approval, actual, embedder, dimension=vectors.shape[1])
    sources = tuple(SourceRecord(item.relative_path, item.sha256) for item in documents)
    chunk_records = tuple(ChunkRecord(item.chunk_id, item.relative_path, item.start, item.end) for item in chunks)

    def writer(staging: Path) -> None:
        _write_chunks(staging / "chunks.jsonl", chunks)
        float_save_start = deps.perf_counter()
        with (staging / "vectors-float32.npy").open("xb") as stream:
            np.save(stream, vectors, allow_pickle=False)
        with (staging / "ids.npy").open("xb") as stream:
            np.save(stream, ids, allow_pickle=False)
        build_timings = {
            "document_embedding_seconds": document_embedding_seconds,
            "float32_index_build_seconds": float32_index_build_seconds,
            "float32_index_save_seconds": deps.perf_counter() - float_save_start,
        }
        for width in bits:
            build_start = deps.perf_counter()
            index = deps.make_turbovec(vectors.shape[1], width)
            index.add_with_ids(vectors, ids)
            build_timings[f"{width}bit_index_build_seconds"] = deps.perf_counter() - build_start
            raw = staging / f"raw-{width}bit-{uuid.uuid4().hex}.tmp"
            raw_identity = None
            save_start = deps.perf_counter()
            try:
                index.write(raw)
                raw_metadata = raw.lstat()
                if _is_link_or_reparse(raw) or not raw.is_file():
                    raise ResearchError("index-artifact-invalid")
                raw_identity = (raw_metadata.st_dev, raw_metadata.st_ino)
                write_container(raw, staging / f"index-{width}bit.tvim", bits=width, dimension=vectors.shape[1], count=len(chunks))
            finally:
                if raw.exists() and raw.is_file():
                    if raw_identity is None:
                        raw_metadata = raw.lstat(); raw_identity = (raw_metadata.st_dev, raw_metadata.st_ino)
                    _unlink_owned_file(raw, raw_identity)
            build_timings[f"{width}bit_index_save_seconds"] = deps.perf_counter() - save_start
        build_timings["index_end_to_end_seconds"] = deps.perf_counter() - index_start
        _validate_timing_mapping(build_timings)
        (staging / "baseline-results.json").write_bytes(_json_bytes({
            "schema_version": 1, "route": "float32", "count": len(chunks),
            "dimension": vectors.shape[1], "available_routes": ["float32", *(f"{item}bit" for item in bits)],
            "turbovec": {"version": approval["turbovec_version"], "source_commit": approval["turbovec_source_commit"], "wheel_sha256": approval["turbovec_wheel_sha256"], "license": "MIT"},
            "vector_count": len(chunks),
            "actual_providers": list(embedder.providers),
            "build_timings_seconds": build_timings,
        }))

    def factory(staging: Path) -> IndexManifest:
        records = tuple(_record(path, len(chunks), vectors.shape[1]) for path in sorted(staging.iterdir(), key=lambda item: item.name))
        return IndexManifest(identity, sources, chunk_records, deps.now_utc(), records)

    promote_staged_index(destination, None, writer, lambda path, record, manifest: _validate_artifact(path, record, manifest, deps), operation_id=uuid.uuid4().hex, manifest_factory=factory)
    return {"schema_version": 1, "index": destination.name, "chunk_count": len(chunks), "routes": ["float32", *(f"{item}bit" for item in bits)]}


def _query(namespace: argparse.Namespace, deps: CliDependencies) -> Mapping[str, Any]:
    text = _validate_query_text(namespace.text)
    if type(namespace.top_k) is not int or namespace.top_k <= 0:
        raise ResearchError("query-top-k-invalid")
    approval, actual = _validate_approved_input(Path(namespace.approved_input), deps)
    root = Path(namespace.index)
    expected = _identity(approval, actual, None, dimension=384)
    # Identity is checked before any artifact, index, or embedder is loaded.
    manifest = load_and_validate_manifest(root / "manifest.json", expected)
    routes = {item.route for item in manifest.artifacts}
    if namespace.route not in routes:
        raise ResearchError("index-route-mismatch")
    if namespace.top_k > len(manifest.chunks):
        raise ResearchError("query-top-k-invalid")
    embedder = _validated_embedder(approval, deps)
    _verify_all_artifacts(root, manifest, deps)
    chunks = _load_chunks(root / "chunks.jsonl", manifest)
    import numpy as np
    ids = np.load(root / "ids.npy", allow_pickle=False)
    start = deps.perf_counter()
    embed_start = deps.perf_counter()
    query_vector = validate_vectors(embedder.embed_queries([text]), dimension=manifest.identity.dimension, expected_count=1)
    embedding_seconds = deps.perf_counter() - embed_start
    load_start = deps.perf_counter()
    with contextlib.ExitStack() as stack:
        if namespace.route == "float32":
            vectors = np.load(root / "vectors-float32.npy", allow_pickle=False)
            index = Float32Index(vectors, ids)
        else:
            record = next(item for item in manifest.artifacts if item.route == namespace.route)
            raw_path = stack.enter_context(extract_validated_payload(root / record.filename, bits=record.bit_width, dimension=record.dimension, count=record.count))
            index = deps.load_turbovec(raw_path)
        load_seconds = deps.perf_counter() - load_start
        search_start = deps.perf_counter()
        scores, result_ids = index.search(query_vector, namespace.top_k)
    search_seconds = deps.perf_counter() - search_start
    by_id = {chunk.chunk_id: chunk for chunk in chunks}
    results = []
    for rank, (score, chunk_id) in enumerate(zip(scores[0], result_ids[0]), 1):
        chunk = by_id.get(int(chunk_id))
        if chunk is None or not math.isfinite(float(score)):
            raise ResearchError("index-search-result-invalid")
        results.append({"rank": rank, "chunk_id": chunk.chunk_id, "score": float(score), "source": chunk.relative_path, "start": chunk.start, "end": chunk.end, "excerpt": _excerpt(chunk.text)})
    end_to_end_seconds = deps.perf_counter() - start
    if any(not math.isfinite(value) or value < 0 for value in (embedding_seconds, load_seconds, search_seconds, end_to_end_seconds)):
        raise ResearchError("index-search-result-invalid")
    return {"schema_version": 1, "route": namespace.route, "provider": list(getattr(embedder, "providers", ())), "model": approval["embedding_model"], "results": results, "timings": {"embedding_seconds": embedding_seconds, "index_load_seconds": load_seconds, "search_seconds": search_seconds, "end_to_end_seconds": end_to_end_seconds}}


def _benchmark(namespace: argparse.Namespace, deps: CliDependencies) -> tuple[Mapping[str, Any], bool]:
    end_to_end_start = deps.perf_counter()
    approval, actual = _validate_approved_input(Path(namespace.approved_input), deps)
    fixture_path = Path(namespace.fixture)
    fixture = load_evaluation_fixture(fixture_path)
    output = Path(namespace.output)
    if output.exists() or output.is_symlink():
        raise ResearchError("evidence-destination-exists")
    root = Path(namespace.index)
    expected = _identity(approval, actual, None, dimension=384)
    manifest = load_and_validate_manifest(root / "manifest.json", expected)
    route_records = {item.route: item for item in manifest.artifacts if item.route != "metadata"}
    if set(route_records) != {"float32", "2bit", "4bit"} or len(manifest.chunks) < fixture.top_k:
        raise ResearchError("index-route-mismatch")
    embedder = _validated_embedder(approval, deps)
    _verify_all_artifacts(root, manifest, deps)
    chunks = _load_chunks(root / "chunks.jsonl", manifest)
    query_embedding_start = deps.perf_counter()
    query_vectors = validate_vectors(embedder.embed_queries([item.text for item in fixture.queries]), dimension=embedder.dimension, expected_count=len(fixture.queries))
    query_embedding_seconds = deps.perf_counter() - query_embedding_start
    import numpy as np
    ids = validate_ids(np.load(root / "ids.npy", allow_pickle=False), expected_count=len(chunks))
    metadata = json.loads((root / "baseline-results.json").read_text(encoding="utf-8"))
    phase_timings = dict(metadata["build_timings_seconds"])
    phase_timings["query_embedding_seconds"] = query_embedding_seconds
    with contextlib.ExitStack() as stack:
        routes = {}
        load_start = deps.perf_counter()
        vectors = validate_vectors(np.load(root / "vectors-float32.npy", allow_pickle=False), dimension=384, expected_count=len(chunks))
        routes["float32"] = Float32Index(vectors, ids)
        phase_timings["float32_index_load_seconds"] = deps.perf_counter() - load_start
        for bits in (2, 4):
            record = route_records[f"{bits}bit"]
            load_start = deps.perf_counter()
            raw = stack.enter_context(extract_validated_payload(root / record.filename, bits=bits, dimension=384, count=len(chunks)))
            routes[f"{bits}bit"] = deps.load_turbovec(raw)
            phase_timings[f"{bits}bit_index_load_seconds"] = deps.perf_counter() - load_start
        rankings: dict[str, list[list[int]]] = {}
        timings = {}
        for name, index in routes.items():
            cold_start = deps.perf_counter(); _, cold_ids = index.search(query_vectors, fixture.top_k); cold = deps.perf_counter() - cold_start
            samples = []
            latest = cold_ids
            for _ in range(5):
                warm_start = deps.perf_counter(); _, latest = index.search(query_vectors, fixture.top_k); samples.append(deps.perf_counter() - warm_start)
            rankings[name] = [[int(item) for item in row] for row in latest]
            timings[name] = summarize_cold_warm([cold], samples)
        relevant = []
        source_by_id = {item.chunk_id: item.relative_path for item in chunks}
        for query in fixture.queries:
            relevant.append({chunk_id for chunk_id, source in source_by_id.items() if source in query.relevant_sources})
        evidence = build_matched_suite(
            rankings["float32"], rankings["2bit"], rankings["4bit"], relevant,
            baseline_timings=timings["float32"], two_bit_timings=timings["2bit"], four_bit_timings=timings["4bit"],
            float32_vector_bytes=_raw_float32_bytes(vectors),
            two_bit_persisted_bytes=(root / route_records["2bit"].filename).stat().st_size,
            four_bit_persisted_bytes=(root / route_records["4bit"].filename).stat().st_size,
            baseline_requested_provider="CPUExecutionProvider", baseline_actual_provider=_provider(embedder),
            two_bit_requested_provider="CPUExecutionProvider", two_bit_actual_provider=_provider(embedder),
            four_bit_requested_provider="CPUExecutionProvider", four_bit_actual_provider=_provider(embedder),
        )
        phase_timings["end_to_end_seconds"] = deps.perf_counter() - end_to_end_start
        storage_files = {
            "float32_raw_vector_bytes": _raw_float32_bytes(vectors),
            "float32_persisted_npy_bytes": route_records["float32"].size,
            "2bit_persisted_bytes": route_records["2bit"].size,
            "4bit_persisted_bytes": route_records["4bit"].size,
        }
        evidence_document = _build_evidence_document(evidence, phase_timings, storage_files, approval, actual, deps, manifest, fixture_path)
        _write_evidence_atomic(output, evidence, evidence_document)
        payload = {"schema_version": 1, "evidence": output.name, "gate_passed": evidence.gate.passed}
        return payload, evidence.gate.passed


def _validate_approved_input(path: Path, deps: CliDependencies) -> tuple[dict[str, Any], dict[str, Any]]:
    try:
        if any(_is_link_or_reparse(item) for item in (path, *path.parents)):
            raise ResearchError("approved-input-invalid")
        raw = path.read_bytes()
        if len(raw) > 64 * 1024:
            raise ValueError
        value = json.loads(raw.decode("utf-8"), object_pairs_hook=_unique_pairs, parse_constant=lambda _: (_ for _ in ()).throw(ValueError()))
    except (OSError, UnicodeError, ValueError, json.JSONDecodeError, ResearchError):
        raise ResearchError("approved-input-invalid") from None
    if type(value) is not dict or set(value) != APPROVAL_KEYS or value.get("schema_version") != 1:
        raise ResearchError("approved-input-invalid")
    strings = APPROVAL_KEYS - {"schema_version"}
    if any(type(value.get(key)) is not str or not value[key] for key in strings):
        raise ResearchError("approved-input-invalid")
    if value["approval_status"] != "approved" or value["turbovec_wheel_sha256"] in {"0" * 64, "f" * 64} or value["embedding_model_manifest_sha256"] in {"0" * 64, "f" * 64}:
        raise ResearchError("approval-mismatch")
    if not _HASH.fullmatch(value["turbovec_wheel_sha256"]) or not _HASH.fullmatch(value["embedding_model_manifest_sha256"]):
        raise ResearchError("approved-input-invalid")
    if value["turbovec_version"] != "1.0.0" or value["turbovec_source_commit"] != TURBOVEC_COMMIT or value["embedding_model"] != MODEL_IDENTITY or value["embedding_model_license"] != "MIT":
        raise ResearchError("approval-mismatch")
    cache = Path(value["model_cache_root"])
    try:
        if not cache.is_absolute() or not cache.is_dir() or any(_is_link_or_reparse(parent) for parent in (cache, *cache.parents)):
            raise ResearchError("approval-mismatch")
    except OSError:
        raise ResearchError("approval-mismatch") from None
    info = deps.platform_info()
    if str(info.get("platform", "")).casefold() != "windows" or str(info.get("architecture", "")).casefold() not in {"amd64", "x86_64"}:
        raise ResearchError("environment-mismatch")
    for flag in ("HF_HUB_OFFLINE", "TRANSFORMERS_OFFLINE", "HF_HUB_DISABLE_TELEMETRY"):
        if os.environ.get(flag) != "1":
            raise ResearchError("environment-mismatch")
    actual = {
        "python_version": deps.python_version(), "packages": dict(deps.package_versions()),
        "dependency_lock_sha256": deps.dependency_lock_sha256(), "wheel_sha256": deps.wheel_sha256(),
        "model_manifest_sha256": deps.model_manifest_sha256(cache),
    }
    if actual["python_version"] != value["python_version"] or actual["wheel_sha256"] != value["turbovec_wheel_sha256"] or actual["model_manifest_sha256"] != value["embedding_model_manifest_sha256"]:
        raise ResearchError("approval-mismatch")
    if actual["dependency_lock_sha256"] != APPROVED_LOCK_SHA256:
        raise ResearchError("dependency-mismatch")
    normalized_packages = {str(name).casefold(): str(version) for name, version in actual["packages"].items()}
    if normalized_packages != LOCKED_VERSIONS:
        raise ResearchError("dependency-mismatch")
    return value, actual


def _identity(approval: Mapping[str, Any], actual: Mapping[str, Any], embedder: Any | None, *, dimension: int) -> IndexIdentity:
    del embedder
    return IndexIdentity(1, "bounded-character-v1", 1, 1200, 200, approval["embedding_model"], approval["embedding_model_manifest_sha256"], approval["embedding_model_license"], dimension, "matched-suite", "matched-suite", "matched-suite-v1", None, approval["turbovec_version"], approval["turbovec_source_commit"], approval["turbovec_wheel_sha256"], "MIT", actual["dependency_lock_sha256"], "CPUExecutionProvider", "CPUExecutionProvider", approval["python_version"])


def _write_chunks(path: Path, chunks: Sequence[Chunk]) -> None:
    total = 0
    with path.open("xb") as stream:
        for item in chunks:
            line = _json_bytes({"chunk_id": item.chunk_id, "relative_path": item.relative_path, "start": item.start, "end": item.end, "text": item.text}) + b"\n"
            total += len(line)
            if total > MAX_CHUNKS_BYTES:
                raise ResearchError("chunks-artifact-limit")
            stream.write(line)


def _load_chunks(path: Path, manifest: IndexManifest) -> tuple[Chunk, ...]:
    try:
        if path.stat().st_size > MAX_CHUNKS_BYTES:
            raise ResearchError("chunks-artifact-limit")
        with path.open("rb") as stream:
            raw = stream.read(MAX_CHUNKS_BYTES + 1)
        if len(raw) > MAX_CHUNKS_BYTES:
            raise ResearchError("chunks-artifact-limit")
        lines = raw.decode("utf-8", errors="strict").splitlines()
        chunks = []
        for line in lines:
            value = json.loads(line, object_pairs_hook=lambda pairs: _unique_mapping(pairs, "chunks-artifact-invalid"), parse_constant=lambda _: (_ for _ in ()).throw(ValueError()))
            if type(value) is not dict or set(value) != {"chunk_id", "relative_path", "start", "end", "text"}:
                raise ValueError
            chunk = Chunk(value["chunk_id"], value["relative_path"], value["start"], value["end"], value["text"])
            if type(chunk.text) is not str or len(chunk.text) > 1200 or not _safe_relative(chunk.relative_path):
                raise ValueError
            chunks.append(chunk)
    except ResearchError:
        raise
    except Exception:
        raise ResearchError("chunks-artifact-invalid") from None
    expected = tuple((item.chunk_id, item.relative_path, item.start, item.end) for item in manifest.chunks)
    actual = tuple((item.chunk_id, item.relative_path, item.start, item.end) for item in chunks)
    if expected != actual or len({item.chunk_id for item in chunks}) != len(chunks):
        raise ResearchError("chunks-artifact-invalid")
    return tuple(chunks)


def _record(path: Path, count: int, dimension: int) -> ArtifactRecord:
    name = path.name
    if name == "vectors-float32.npy":
        magic, route, backend, index_format, bit_width = "NPY", "float32", "float32", "float32-npy-v1", None
    elif name.endswith(".tvim"):
        bit_width = 2 if "-2bit" in name else 4
        magic, route, backend, index_format = "GTVI", f"{bit_width}bit", "turbovec", "gtvi-turbovec-v1"
    elif name == "ids.npy":
        magic, route, backend, index_format, bit_width = "NPY", "metadata", "metadata", "npy-ids-v1", None
    elif name == "chunks.jsonl":
        magic, route, backend, index_format, bit_width = "JSONL", "metadata", "metadata", "canonical-jsonl-v1", None
    else:
        magic, route, backend, index_format, bit_width = "JSON", "metadata", "metadata", "canonical-json-v1", None
    return ArtifactRecord(name, _sha256_file(path), path.stat().st_size, count, magic, 1, dimension, route, backend, index_format, bit_width)


def _validate_artifact(path: Path, record: ArtifactRecord, manifest: IndexManifest, deps: CliDependencies) -> bool:
    import numpy as np
    expected_magic = {
        "chunks.jsonl": "JSONL", "vectors-float32.npy": "NPY", "ids.npy": "NPY",
        "baseline-results.json": "JSON", "index-2bit.tvim": "GTVI", "index-4bit.tvim": "GTVI",
    }.get(record.filename)
    if expected_magic is None or record.magic != expected_magic or record.version != 1:
        raise ResearchError("index-artifact-invalid")
    if record.filename == "chunks.jsonl":
        _load_chunks(path, manifest)
    elif record.filename == "vectors-float32.npy":
        if _npy_version(path) != (1, 0):
            raise ResearchError("index-artifact-invalid")
        value = np.load(path, allow_pickle=False)
        validate_vectors(value, dimension=record.dimension, expected_count=record.count)
    elif record.filename == "ids.npy":
        if _npy_version(path) != (1, 0):
            raise ResearchError("index-artifact-invalid")
        value = np.load(path, allow_pickle=False)
        stable = validate_ids(value, expected_count=record.count)
        if tuple(int(x) for x in stable) != tuple(item.chunk_id for item in manifest.chunks):
            raise ResearchError("ids-count-invalid")
    elif record.filename.endswith(".tvim"):
        expected_bits = 2 if "-2bit" in record.filename else 4
        with extract_validated_payload(path, bits=expected_bits, dimension=record.dimension, count=record.count) as raw:
            index = deps.load_turbovec(raw)
            if index.dimension != record.dimension or index.bits != expected_bits or getattr(index, "_count", len(getattr(index, "ids", ()))) != record.count:
                raise ResearchError("index-artifact-invalid")
    elif record.filename == "baseline-results.json":
        value = json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=lambda pairs: _unique_mapping(pairs, "index-artifact-invalid"), parse_constant=lambda _: (_ for _ in ()).throw(ValueError()))
        available_routes = ["float32", *(route for route in ("2bit", "4bit") if any(item.route == route for item in manifest.artifacts))]
        required_timings = {"document_embedding_seconds", "float32_index_build_seconds", "float32_index_save_seconds", "index_end_to_end_seconds", *(f"{route}_index_{phase}_seconds" for route in available_routes[1:] for phase in ("build", "save"))}
        if (
            type(value) is not dict
            or set(value) != {"schema_version", "route", "count", "dimension", "available_routes", "turbovec", "vector_count", "actual_providers", "build_timings_seconds"}
            or value.get("schema_version") != 1
            or value.get("route") != "float32"
            or value.get("count") != record.count
            or value.get("dimension") != record.dimension
            or value.get("available_routes") != available_routes
            or value.get("vector_count") != record.count
            or value.get("actual_providers") != ["CPUExecutionProvider"]
            or type(value.get("turbovec")) is not dict
            or value["turbovec"].get("version") != "1.0.0"
            or value["turbovec"].get("source_commit") != TURBOVEC_COMMIT
            or value["turbovec"].get("license") != "MIT"
            or not _HASH.fullmatch(str(value["turbovec"].get("wheel_sha256", "")))
            or type(value.get("build_timings_seconds")) is not dict
            or set(value.get("build_timings_seconds", {})) != required_timings
        ):
            raise ResearchError("index-artifact-invalid")
        try:
            _validate_timing_mapping(value["build_timings_seconds"])
        except ResearchError:
            raise ResearchError("index-artifact-invalid") from None
    else:
        raise ResearchError("index-artifact-invalid")
    return True


def _verify_all_artifacts(root: Path, manifest: IndexManifest, deps: CliDependencies) -> None:
    expected = {"manifest.json", *(item.filename for item in manifest.artifacts)}
    try:
        if {item.name for item in root.iterdir()} != expected:
            raise ResearchError("index-artifact-invalid")
        for record in manifest.artifacts:
            path = root / record.filename
            if path.stat().st_size != record.size or _sha256_file(path) != record.sha256:
                raise ResearchError("index-artifact-mismatch")
            _validate_artifact(path, record, manifest, deps)
    except ResearchError as error:
        if error.code in {
            "vectors-shape-invalid", "vectors-dimension-invalid", "vectors-type-invalid",
            "vectors-count-invalid", "vectors-empty", "vectors-nonfinite", "vector-dtype-invalid",
            "ids-shape-invalid", "ids-count-invalid", "ids-empty", "ids-type-invalid", "ids-duplicate",
            "index-load-failed", "index-search-result-invalid",
        }:
            raise ResearchError("index-artifact-invalid") from None
        raise
    except Exception:
        raise ResearchError("index-artifact-invalid") from None


def _validate_timing_mapping(value: Mapping[str, float]) -> None:
    if type(value) is not dict or any(
        type(name) is not str
        or not name
        or not isinstance(seconds, (int, float))
        or isinstance(seconds, bool)
        or not math.isfinite(float(seconds))
        or seconds < 0
        for name, seconds in value.items()
    ):
        raise ResearchError("timing-samples-invalid")


def _raw_float32_bytes(vectors: Any) -> int:
    import numpy as np
    if not isinstance(vectors, np.ndarray) or vectors.dtype != np.float32 or vectors.ndim != 2 or vectors.size == 0:
        raise ResearchError("vectors-type-invalid")
    return int(vectors.nbytes)


def _build_evidence_document(evidence: Any, phase_timings: Mapping[str, float], storage_files: Mapping[str, int], approval: Mapping[str, Any], actual: Mapping[str, Any], deps: CliDependencies, manifest: IndexManifest, fixture_path: Path) -> Mapping[str, Any]:
    _validate_timing_mapping(phase_timings)
    info = deps.platform_info()
    peak = deps.peak_working_set()
    if type(peak) is not int or peak <= 0:
        raise ResearchError("environment-mismatch")
    try:
        if fixture_path.stat().st_size > 1024 * 1024:
            raise ResearchError("evaluation-fixture-invalid")
        fixture_hash = _sha256_file(fixture_path)
    except ResearchError:
        raise
    except Exception:
        raise ResearchError("evaluation-fixture-invalid") from None
    environment = {
        "python_version": approval["python_version"],
        "packages": dict(sorted(actual["packages"].items())),
        "dependency_lock_sha256": actual["dependency_lock_sha256"],
        "turbovec_wheel_sha256": actual["wheel_sha256"],
        "turbovec_source_commit": approval["turbovec_source_commit"],
        "turbovec_license": "MIT",
        "model_identity": approval["embedding_model"],
        "model_manifest_sha256": actual["model_manifest_sha256"],
        "model_license": approval["embedding_model_license"],
        "requested_providers": ["CPUExecutionProvider"],
        "actual_providers": ["CPUExecutionProvider"],
        "os": _safe_machine_label(info.get("platform")),
        "architecture": _safe_machine_label(info.get("architecture")),
        "processor": _safe_machine_label(info.get("processor")),
    }
    document = {
        "schema_version": 1,
        "benchmark": evidence.to_dict(),
        "environment": environment,
        "index_manifest_sha256": manifest_sha256(manifest),
        "source_hashes": [{"relative_path": item.relative_path, "sha256": item.sha256} for item in manifest.sources],
        "evaluation_fixture_sha256": fixture_hash,
        "phase_timings": {name: float(value) for name, value in sorted(phase_timings.items())},
        "storage_files": dict(sorted(storage_files.items())),
        "peak_working_set_bytes": peak,
    }
    _validate_evidence_document(document, evidence)
    return document


def _validate_evidence_document(document: Mapping[str, Any], evidence: Any) -> None:
    if type(document) is not dict or set(document) != {"schema_version", "benchmark", "environment", "index_manifest_sha256", "source_hashes", "evaluation_fixture_sha256", "phase_timings", "storage_files", "peak_working_set_bytes"}:
        raise ResearchError("benchmark-evidence-invalid")
    if document["schema_version"] != 1 or document["benchmark"] != evidence.to_dict() or not _HASH.fullmatch(str(document["index_manifest_sha256"])) or not _HASH.fullmatch(str(document["evaluation_fixture_sha256"])):
        raise ResearchError("benchmark-evidence-invalid")
    if type(document["peak_working_set_bytes"]) is not int or document["peak_working_set_bytes"] <= 0:
        raise ResearchError("benchmark-evidence-invalid")
    environment = document["environment"]
    if type(environment) is not dict or set(environment) != {"python_version", "packages", "dependency_lock_sha256", "turbovec_wheel_sha256", "turbovec_source_commit", "turbovec_license", "model_identity", "model_manifest_sha256", "model_license", "requested_providers", "actual_providers", "os", "architecture", "processor"}:
        raise ResearchError("benchmark-evidence-invalid")
    for field in ("dependency_lock_sha256", "turbovec_wheel_sha256", "model_manifest_sha256"):
        if not _HASH.fullmatch(str(environment[field])):
            raise ResearchError("benchmark-evidence-invalid")
    if environment["requested_providers"] != ["CPUExecutionProvider"] or environment["actual_providers"] != ["CPUExecutionProvider"]:
        raise ResearchError("benchmark-evidence-invalid")
    sources = document["source_hashes"]
    if type(sources) is not list or not sources or any(type(item) is not dict or set(item) != {"relative_path", "sha256"} or not _safe_relative(item["relative_path"]) or not _HASH.fullmatch(str(item["sha256"])) for item in sources):
        raise ResearchError("benchmark-evidence-invalid")
    timings = document["phase_timings"]
    required = {"document_embedding_seconds", "float32_index_build_seconds", "float32_index_save_seconds", "2bit_index_build_seconds", "4bit_index_build_seconds", "2bit_index_save_seconds", "4bit_index_save_seconds", "index_end_to_end_seconds", "query_embedding_seconds", "float32_index_load_seconds", "2bit_index_load_seconds", "4bit_index_load_seconds", "end_to_end_seconds"}
    if type(timings) is not dict or set(timings) != required or any(not isinstance(value, (int, float)) or isinstance(value, bool) or not math.isfinite(float(value)) or value < 0 for value in timings.values()):
        raise ResearchError("benchmark-evidence-invalid")
    storage = document["storage_files"]
    storage_keys = {"float32_raw_vector_bytes", "float32_persisted_npy_bytes", "2bit_persisted_bytes", "4bit_persisted_bytes"}
    if type(storage) is not dict or set(storage) != storage_keys or any(type(value) is not int or value <= 0 for value in storage.values()):
        raise ResearchError("benchmark-evidence-invalid")
    if (
        storage["float32_raw_vector_bytes"] != evidence.two_bit_storage.float32_vector_bytes
        or storage["2bit_persisted_bytes"] != evidence.two_bit_storage.candidate_persisted_bytes
        or storage["4bit_persisted_bytes"] != evidence.four_bit_storage.candidate_persisted_bytes
        or storage["float32_persisted_npy_bytes"] <= storage["float32_raw_vector_bytes"]
    ):
        raise ResearchError("benchmark-evidence-invalid")
    benchmark_json(document)


def _write_evidence_atomic(destination: Path, evidence: Any, document: Mapping[str, Any]) -> None:
    parent = destination.parent
    if not _safe_identity(destination.name):
        raise ResearchError("index-path-invalid")
    if not parent.is_dir() or any(_is_link_or_reparse(item) for item in (parent, *parent.parents)):
        raise ResearchError("index-path-invalid")
    if destination.exists() or destination.is_symlink():
        raise ResearchError("evidence-destination-exists")
    staging = parent / f".{destination.name}.staging-{uuid.uuid4().hex}"
    staging_identity = None
    owned_files = []
    published = False
    try:
        staging.mkdir()
        staging_identity = _require_plain_directory(staging)
        results_path = staging / "results.json"
        with results_path.open("xb") as stream:
            stream.write(benchmark_json(document).encode("utf-8")); stream.flush(); os.fsync(stream.fileno())
            metadata = os.fstat(stream.fileno()); owned_files.append((results_path, (metadata.st_dev, metadata.st_ino)))
        timing_lines = ["", "## Orchestration timings", "", "| Phase | Seconds |", "|---|---:|", *(f"| {name} | {float(value):.9g} |" for name, value in sorted(document["phase_timings"].items()))]
        summary_path = staging / "summary.md"
        with summary_path.open("xb") as stream:
            stream.write((render_markdown(evidence) + "\n".join(timing_lines) + "\n").encode("utf-8")); stream.flush(); os.fsync(stream.fileno())
            metadata = os.fstat(stream.fileno()); owned_files.append((summary_path, (metadata.st_dev, metadata.st_ino)))
        _require_same_directory(staging, staging_identity)
        if destination.exists() or destination.is_symlink():
            raise ResearchError("evidence-destination-exists")
        os.rename(staging, destination)
        _require_same_directory(destination, staging_identity)
        published = True
    except FileExistsError:
        raise ResearchError("evidence-destination-exists") from None
    except ResearchError:
        raise
    except Exception:
        raise ResearchError("index-promotion-failed") from None
    finally:
        if not published and staging_identity is not None:
            for path, identity in owned_files:
                _unlink_owned_file(path, identity)
            try:
                _require_same_directory(staging, staging_identity)
                staging.rmdir()
            except Exception:
                pass


def _safe_machine_label(value: Any) -> str:
    text = str(value or "unknown")[:80]
    sanitized = "".join(character if character.isalnum() or character in " ._+-" else "-" for character in text).strip()
    return sanitized or "unknown"


def _validate_query_text(value: Any) -> str:
    if type(value) is not str or not value.strip():
        raise ResearchError("query-empty")
    if len(value) > MAX_QUERY_CHARS:
        raise ResearchError("query-too-long")
    if any((ord(character) < 32 and character not in "\t\n\r") or ord(character) == 127 for character in value):
        raise ResearchError("query-control-character")
    return value


def _excerpt(value: str) -> str:
    normalized = " ".join(value.split())
    return normalized[:240]


def _diagnostic(code: str, relative_identity: str | None = None) -> Mapping[str, Any]:
    value: dict[str, Any] = {"schema_version": 1, "code": code}
    if relative_identity is not None and _safe_identity(relative_identity):
        value["relative_identity"] = relative_identity
    return value


@contextlib.contextmanager
def _suppress_dependency_output():
    saved = []
    try:
        sys.stdout.flush(); sys.stderr.flush()
        with open(os.devnull, "w", encoding="utf-8") as null:
            for descriptor in (1, 2):
                saved.append((descriptor, os.dup(descriptor)))
                os.dup2(null.fileno(), descriptor)
            with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                yield
    finally:
        for descriptor, original in reversed(saved):
            try:
                os.dup2(original, descriptor)
            finally:
                os.close(original)


def _json_bytes(value: Mapping[str, Any]) -> bytes:
    return json.dumps(value, ensure_ascii=False, allow_nan=False, separators=(",", ":"), sort_keys=True).encode("utf-8")


def _safe_identity(value: str) -> bool:
    return type(value) is str and re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]{0,127}", value) is not None


def _safe_relative(value: Any) -> bool:
    if type(value) is not str or not value or "\\" in value or ":" in value or any(ord(c) < 32 for c in value):
        return False
    p, w = PurePosixPath(value), PureWindowsPath(value)
    return not p.is_absolute() and not w.is_absolute() and not w.drive and all(part not in ("", ".", "..") for part in p.parts)


def _unique_pairs(pairs):
    return _unique_mapping(pairs, "approved-input-invalid")


def _unique_mapping(pairs, code: str):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ResearchError(code)
        result[key] = value
    return result


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while block := stream.read(1024 * 1024):
            digest.update(block)
    return digest.hexdigest()


def _npy_version(path: Path) -> tuple[int, int]:
    try:
        with path.open("rb") as stream:
            header = stream.read(8)
        if len(header) != 8 or header[:6] != b"\x93NUMPY":
            raise ResearchError("index-artifact-invalid")
        return header[6], header[7]
    except ResearchError:
        raise
    except Exception:
        raise ResearchError("index-artifact-invalid") from None


def _is_link_or_reparse(path: Path) -> bool:
    try:
        metadata = path.lstat()
        return path.is_symlink() or bool(getattr(metadata, "st_file_attributes", 0) & 0x400)
    except OSError:
        raise ResearchError("approval-mismatch") from None


def _model_manifest_sha256(root: Path) -> str:
    records = []
    total = 0
    for path in sorted(root.rglob("*"), key=lambda item: item.relative_to(root).as_posix()):
        if _is_link_or_reparse(path):
            raise ResearchError("approval-mismatch")
        if not path.is_file():
            continue
        if len(records) >= MAX_MODEL_FILES:
            raise ResearchError("approval-mismatch")
        size = path.stat().st_size
        total += size
        if total > MAX_MODEL_BYTES:
            raise ResearchError("approval-mismatch")
        records.append({"path": path.relative_to(root).as_posix(), "size": size, "sha256": _sha256_file(path)})
    if not records:
        raise ResearchError("approval-mismatch")
    return hashlib.sha256(_json_bytes({"files": records, "schema_version": 1})).hexdigest()


def _installed_locked_versions() -> Mapping[str, str]:
    result = {}
    try:
        for line in REQUIREMENTS_LOCK.read_text(encoding="utf-8").splitlines():
            if not line:
                continue
            name, approved = line.split("==", 1)
            actual = importlib.metadata.version(name)
            if actual != approved:
                raise ResearchError("dependency-mismatch")
            result[name.casefold()] = actual
    except ResearchError:
        raise
    except Exception:
        raise ResearchError("dependency-unavailable") from None
    return result


def _configured_wheel_sha256() -> str:
    wheel = os.environ.get("GRANITE_TURBOVEC_WHEEL")
    if not wheel:
        raise ResearchError("approval-mismatch")
    try:
        path = Path(wheel)
        if not path.is_absolute() or path.suffix.casefold() != ".whl" or not path.is_file() or any(_is_link_or_reparse(item) for item in (path, *path.parents)):
            raise ResearchError("approval-mismatch")
        return _sha256_file(path)
    except ResearchError:
        raise
    except Exception:
        raise ResearchError("approval-mismatch") from None


def _provider(embedder: Any) -> str:
    providers = list(getattr(embedder, "providers", ()))
    return providers[0] if providers else "unknown"


def _validated_embedder(approval: Mapping[str, Any], deps: CliDependencies):
    embedder = deps.make_embedder(Path(approval["model_cache_root"]))
    if getattr(embedder, "model_identity", None) != approval["embedding_model"] or getattr(embedder, "dimension", None) != 384:
        raise ResearchError("approval-mismatch")
    if list(getattr(embedder, "providers", ())) != ["CPUExecutionProvider"]:
        raise ResearchError("environment-mismatch")
    return embedder


def _peak_working_set_bytes() -> int:
    if os.name == "nt":
        try:
            import ctypes
            from ctypes import wintypes
            class Counters(ctypes.Structure):
                _fields_ = [("cb", wintypes.DWORD), ("PageFaultCount", wintypes.DWORD), ("PeakWorkingSetSize", ctypes.c_size_t), ("WorkingSetSize", ctypes.c_size_t), ("QuotaPeakPagedPoolUsage", ctypes.c_size_t), ("QuotaPagedPoolUsage", ctypes.c_size_t), ("QuotaPeakNonPagedPoolUsage", ctypes.c_size_t), ("QuotaNonPagedPoolUsage", ctypes.c_size_t), ("PagefileUsage", ctypes.c_size_t), ("PeakPagefileUsage", ctypes.c_size_t)]
            counters = Counters(); counters.cb = ctypes.sizeof(counters)
            if not ctypes.windll.psapi.GetProcessMemoryInfo(ctypes.windll.kernel32.GetCurrentProcess(), ctypes.byref(counters), counters.cb):
                raise OSError
            return int(counters.PeakWorkingSetSize)
        except Exception:
            raise ResearchError("environment-mismatch") from None
    raise ResearchError("environment-mismatch")


if __name__ == "__main__":
    raise SystemExit(main())
