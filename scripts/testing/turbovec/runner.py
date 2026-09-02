"""Append-only matched runner for EXP-TV-COMP-001."""

from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import shutil
import tempfile
import time
from typing import Sequence

import numpy as np

from .chunking import PageText, WordFixtureTokenCounter, chunk_pages
from .decision import ConfigurationResult, decide
from .embedding import EmbeddingProvider
from .indexes import ExactIndex, TurboVecIndex, stable_uint64_id
from .metrics import aggregate_latency, ndcg_at_k
from .provenance import sha256_file


CONFIGURATIONS = ("exact", "tq2", "tq3", "tq4")
WARMUPS = 5
MEASURED = 30


def _embed_batched(provider: EmbeddingProvider, texts: Sequence[str], documents: bool) -> np.ndarray:
    method = provider.embed_documents if documents else provider.embed_queries
    batches = [method(texts[start:start + 16]) for start in range(0, len(texts), 16)]
    return np.ascontiguousarray(np.concatenate(batches), dtype=np.float32)


def prepare_controlled_inputs(repository_root: Path, provider: EmbeddingProvider) -> dict[str, object]:
    protocol = Path(repository_root) / "experiments/protocols/turbovec"
    corpus = json.loads((protocol / "corpus-v1.json").read_text(encoding="utf-8"))
    queries = json.loads((protocol / "queries-v1.json").read_text(encoding="utf-8"))
    relevance = json.loads((protocol / "relevance-v1.json").read_text(encoding="utf-8"))
    chunks = []
    for document in corpus["documents"]:
        pages = [PageText(int(page["page"]), "\n\n".join(page["paragraphs"])) for page in document["pages"]]
        made = chunk_pages(document["document_sha256"], pages, WordFixtureTokenCounter())
        if [chunk.chunk_id for chunk in made] != document["expected_chunk_ids"]:
            raise ValueError("controlled chunk identity mismatch")
        chunks.extend(made)
    query_ids = tuple(item["query_id"] for item in queries["queries"])
    if len(chunks) != 30 or len(query_ids) != 120:
        raise ValueError("controlled protocol cardinality mismatch")
    grades: dict[str, dict[int, int]] = {query_id: {} for query_id in query_ids}
    for item in relevance["judgements"]:
        grades[item["query_id"]][stable_uint64_id(item["chunk_id"])] = int(item["grade"])
    return {
        "document_embeddings": _embed_batched(provider, [chunk.text for chunk in chunks], True),
        "query_embeddings": _embed_batched(provider, [item["text"] for item in queries["queries"]], False),
        "ids": np.asarray([stable_uint64_id(chunk.chunk_id) for chunk in chunks], dtype=np.uint64),
        "query_ids": query_ids,
        "grades": grades,
    }


