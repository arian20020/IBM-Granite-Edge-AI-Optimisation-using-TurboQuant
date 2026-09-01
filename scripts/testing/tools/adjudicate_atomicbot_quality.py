"""Create content-keyed, harsh GTQ-QUALITY-RUBRIC-v1 adjudications."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path


DIMENSIONS = (
    "correctness_and_grounding", "instruction_and_format_adherence",
    "completeness_and_fact_retention", "relevance_clarity_and_coherence",
    "stability_and_output_integrity",
)


def decision(scores, passed, cap, reason, fmt, facts, unsupported=0, integrity="No", notes=""):
    return {
        "dimensions": dict(zip(DIMENSIONS, scores)), "deterministic_pass": passed,
        "critical_caps": [] if cap is None else [cap], "critical_cap_reason": reason,
        "format_valid": fmt, "required_facts_retained": facts,
        "unsupported_statements_count": unsupported, "integrity_issue": integrity,
        "manual_result": reason, "notes": notes or "Harsh content-based adjudication under the frozen rubric.",
    }


def p1(output: str):
    bullets = [line for line in output.splitlines() if re.match(r"^\s*[-*]\s+", line)]
    words = re.findall(r"\b[\w'-]+\b", output)
    required = all(term in output for term in ("KV cache", "offline", "memory"))
    labelled = ((output.count("**Benefit**") >= 2 and output.count("**Limitation**") >= 2)
                or (output.count("Benefit:") >= 2 and output.count("Limitation:") >= 2))
    combined = "**Benefits:**" in output and "**Limitations:**" in output and "**Check:**" in output
    slots = labelled or combined
    passed = len(bullets) == 5 and len(words) <= 89 and required and slots
    if len(output.strip()) < 20:
        return decision((1, 0, 0, 1, 1), False, 2, "Severely incomplete/corrupted response", "No", "No", integrity="Yes")
    if passed:
        return decision((9, 9, 9, 9, 9), True, None, "No critical cap", "Yes", "Yes")
    return decision((6, 4, 4, 8, 9), False, 4,
                    f"P1 gate failure: bullets={len(bullets)}, words={len(words)}, required_terms={required}, semantic_slots={slots}",
                    "No" if len(bullets) != 5 else "Yes", "No")


def p2(output: str):
    lines = [line.strip() for line in output.splitlines() if line.strip()]
    valid = len(lines) == 3 and all(lines[i].startswith(prefix) for i, prefix in enumerate(("BENEFIT:", "LIMITATION:", "CHECK:")))
    if not valid:
        return decision((1, 0, 0, 1, 2), False, 2, "Required three-line labelled format failed", "No", "No", integrity="Yes")
    lower = output.lower()
    if "quantum circuit" in lower or "bit-for-bit" in lower:
        return decision((3, 9, 5, 7, 9), False, 4, "Materially unsuitable or wrong verification claim", "Yes", "No", unsupported=1)
    minor = "limitation: limitation:" in lower or "check: check:" in lower
    scores = (7, 7, 7, 8, 9) if minor else (8, 9, 8, 8, 9)
    return decision(scores, True, None, "No critical cap", "Yes", "Yes")


def p3(output: str):
    fenced = "```" in output
    try:
        value = json.loads(output)
    except Exception:
        value = None
    keys = {"optimisation", "memory_effect", "quality_risk", "verification"}
    valid = isinstance(value, dict) and set(value) == keys and value.get("optimisation") == "TurboQuant" and all(isinstance(value[k], str) for k in keys - {"optimisation"}) and not fenced
    if not valid:
        if fenced and isinstance(value, dict):
            reason = "Markdown fence violates JSON-only format"
            return decision((7, 4, 8, 8, 9), False, 4, reason, "No", "Partly")
        return decision((2, 1, 2, 3, 3), False, 2 if value is None else 4,
                        "Invalid JSON or wrong exact schema", "No", "No", integrity="Yes" if value is None else "No")
    effect = value["memory_effect"].lower()
    if effect in {"increased", "high"}:
        return decision((2, 10, 7, 8, 10), False, 4, "Materially wrong memory-effect claim", "Yes", "No", unsupported=1)
    if "latency" in effect:
        return decision((5, 10, 8, 8, 10), True, None, "No critical cap", "Yes", "Yes",
                        notes="Schema passes; memory_effect is relevant but describes latency rather than memory footprint.")
    return decision((8, 10, 8, 8, 10), True, None, "No critical cap", "Yes", "Yes")


def p4(output: str):
    sentence_text = output.replace("llama.cpp", "llama_cpp")
    sentences = len([x for x in re.split(r"(?<=[.!?])\s+", sentence_text.strip()) if x])
    checks = {
        "model": "IBM Granite 4.1 3B" in output,
        "runtime": "upstream llama.cpp" in output,
        "weights": "Q4_K_M" in output,
        "caches": "Q8_0" in output and (("both K and V" in output) or ("K and V caches" in output)),
        "context": "4096" in output,
        "device": "local" in output and "Windows Intel" in output,
        "turbo": "TurboQuant" in output and ("not active" in output or "not activated" in output or "without activat" in output),
    }
    passed = sentences == 2 and all(checks.values())
    if len(output) > 700 or "Granite 4.3" in output:
        return decision((1, 1, 1, 1, 1), False, 2, "Severe incoherence and fact corruption", "No", "No", unsupported=3, integrity="Yes")
    if passed:
        return decision((10, 10, 10, 10, 10), True, None, "No critical cap", "Yes", "Yes")
    missing = ",".join(name for name, ok in checks.items() if not ok) or "none"
    return decision((7, 4 if sentences != 2 else 8, 4, 8, 9), False, 4,
                    f"P4 missing/format gate: sentences={sentences}, missing={missing}",
                    "No" if sentences != 2 else "Yes", "No")


def p5(raw: dict):
    if raw["status"] != "complete":
        reason = "No model response: controlled timeout or 16K safety block"
        return decision((0, 0, 0, 0, 0), False, 0, reason, "No", "No", integrity="No response")
    if raw["output"].strip() == "MARKER:IXN-TQ-7319":
        return decision((10, 10, 10, 10, 10), True, None, "No critical cap", "Yes", "Yes")
    if raw["output"].strip() == "MARKER: IXN-TQ-7319":
        return decision((10, 4, 10, 10, 10), False, 4, "Exact-output failure: prohibited space after colon", "No", "Yes")
    return decision((0, 2, 0, 2, 5), False, 2, "Wrong long-context marker", "No", "No")


def p6(raw: dict):
    first, second = raw.get("turn_1", "").strip(), raw.get("output", "").strip()
    if first == "SAVED" and second == "amber:4821":
        return decision((10, 10, 10, 10, 10), True, None, "No critical cap", "Yes", "Yes")
    if first == "SAVED" and second == "4821":
        return decision((4, 4, 4, 8, 2), False, 2, "Wrong remembered value: missing amber prefix", "No", "No", integrity="Yes")
    return decision((0, 0, 0, 0, 0), False, 0, "Multi-turn response unusable", "No", "No", integrity="Yes")


def main():
    ap = argparse.ArgumentParser(); ap.add_argument("--raw-root", type=Path, required=True); ap.add_argument("--output", type=Path, required=True)
    args = ap.parse_args(); functions = {"P1": p1, "P2": p2, "P3": p3, "P4": p4}
    results = {}
    for row in sorted(p for p in args.raw_root.iterdir() if p.is_dir()):
        for n in range(1, 7):
            pid = f"P{n}"; raw = json.loads((row / f"{pid}.json").read_text(encoding="utf-8")); key = f"{pid}:{raw['output_sha256']}"
            adjudication = p5(raw) if pid == "P5" else p6(raw) if pid == "P6" else functions[pid](raw["output"])
            if key in results and results[key] != adjudication:
                raise RuntimeError(f"content-keyed adjudication mismatch: {key}")
            results[key] = adjudication
    args.output.write_text(json.dumps(results, indent=2), encoding="utf-8")
    print(f"wrote {len(results)} unique content-keyed adjudications")


if __name__ == "__main__": main()
