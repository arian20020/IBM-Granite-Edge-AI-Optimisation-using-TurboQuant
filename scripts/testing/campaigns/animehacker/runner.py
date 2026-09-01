"""Command construction, selection, and unique identifiers for WB-03 runs."""

from __future__ import annotations

from pathlib import Path

from scripts.testing.campaigns.animehacker.matrix import TestCase


def build_server_command(case: TestCase, server: Path, model: Path, port: int) -> list[str]:
    gpu_layers = 0 if case.backend == "cpu" else 1
    command = [
        str(server), "-m", str(model), "-c", str(case.context), "-t", "8", "-tb", "8",
        "-b", "512", "-ub", "512", "-ctk", case.cache, "-ctv", case.cache,
        "-ngl", str(gpu_layers), "--host", "127.0.0.1", "--port", str(port),
        "-np", "1", "--cache-ram", "0", "--fit", "off", "--offline",
        "--no-webui", "--log-colors", "off", "-lv", "4",
    ]
    if case.backend == "sycl-partial":
        if case.cache == "tq3_0":
            command.extend(("-sm", "none", "-mg", "0"))
        else:
            command.extend(("-fa", "off"))
    return command


def format_runtime_prompt(case: TestCase, prompt: str) -> str:
    if case.backend == "sycl-partial" and case.cache == "tq3_0":
        return ("<|start_of_role|>user<|end_of_role|>" + prompt +
                "<|end_of_text|>\n<|start_of_role|>assistant<|end_of_role|>")
    return prompt


def select_cases(cases: list[TestCase], *, only: set[str] | None = None,
                 start_at: str | None = None, skip: set[str] | None = None,
                 resume_state: dict | None = None) -> list[TestCase]:
    known = {case.test_id for case in cases}
    requested = (only or set()) | (skip or set()) | ({start_at} if start_at else set())
    unknown = requested - known
    if unknown:
        raise ValueError(f"unknown test id: {sorted(unknown)[0]}")
    start_index = next((i for i, item in enumerate(cases) if item.test_id == start_at), 0)
    attempts = (resume_state or {}).get("attempts", {})
    selected = []
    for item in cases[start_index:]:
        if only is not None and item.test_id not in only:
            continue
        if item.test_id in (skip or set()):
            continue
        prior = attempts.get(item.test_id, {})
        if prior.get("status") == "complete" and prior.get("reconciled") is True:
            continue
        selected.append(item)
    return selected


def next_run_id(test_id: str, date: str, root: Path) -> str:
    prefix = f"{test_id}-{date}-R"
    numbers = []
    if root.exists():
        for path in root.iterdir():
            suffix = path.name.removeprefix(prefix)
            if path.name.startswith(prefix) and suffix.isdigit():
                numbers.append(int(suffix))
    return f"{prefix}{max(numbers, default=0) + 1:04d}"