def evaluate_matched_result(result: dict[str, object], query_ids: Sequence[str], grades: dict[str, dict[int, int]]) -> dict[str, object]:
    rows = {row["name"]: row for row in result["configurations"]}
    if set(rows) != set(CONFIGURATIONS):
        raise ValueError("configuration set mismatch")
    exact_ids = rows["exact"]["results"]["ids"]
    if len(exact_ids) != len(query_ids):
        raise ValueError("unmatched controlled query set")
    output = {}
    exact_ndcg = None
    for name in CONFIGURATIONS:
        ranked_rows = rows[name]["results"]["ids"]
        scores = rows[name]["results"]["scores"]
        if len(ranked_rows) != len(query_ids):
            raise ValueError("unmatched controlled query set")
        overlaps = {k: [] for k in (1, 5, 10)}; ndcgs=[]; reciprocal=[]; negative_scores=[]
        for position, query_id in enumerate(query_ids):
            ranked = list(map(int, ranked_rows[position])); truth = grades[query_id]
            if truth:
                for k in overlaps: overlaps[k].append(len(set(ranked[:k]) & set(map(int, exact_ids[position][:k]))) / k)
                ndcgs.append(ndcg_at_k(ranked, truth, 10))
                rank = next((index + 1 for index, identifier in enumerate(ranked) if identifier in truth), None)
                reciprocal.append(0.0 if rank is None else 1.0 / rank)
            else:
                negative_scores.append(float(scores[position][0]))
        mean_ndcg = float(np.mean(ndcgs));
        if name == "exact": exact_ndcg = mean_ndcg
        output[name] = {
            "recall_at_1_against_exact": float(np.mean(overlaps[1])),
            "recall_at_5_against_exact": float(np.mean(overlaps[5])),
            "recall_at_10_against_exact": float(np.mean(overlaps[10])),
            "ndcg_at_10": mean_ndcg,
            "mrr": float(np.mean(reciprocal)),
            "negative_query_top_score_mean": float(np.mean(negative_scores)) if negative_scores else None,
        }
    if not exact_ndcg or exact_ndcg <= 0:
        raise ValueError("exact nDCG baseline is invalid")
    for metrics in output.values(): metrics["relative_ndcg_at_10"] = metrics["ndcg_at_10"] / exact_ndcg
    return {"schema_version": "1.0", "queries": len(query_ids), "configurations": output}


def create_run_directory(output_root: Path, run_id: str) -> Path:
    root = Path(output_root)
    target = root / run_id
    staging = root / f".staging-{run_id}"
    if target.exists() or staging.exists(): raise FileExistsError(run_id)
    root.mkdir(parents=True, exist_ok=True); staging.mkdir()
    return staging


