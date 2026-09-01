from __future__ import annotations

import importlib
import subprocess
import sys
from pathlib import Path

import pytest


REPOSITORY_ROOT = Path(__file__).resolve().parents[4]


def run_module(module: str, *args: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, "-m", module, *args],
        cwd=REPOSITORY_ROOT,
        capture_output=True,
        text=True,
        check=False,
    )


@pytest.mark.parametrize(
    "module",
    [
        "run_llama",
        "run_atomicbot",
        "run_animehacker",
        "run_openvino",
        "build_results",
        "validate_results",
    ],
)
def test_supported_cli_help_is_side_effect_free(module: str) -> None:
    result = run_module(f"scripts.testing.cli.{module}", "--help")

    assert result.returncode == 0
    assert result.stderr == ""


@pytest.mark.parametrize(
    ("module_name", "expected_modes"),
    [
        ("run_llama", ("measure-run", "measure-server")),
        ("run_atomicbot", ("retest", "server-metrics", "full-quality")),
        ("run_animehacker", ("retest", "quality", "large-host")),
        (
            "run_openvino",
            (
                "retest",
                "quality",
                "reference-capability",
                "diagnostics",
                "adaptive-quality",
                "adaptive-comparison",
                "format-boundary",
            ),
        ),
    ],
)
def test_route_cli_help_lists_documented_modes(
    module_name: str,
    expected_modes: tuple[str, ...],
) -> None:
    result = run_module(f"scripts.testing.cli.{module_name}", "--help")

    assert result.returncode == 0
    assert "--mode" in result.stdout
    for mode in expected_modes:
        assert mode in result.stdout


def test_build_results_help_lists_documented_routes() -> None:
    result = run_module("scripts.testing.cli.build_results", "--help")

    assert result.returncode == 0
    assert "--route" in result.stdout
    assert "--validate-only" in result.stdout
    for route in (
        "all",
        "openvino",
        "experimental-openvino",
        "official-openvino",
        "upstream-llama",
        "atomicbot",
        "animehacker",
        "cross-route",
    ):
        assert route in result.stdout


def test_validate_results_help_reuses_build_routes_without_exposing_toggle() -> None:
    result = run_module("scripts.testing.cli.validate_results", "--help")

    assert result.returncode == 0
    assert "--route" in result.stdout
    assert "--validate-only" not in result.stdout
    for route in (
        "all",
        "openvino",
        "experimental-openvino",
        "official-openvino",
        "upstream-llama",
        "atomicbot",
        "animehacker",
        "cross-route",
    ):
        assert route in result.stdout


def test_route_cli_missing_mode_exits_2() -> None:
    result = run_module("scripts.testing.cli.run_atomicbot")

    assert result.returncode == 2
    assert "usage:" in result.stderr.casefold()
    assert "--mode" in result.stderr


def test_build_results_invalid_route_exits_2(tmp_path: Path) -> None:
    result = run_module(
        "scripts.testing.cli.build_results",
        "--route",
        "not-a-route",
        "--output-root",
        str(tmp_path),
        "--validate-only",
    )

    assert result.returncode == 2
    assert "usage:" in result.stderr.casefold()
    assert "invalid choice" in result.stderr.casefold()


@pytest.mark.parametrize("module_name", ["build_results", "validate_results"])
def test_validation_failure_exits_1_without_creating_receipts(
    module_name: str,
    tmp_path: Path,
) -> None:
    invalid_root = tmp_path / "invalid-final-results"
    invalid_root.mkdir()

    arguments = [
        "--route",
        "all",
        "--output-root",
        str(invalid_root),
    ]
    if module_name == "build_results":
        arguments.append("--validate-only")

    result = run_module(f"scripts.testing.cli.{module_name}", *arguments)

    assert result.returncode == 1
    assert not (invalid_root / "validation").exists()


@pytest.mark.parametrize(
    ("module_name", "selected_mode", "expected_handler", "handler_names"),
    [
        (
            "run_llama",
            "measure-server",
            "measure_server_main",
            ("measure_run_main", "measure_server_main"),
        ),
        (
            "run_atomicbot",
            "server-metrics",
            "run_atomicbot_server_metrics_main",
            (
                "run_atomicbot_retest_main",
                "run_atomicbot_server_metrics_main",
                "run_atomicbot_full_quality_main",
            ),
        ),
        (
            "run_animehacker",
            "quality",
            "run_animehacker_quality_main",
            (
                "run_animehacker_retest_main",
                "run_animehacker_quality_main",
                "run_animehacker_large_host_main",
            ),
        ),
        (
            "run_openvino",
            "adaptive-quality",
            "run_official_openvino_adaptive_quality_main",
            (
                "run_official_openvino_retest_main",
                "run_official_openvino_quality_main",
                "run_openvino_reference_capability_main",
                "run_official_openvino_diagnostics_main",
                "run_official_openvino_adaptive_quality_main",
                "run_official_openvino_adaptive_comparison_main",
                "run_official_openvino_format_boundary_main",
            ),
        ),
    ],
)
def test_route_cli_dispatches_exactly_one_existing_command(
    module_name: str,
    selected_mode: str,
    expected_handler: str,
    handler_names: tuple[str, ...],
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    module = importlib.import_module(f"scripts.testing.cli.{module_name}")
    calls: list[tuple[str, list[str]]] = []

    for handler_name in handler_names:
        def fake_main(name: str = handler_name) -> int:
            calls.append((name, sys.argv[1:]))
            return 0

        monkeypatch.setattr(module, handler_name, fake_main)

    exit_code = module.main(["--mode", selected_mode, "--", "--flag", "value"])

    assert exit_code == 0
    assert calls == [(expected_handler, ["--flag", "value"])]


def test_validate_results_delegates_to_build_results_with_validate_only(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    module = importlib.import_module("scripts.testing.cli.validate_results")
    forwarded: list[list[str]] = []

    def fake_main(argv: list[str] | None = None) -> int:
        forwarded.append(list(argv or ()))
        return 7

    monkeypatch.setattr(module.build_results, "main", fake_main)

    result = module.main(["--route", "all", "--output-root", "docs/testing/final-results"])

    assert result == 7
    assert len(forwarded) == 1
    assert forwarded[0][:3] == ["--route", "all", "--output-root"]
    assert Path(forwarded[0][3]) == Path("docs/testing/final-results")
    assert forwarded[0][4:] == ["--validate-only"]
