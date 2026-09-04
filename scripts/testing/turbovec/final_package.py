"""Validate the final TurboVec research handoff and its committed evidence links."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


ALLOWED_DISPOSITIONS = {"INTEGRATE_CANDIDATE", "DEMONSTRATOR_ONLY", "EXCLUDE", "BLOCKED"}
REQUIRED_DOCUMENTS = (
    "README.md",
    "environment-manifest.json",
    "evidence-manifest.json",
    "final-decision.md",
    "final-research-report.md",
    "gate-a-audit.md",
    "gate-b-audit.md",
    "handoff-receipt.md",
    "machine-readiness-report.md",
    "per-scale-results.md",
    "reproduction-guide.md",
    "storage-memory-accounting.md",
    "threats-to-validity.md",
)


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def validate_final_package(repository_root: Path) -> dict[str, object]:
    report_root = repository_root / "docs/testing/turbovec/production-scale-v2"
    manifest_path = report_root / "evidence-manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    disposition = manifest.get("disposition")
    if disposition not in ALLOWED_DISPOSITIONS:
        raise ValueError(f"unsupported disposition: {disposition!r}")
    missing = [name for name in REQUIRED_DOCUMENTS if not (report_root / name).is_file()]
    if missing:
        raise ValueError(f"missing required documents: {missing}")
    verified = 0
    for run in manifest.get("formal_runs", []):
        if run.get("status") != "completed":
            continue
        run_root = repository_root / "experiments/raw-results/turbovec/production-scale-v2" / str(run["run_id"])
        for name in ("benchmark", "evaluation", "summary"):
            path = run_root / f"{name}.json"
            if path.stat().st_size != int(run[f"{name}_bytes"]):
                raise ValueError(f"size mismatch: {path}")
            if _sha256(path) != run[f"{name}_sha256"]:
                raise ValueError(f"hash mismatch: {path}")
            verified += 1
    return {
        "disposition": disposition,
        "required_document_count": len(REQUIRED_DOCUMENTS),
        "verified_formal_files": verified,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repository-root", type=Path, default=Path.cwd())
    args = parser.parse_args(argv)
    print(json.dumps(validate_final_package(args.repository_root.resolve()), sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