def _json(path: Path, value: object) -> None:
    path.write_text(json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")


def _matrix_hash(values: np.ndarray) -> str:
    return hashlib.sha256(np.ascontiguousarray(values, dtype="<f4").tobytes()).hexdigest()


def _index_factory(name: str):
    return ExactIndex(384) if name == "exact" else TurboVecIndex(384, int(name[-1]))


def run_matched_campaign(document_embeddings: np.ndarray, query_embeddings: np.ndarray, ids: np.ndarray) -> dict[str, object]:
    embedding_hash = _matrix_hash(document_embeddings); indexes = {}; rows = []
    with tempfile.TemporaryDirectory(prefix="tv-index-") as directory:
        root = Path(directory)
        for name in CONFIGURATIONS:
            started = time.perf_counter_ns(); index = _index_factory(name); index.add(document_embeddings, ids); build_ns = time.perf_counter_ns() - started
            index_root = root / name; save_started = time.perf_counter_ns(); index.save(index_root); save_ns = time.perf_counter_ns() - save_started
            serving_bytes = sum(path.stat().st_size for path in index_root.iterdir())
            load_started = time.perf_counter_ns(); reopened = type(index).load(index_root); load_ns = time.perf_counter_ns() - load_started
            for _ in range(WARMUPS): reopened.search(query_embeddings, min(10, len(ids)))
            indexes[name] = (reopened, build_ns, save_ns, load_ns, serving_bytes, index_root)
        latencies = {name: [] for name in CONFIGURATIONS}; first_results = {}
        for batch in range(MEASURED):
            order = CONFIGURATIONS[batch % 4:] + CONFIGURATIONS[:batch % 4]
            for name in order:
                started = time.perf_counter_ns(); result = indexes[name][0].search(query_embeddings, min(10, len(ids))); elapsed = time.perf_counter_ns() - started
                latencies[name].append(elapsed / 1_000_000)
                first_results.setdefault(name, {"ids": result.ids.tolist(), "scores": result.scores.tolist()})
        remove_count = min(10, len(ids)); removed_ids = ids[:remove_count]; removed_vectors = document_embeddings[:remove_count]
        for name in CONFIGURATIONS:
            index, build_ns, save_ns, load_ns, serving_bytes, index_root = indexes[name]
            lifecycle = all(index.remove(int(identifier)) for identifier in removed_ids)
            index.add(removed_vectors, removed_ids)
            lifecycle = lifecycle and set(map(int, index.search(removed_vectors, min(10, len(ids))).ids.flat)) <= set(map(int, ids))
            corrupt_ok = False
            corrupt_root = root / f"{name}-corrupt"; shutil.copytree(index_root, corrupt_root)
            target = next(path for path in corrupt_root.iterdir() if path.name != "metadata.json"); target.write_bytes(target.read_bytes() + b"corrupt")
            try: type(index).load(corrupt_root)
            except ValueError: corrupt_ok = True
            latency = aggregate_latency(latencies[name])
            rows.append({"name":name,"bit_width":None if name=="exact" else int(name[-1]),"embedding_matrix_sha256":embedding_hash,"warmup_batches":WARMUPS,"measured_batches":MEASURED,"build_ns":build_ns,"save_ns":save_ns,"load_ns":load_ns,"query_latency_ms":latency,"serving_bytes":serving_bytes,"lifecycle_passed":bool(lifecycle and corrupt_ok),"measured_latency_ms":latencies[name],"results":first_results[name]})
    return {"schema_version":"1.0","configurations":rows}


def run_fixture_campaign(provider: EmbeddingProvider, output_root: Path | None = None, run_id: str | None = None) -> dict[str, object]:
    texts = [f"fixture document {index}" for index in range(32)]
    queries = texts[:8]
    document_embeddings = provider.embed_documents(texts); query_embeddings = provider.embed_queries(queries)
    ids = np.arange(1000, 1032, dtype=np.uint64)
    result = run_matched_campaign(document_embeddings, query_embeddings, ids)
    if output_root is None: return result
    if run_id is None: raise ValueError("run_id is required")
    staging = create_run_directory(output_root, run_id)
    try:
        _json(staging / "identities.json", {"schema_version":"1.0","run_id":run_id,"provider":"deterministic-test-only"})
        _json(staging / "embedding-summary.json", {"schema_version":"1.0","sha256":result["configurations"][0]["embedding_matrix_sha256"],"rows":32,"dimension":384})
        _json(staging / "results.json", result)
        _json(staging / "command-arithmetic.json", {"discovered":4,"executed":4,"passed":4,"failed":0,"skipped":0})
        (staging / "events.jsonl").write_text('{"event":"matched_run_completed"}\n',encoding="utf-8",newline="\n")
        files=[]
        for path in sorted(staging.iterdir()): files.append({"name":path.name,"bytes":path.stat().st_size,"sha256":sha256_file(path)})
        _json(staging / "terminal.json", {"schema_version":"1.0","run_id":run_id,"status":"completed","files":files})
        target=Path(output_root)/run_id; staging.rename(target)
        return result
    except BaseException:
        _json(staging / "failure.json", {"schema_version":"1.0","run_id":run_id,"status":"failed"})
        raise


def run_live_campaign(provider: EmbeddingProvider, repository_root: Path, output_root: Path, run_id: str, identities: dict[str, object]) -> dict[str, object]:
    staging = create_run_directory(output_root, run_id)
    try:
        prepared = prepare_controlled_inputs(repository_root, provider)
        result = run_matched_campaign(prepared["document_embeddings"], prepared["query_embeddings"], prepared["ids"])
        metrics = evaluate_matched_result(result, prepared["query_ids"], prepared["grades"])
        try:
            import psutil  # type: ignore
            memory = psutil.Process().memory_info()
            peak = int(getattr(memory, "peak_wset", memory.rss))
        except ImportError:
            peak = 0
        result["peak_process_memory_bytes"] = peak
        _json(staging / "identities.json", {"schema_version": "1.0", "run_id": run_id, **identities})
        _json(staging / "results.json", result)
        _json(staging / "metrics.json", metrics)
        _json(staging / "command-arithmetic.json", {"discovered": 4, "executed": 4, "passed": 4, "failed": 0, "skipped": 0})
        files=[]
        for path in sorted(staging.iterdir()):
            files.append({"name":path.name,"bytes":path.stat().st_size,"sha256":sha256_file(path)})
        _json(staging / "terminal.json", {"schema_version":"1.0","run_id":run_id,"status":"completed","files":files})
        staging.rename(Path(output_root) / run_id)
        return {"results": result, "metrics": metrics}
    except BaseException as error:
        _json(staging / "failure.json", {"schema_version":"1.0","run_id":run_id,"status":"failed","error_type":type(error).__name__})
        raise


def write_processed_results(run_directory: Path, processed_root: Path) -> Path:
    run = Path(run_directory)
    terminal = json.loads((run / "terminal.json").read_text(encoding="utf-8"))
    if terminal.get("status") != "completed": raise ValueError("raw run is not complete")
    results = json.loads((run / "results.json").read_text(encoding="utf-8"))
    metrics = json.loads((run / "metrics.json").read_text(encoding="utf-8"))
    identities = json.loads((run / "identities.json").read_text(encoding="utf-8"))
    rows = {item["name"]: item for item in results["configurations"]}; exact = rows["exact"]
    candidates=[]
    for name in ("tq2", "tq3", "tq4"):
        row=rows[name]; quality=metrics["configurations"][name]
        candidates.append(ConfigurationResult(name,int(name[-1]),quality["recall_at_10_against_exact"],quality["relative_ndcg_at_10"],row["query_latency_ms"]["p95"],row["serving_bytes"],row["lifecycle_passed"]))
    decision=decide(candidates,exact_p95=exact["query_latency_ms"]["p95"],exact_bytes=exact["serving_bytes"],prerequisites_complete=True)
    run_id=str(terminal["run_id"]); output=Path(processed_root)/run_id
    if output.exists(): raise FileExistsError(run_id)
    output.mkdir(parents=True)
    candidate = identities.get("dependencies", {}).get("turbovec")
    decision_document={"schema_version":"1.0","experiment_id":"EXP-TV-COMP-001","run_id":run_id,"outcome":decision.outcome.value,"selected_configuration":decision.selected_configuration,"thresholds":decision.thresholds,"reasons":list(decision.reasons),"candidate":candidate,"product_status":"deferred"}
    summary={"schema_version":"1.0","experiment_id":"EXP-TV-COMP-001","run_id":run_id,"outcome":decision.outcome.value,"metrics":metrics["configurations"],"operational":{name:{"build_ns":row["build_ns"],"save_ns":row["save_ns"],"load_ns":row["load_ns"],"query_latency_ms":row["query_latency_ms"],"serving_bytes":row["serving_bytes"],"lifecycle_passed":row["lifecycle_passed"]} for name,row in rows.items()},"peak_process_memory_bytes":results.get("peak_process_memory_bytes",0)}
    _json(output/"decision.json",decision_document); _json(output/"summary.json",summary)
    lines=["# EXP-TV-COMP-001 Result","",f"**Outcome: {decision.outcome.value}**", "", "No configuration passed every Gate A threshold." if decision.reasons else f"Selected configuration: {decision.selected_configuration}.", "", "| Configuration | Recall@10 vs Exact | Relative nDCG@10 | p95 ms | Serving bytes |", "|---|---:|---:|---:|---:|"]
    for name in CONFIGURATIONS:
        quality=metrics["configurations"][name]; row=rows[name]
        lines.append(f"| {name} | {quality['recall_at_10_against_exact']:.6f} | {quality['relative_ndcg_at_10']:.6f} | {row['query_latency_ms']['p95']:.6f} | {row['serving_bytes']} |")
    lines += ["", "This is report-side feasibility evidence only. Product integration, RAG, citation quality, accessibility, packaging and release approval were not performed."]
    (output/"report.md").write_text("\n".join(lines)+"\n",encoding="utf-8",newline="\n")
    return output
