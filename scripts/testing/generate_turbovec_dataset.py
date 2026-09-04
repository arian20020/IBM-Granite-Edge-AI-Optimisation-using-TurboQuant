"""Generate closed provenance records for the deterministic TurboVec dataset."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))

from scripts.testing.turbovec.dataset import SUPPORTED_SCALES, build_frozen_dataset


def _write(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n",
        encoding="utf-8",
        newline="\n",
    )


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-root", type=Path, required=True)
    args = parser.parse_args(argv)
    root = args.output_root.resolve()
    summaries = []
    for scale in SUPPORTED_SCALES:
        dataset = build_frozen_dataset(scale)
        summaries.append({
            "scale": scale,
            "chunks": len(dataset.chunks),
            "queries": len(dataset.queries),
            "development_queries": sum(q.split == "development" for q in dataset.queries),
            "evaluation_queries": sum(q.split == "evaluation" for q in dataset.queries),
            "corpus_sha256": dataset.corpus_sha256,
            "query_sha256": dataset.query_sha256,
            "relevance_sha256": dataset.relevance_sha256,
        })
    _write(root / "corpus-v2-provenance.json", {"schema_version": "2.0", "generator": "turbovec-dataset-v2", "scales": summaries})
    reference = build_frozen_dataset(10_000)
    _write(root / "queries-v2.json", {"schema_version": "2.0", "scale": 10_000, "queries": [q.__dict__ for q in reference.queries]})
    _write(root / "relevance-v2.json", {"schema_version": "2.0", "scale": 10_000, "relevance": reference.relevance})
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
