"""Independent quality and repetition statistics for formal TurboVec evidence."""

from __future__ import annotations

import math
import statistics
from typing import Mapping, Sequence

from .dataset_schema import FrozenDataset
from .metrics import mean_reciprocal_rank, ndcg_at_k, recall_at_k


T_CRITICAL_95 = {2: 12.706, 3: 4.303, 4: 3.182, 5: 2.776}


def _mean(values: Sequence[float]) -> float:
    return float(statistics.fmean(values))


def evaluate_repetition(dataset: FrozenDataset, repetition: Mapping[str, object]) -> dict[str, object]:
    order = [int(item) for item in repetition["query_order"]]  # type: ignore[index]
    configurations = repetition["configurations"]  # type: ignore[index]
    if sorted(order) != list(range(len(dataset.queries))) or not isinstance(configurations, Mapping):
        raise ValueError("invalid query order or configurations")
    chunk_by_id = {chunk.ordinal: chunk for chunk in dataset.chunks}
    ordinal_by_chunk_id = {chunk.chunk_id: chunk.ordinal for chunk in dataset.chunks}
    evaluation_positions = [position for position, query_index in enumerate(order) if dataset.queries[query_index].split == "evaluation"]
    absent_positions = [position for position in evaluation_positions if dataset.queries[order[position]].kind == "absent_answer"]
    relevant_positions = [position for position in evaluation_positions if position not in absent_positions]
    expected_by_position = {
        position: {ordinal_by_chunk_id[item] for item in dataset.relevance[dataset.queries[order[position]].query_id]}
        for position in relevant_positions
    }
    grade_by_position = {
        position: {ordinal_by_chunk_id[item]: grade for item, grade in dataset.relevance[dataset.queries[order[position]].query_id].items()}
        for position in relevant_positions
    }
    exact_rankings = configurations["exact"]["result_ids"]  # type: ignore[index]
    output: dict[str, dict[str, object]] = {}
    exact_ndcg = None
    for name, raw in configurations.items():
        row = raw  # type: ignore[assignment]
        rankings = [[int(item) for item in ranked] for ranked in row["result_ids"]]
        scores = [[float(item) for item in ranked] for ranked in row["result_scores"]]
        if len(rankings) != len(order) or len(scores) != len(order):
            raise ValueError("result/query count mismatch")
        recalls = {
            k: _mean([recall_at_k(rankings[position], expected_by_position[position], k) for position in relevant_positions])
            for k in (1, 5, 10)
        }
        ndcg = _mean([ndcg_at_k(rankings[position], grade_by_position[position], 10) for position in relevant_positions])
        if name == "exact":
            exact_ndcg = ndcg
        source_hits = []
        page_hits = []
        for position in relevant_positions:
            expected_chunks = [chunk_by_id[item] for item in expected_by_position[position]]
            first = chunk_by_id.get(rankings[position][0])
            source_hits.append(float(first is not None and first.source_document_id in {item.source_document_id for item in expected_chunks}))
            page_hits.append(float(first is not None and (first.source_document_id, first.page_number) in {(item.source_document_id, item.page_number) for item in expected_chunks}))
        exact_overlap = _mean([
            len(set(rankings[position][:10]) & set(exact_rankings[position][:10])) / 10.0
            for position in evaluation_positions
        ])
        latency = row["latency_summary_ms"]
        storage = row["storage"]
        output[str(name)] = {
            "recall_at_1": recalls[1],
            "recall_at_5": recalls[5],
            "recall_at_10": recalls[10],
            "ndcg_at_10": ndcg,
            "mrr": mean_reciprocal_rank([rankings[position] for position in relevant_positions], [expected_by_position[position] for position in relevant_positions]),
            "source_accuracy": _mean(source_hits),
            "page_accuracy": _mean(page_hits),
            "absent_top_score_mean": _mean([scores[position][0] for position in absent_positions]),
            "recall_at_10_against_exact": exact_overlap,
            "p95_latency_ms": float(latency["p95"]),
            "storage_bytes": int(storage["equivalent_total_bytes"]),
            "incremental_memory_bytes": int(row["memory"]["incremental_peak_process_working_set_bytes"]),
            "lifecycle_passed": all(bool(value) for value in row["lifecycle"].values()),
        }
    if exact_ndcg is None or exact_ndcg <= 0:
        raise ValueError("exact nDCG reference is unavailable")
    exact_latency = float(output["exact"]["p95_latency_ms"])
    exact_storage = int(output["exact"]["storage_bytes"])
    exact_absent = float(output["exact"]["absent_top_score_mean"])
    for row in output.values():
        row["relative_ndcg_at_10"] = float(row["ndcg_at_10"]) / exact_ndcg
        row["p95_slowdown_vs_exact"] = float(row["p95_latency_ms"]) / exact_latency
        row["storage_ratio"] = exact_storage / int(row["storage_bytes"])
        row["absent_score_delta_vs_exact"] = float(row["absent_top_score_mean"]) - exact_absent
    return {
        "valid": bool(repetition["valid"]),
        "evaluation_query_counts": {"total": len(evaluation_positions), "answerable": len(relevant_positions), "absent_answer": len(absent_positions)},
        "configurations": output,
    }


def _describe(values: Sequence[float]) -> dict[str, float | int]:
    if not values:
        raise ValueError("statistics require at least one value")
    mean = _mean(values)
    deviation = statistics.stdev(values) if len(values) > 1 else 0.0
    critical = T_CRITICAL_95.get(len(values), 1.96)
    margin = critical * deviation / math.sqrt(len(values))
    return {
        "n": len(values), "median": float(statistics.median(values)), "mean": mean,
        "minimum": float(min(values)), "maximum": float(max(values)), "standard_deviation": float(deviation),
        "ci95_low": mean - margin, "ci95_high": mean + margin,
    }


def summarize_repetitions(repetitions: Sequence[Mapping[str, object]]) -> dict[str, dict[str, dict[str, float | int]]]:
    valid = [item for item in repetitions if item.get("valid") is True]
    if not valid:
        raise ValueError("no valid repetitions")
    names = tuple(valid[0]["configurations"])  # type: ignore[arg-type,index]
    result: dict[str, dict[str, dict[str, float | int]]] = {}
    for name in names:
        rows = [item["configurations"][name] for item in valid]  # type: ignore[index]
        numeric_keys = set.intersection(*[
            {key for key, value in row.items() if isinstance(value, (int, float)) and not isinstance(value, bool)}
            for row in rows
        ])
        result[name] = {key: _describe([float(row[key]) for row in rows]) for key in sorted(numeric_keys)}
    return result
