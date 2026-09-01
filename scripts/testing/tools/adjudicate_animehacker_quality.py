"""Apply the frozen harsh rubric to WB-03 response evidence."""

from __future__ import annotations

import argparse
import json
import statistics
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.tools.adjudicate_atomicbot_quality import p1, p2, p3, p4, p5, p6
from scripts.testing.campaigns.atomicbot.quality import score_response


FUNCTIONS = {"P1": p1, "P2": p2, "P3": p3, "P4": p4}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--raw-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    rows = {}
    for row in sorted(path for path in args.raw_root.iterdir() if path.is_dir()):
        if not (row / "complete.json").is_file():
            continue
        prompts = {}
        for number in range(1, 7):
            prompt_id = f"P{number}"
            raw = json.loads((row / f"{prompt_id}.json").read_text(encoding="utf-8"))
            adjudication = (p5(raw) if prompt_id == "P5" else p6(raw) if prompt_id == "P6"
                            else FUNCTIONS[prompt_id](raw["output"]))
            score = score_response(prompt_id, raw["output"], adjudication)
            prompts[prompt_id] = {"raw": raw, "adjudication": adjudication,
                                  "uncapped_score": score.uncapped_score,
                                  "final_score": score.final_score}
        values = [item["final_score"] for item in prompts.values()]
        rows[row.name] = {"prompts": prompts, "mean_score": statistics.fmean(values),
                          "median_score": statistics.median(values),
                          "minimum_score": min(values), "maximum_score": max(values)}
    args.output.write_text(json.dumps(rows, indent=2, sort_keys=True), encoding="utf-8")
    print(json.dumps({key: round(value["mean_score"], 4) for key, value in rows.items()}, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
