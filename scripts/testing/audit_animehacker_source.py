"""Evidence-first source audit for the animehacker TQ3_0 fork."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path


SOURCE_SUFFIXES = {".c", ".cc", ".cpp", ".cuh", ".cu", ".h", ".hpp"}
SKIP_PARTS = {"docs", "examples", "tests", "vendor", "build"}
BACKEND_DIRS = {"cuda": "ggml-cuda", "sycl": "ggml-sycl", "vulkan": "ggml-vulkan"}


def _source_files(root: Path) -> list[Path]:
    return sorted(
        path for path in root.rglob("*")
        if path.is_file()
        and path.suffix.lower() in SOURCE_SUFFIXES
        and not SKIP_PARTS.intersection(path.relative_to(root).parts)
    )


def _without_comments(text: str) -> str:
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.DOTALL)
    return re.sub(r"//.*", "", text)


def _evidence(root: Path, files: list[Path], patterns: tuple[str, ...]) -> list[dict]:
    records = []
    for path in files:
        text = path.read_text(encoding="utf-8", errors="replace")
        if all(re.search(pattern, text, re.IGNORECASE) for pattern in patterns):
            records.append({
                "path": path.relative_to(root).as_posix(),
                "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            })
    return records


def audit_source(root: Path) -> dict:
    root = root.resolve()
    files = _source_files(root)
    type_evidence = _evidence(root, files, (r"GGML_TYPE_TQ3_0",))
    quant_evidence = _evidence(root, files, (r"quantize_(?:row_)?tq3_0",))
    dequant_evidence = _evidence(root, files, (r"dequantize_(?:row_)?tq3_0",))
    source_evidence = [
        {**record, "category": category}
        for category, records in (
            ("type-registration", type_evidence),
            ("quantization", quant_evidence),
            ("dequantization", dequant_evidence),
        )
        for record in records
    ]

    has_cpu = bool(type_evidence and quant_evidence and dequant_evidence)
    backend_results = {}
    implementation_backends = ["cpu"] if has_cpu else []
    for backend, directory in BACKEND_DIRS.items():
        candidates = [path for path in files if directory in path.parts]
        specific = _evidence(
            root, candidates, (r"TQ3_0|tq3_0", r"quantize|dequantize|vec_dot|cpy"),
        )
        backend_results[backend] = {
            "backend_source_present": bool(candidates),
            "tq3_0_specific": bool(specific),
            "source_evidence": specific,
        }
        if specific:
            implementation_backends.append(backend)

    qjl_evidence = []
    qjl_patterns = (r"qjl_(?:project|transform|correct)", r"johnson[_ -]?lindenstrauss")
    for path in files:
        code = _without_comments(path.read_text(encoding="utf-8", errors="replace"))
        if any(re.search(pattern, code, re.IGNORECASE) for pattern in qjl_patterns):
            qjl_evidence.append({
                "path": path.relative_to(root).as_posix(),
                "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            })

    docs_claim = any(
        "tq3_0" in path.read_text(encoding="utf-8", errors="replace").lower()
        for path in root.glob("README*") if path.is_file()
    )
    depth = "source-implemented" if has_cpu else ("documentation-only" if docs_claim else "not-found")
    return {
        "root": str(root),
        "tq3_0": {
            "implemented": has_cpu,
            "implementation_depth": depth,
            "implementation_backends": implementation_backends,
            "source_evidence": source_evidence,
        },
        "backends": backend_results,
        "qjl": {
            "status": "implemented" if qjl_evidence else "not-proven",
            "source_evidence": qjl_evidence,
        },
    }


def render_limitations(audit: dict) -> str:
    sycl = "source present" if audit["backends"]["sycl"]["tq3_0_specific"] else "not proven"
    vulkan = "source present" if audit["backends"]["vulkan"]["tq3_0_specific"] else "not proven"
    qjl = "implemented" if audit["qjl"]["status"] == "implemented" else "not proven"
    return (
        "# animehacker TQ3_0 source-audit limitations\n\n"
        f"- Implementation depth: {audit['tq3_0']['implementation_depth']}.\n"
        f"- QJL: {qjl}. The format must not be described as full TurboQuant with residual correction.\n"
        f"- SYCL TQ3_0: {sycl}; runtime verification is still required.\n"
        f"- Vulkan TQ3_0: {vulkan}; runtime verification is still required before any support claim.\n"
        "- Source presence does not prove successful compilation, device placement, or runtime activation.\n"
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("root", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    result = audit_source(args.root)
    rendered = json.dumps(result, indent=2, sort_keys=True) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered, encoding="utf-8")
    else:
        print(rendered, end="")
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(render_limitations(result), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
