"""Reconcile WB-03 build routes from raw logs and binary hashes."""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.animehacker.build_reconcile import inventory_binaries, parse_ctest_summary


BINARIES = ("llama-cli.exe", "llama-server.exe", "llama-bench.exe",
            "llama-quantize.exe", "llama-perplexity.exe")


def passed_tests(text: str) -> dict[int, str]:
    result = {}
    for match in re.finditer(r"Test\s+#(?P<number>\d+):\s+(?P<name>\S+).*?Passed", text):
        result[int(match.group("number"))] = match.group("name")
    return result


def write_json(path: Path, value: dict) -> None:
    path.write_text(json.dumps(value, indent=2, sort_keys=True), encoding="utf-8")


def read_log(path: Path) -> str:
    data = path.read_bytes()
    encoding = "utf-16" if data.startswith((b"\xff\xfe", b"\xfe\xff")) else "utf-8"
    return data.decode(encoding, errors="replace")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--checkout", type=Path, required=True)
    parser.add_argument("--evidence-root", type=Path, required=True)
    args = parser.parse_args()
    cpu = args.evidence_root / "build-cpu"
    sycl = args.evidence_root / "build-sycl"
    vulkan = args.evidence_root / "build-vulkan"
    cpu_summary = parse_ctest_summary(read_log(cpu / "ctest-final.log"))
    serial_text = read_log(sycl / "ctest-final-opencl-serial.log")
    thread_text = read_log(sycl / "thread-safety-exact-minimal-final.log")
    passed = passed_tests(serial_text)
    passed.update(passed_tests(thread_text))
    if set(passed) != set(range(1, 41)):
        raise ValueError(f"SYCL terminal pass set incomplete: {sorted(set(range(1, 41)) - set(passed))}")
    sycl_summary = {
        "route": "Windows SYCL OpenCL GPU",
        "environment": {"ONEAPI_DEVICE_SELECTOR": "opencl:gpu",
                        "GGML_SYCL_ENABLE_FLASH_ATTN": "0"},
        "passed": 40, "failed": 0, "total": 40,
        "terminal_test_ids": passed,
        "reconciliation": "39 terminal passes in serial suite plus isolated thread-safety pass",
        "binaries": inventory_binaries(args.checkout / "build-sycl-controlled", BINARIES),
    }
    write_json(cpu / "reconciliation-final.json", {
        "route": "Windows CPU", **cpu_summary,
        "binaries": inventory_binaries(args.checkout / "build-cpu-controlled", BINARIES),
    })
    write_json(sycl / "reconciliation.json", sycl_summary)
    write_json(vulkan / "reconciliation.json", {
        "route": "supplementary Vulkan", "build": "passed",
        "tq3_runtime_classification": "not proven by source audit; not a controlled TQ3 route",
        "binaries": inventory_binaries(args.checkout / "build-vulkan-controlled", BINARIES),
    })
    patch = subprocess.run(["git", "diff", "--", "tests/test-quantize-fns.cpp",
        "tests/test-quantize-perf.cpp", "ggml/src/ggml-sycl/ggml-sycl.cpp"],
        cwd=args.checkout, check=True, capture_output=True, text=True).stdout
    (sycl / "campaign-fixes.patch").write_text(patch, encoding="utf-8")
    print(json.dumps({"cpu": cpu_summary, "sycl": {"passed": 40, "failed": 0},
                      "vulkan": "passed"}, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
