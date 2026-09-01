from __future__ import annotations

import shutil
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[4]
TOOLS_ROOT = ROOT / "scripts" / "testing" / "tools"


def _powershell_executable() -> str:
    powershell = shutil.which("powershell.exe") or shutil.which("powershell")
    assert powershell is not None
    return powershell


def _ps_quote(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def _extract_snippet(script_path: Path, start_marker: str, end_marker: str) -> str:
    source = script_path.read_text(encoding="utf-8")
    start = source.index(start_marker)
    end = source.index(end_marker, start)
    return source[start:end]


def _evaluate_script_snippet(
    tmp_path: Path,
    *,
    script_path: Path,
    start_marker: str,
    end_marker: str,
    output_variables: tuple[str, ...],
    extra_setup: str = "",
) -> dict[str, str]:
    snippet = _extract_snippet(script_path, start_marker, end_marker).replace(
        "$PSScriptRoot",
        _ps_quote(str(script_path.parent)),
    )
    harness = tmp_path / f"{script_path.stem}.snippet.ps1"
    lines = []
    if extra_setup:
        lines.append(extra_setup)
    lines.append(snippet)
    for name in output_variables:
        lines.append(f'Write-Output ("{name}=" + ${name})')
    harness.write_text("\n".join(lines), encoding="utf-8")

    completed = subprocess.run(
        [
            _powershell_executable(),
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            str(harness),
        ],
        capture_output=True,
        text=True,
        check=False,
    )
    assert completed.returncode == 0, completed.stdout + completed.stderr
    observed: dict[str, str] = {}
    for line in completed.stdout.splitlines():
        if "=" not in line:
            continue
        name, value = line.split("=", 1)
        observed[name] = value
    return observed


def test_run_openvino_reference_capability_supports_direct_help_execution() -> None:
    script = TOOLS_ROOT / "run_openvino_reference_capability.py"

    completed = subprocess.run(
        [sys.executable, str(script), "--help"],
        capture_output=True,
        text=True,
        check=False,
    )

    assert completed.returncode == 0, completed.stdout + completed.stderr
    assert "usage:" in completed.stdout.lower()


def test_acquire_official_openvino_defaults_stay_anchored_to_repository_root(
    tmp_path: Path,
) -> None:
    script = TOOLS_ROOT / "acquire_official_openvino.ps1"

    observed = _evaluate_script_snippet(
        tmp_path,
        script_path=script,
        start_marker="$repoRoot =",
        end_marker='New-Item -ItemType Directory -Force -Path $sourceRoot, $acquisitionRoot, $environmentRoot | Out-Null',
        output_variables=("repoRoot", "sourceRoot", "evidenceRoot"),
        extra_setup=(
            "$CampaignDate = '2026-07-19'\n"
            "$OpenVINOTag = '2026.2.1'"
        ),
    )

    assert Path(observed["repoRoot"]).resolve() == ROOT.resolve()
    assert Path(observed["sourceRoot"]).resolve() == (
        ROOT / "external/official-openvino/2026-07-19"
    ).resolve()
    assert Path(observed["evidenceRoot"]).resolve() == (
        ROOT / "experiments/raw-results/official-openvino/2026-07-19"
    ).resolve()


def test_build_and_invoke_wrappers_resolve_repository_root_from_tools_directory(
    tmp_path: Path,
) -> None:
    build_script = TOOLS_ROOT / "build_openvino_turboquant.ps1"
    build = _evaluate_script_snippet(
        tmp_path,
        script_path=build_script,
        start_marker="$repoRoot = [IO.Path]::GetFullPath(",
        end_marker="$resolvedPythonExecutable = if",
        output_variables=("repoRoot",),
    )
    assert Path(build["repoRoot"]).resolve() == ROOT.resolve()

    invoke_script = TOOLS_ROOT / "invoke_guarded_command.ps1"
    invoke = _evaluate_script_snippet(
        tmp_path,
        script_path=invoke_script,
        start_marker="$controllerRepositoryRoot = [IO.Path]::GetFullPath(",
        end_marker="if (-not [IO.File]::Exists($controllerModulePath)) {",
        output_variables=("controllerRepositoryRoot", "controllerModulePath"),
        extra_setup=(
            "$joinPath = { param($Path, $ChildPath) Join-Path $Path $ChildPath }"
        ),
    )
    expected_controller = (
        ROOT / "scripts" / "testing" / "campaigns" / "openvino" / "guarded_build.py"
    )
    assert Path(invoke["controllerRepositoryRoot"]).resolve() == ROOT.resolve()
    assert Path(invoke["controllerModulePath"]).resolve() == expected_controller.resolve()
    assert expected_controller.is_file()


def test_prepare_patch_wrappers_resolve_repository_root_from_tools_directory(
    tmp_path: Path,
) -> None:
    expectations = {
        "prepare_openvino_cpu_observer_patch.ps1": ROOT,
        "prepare_openvino_turboquant_patch.ps1": ROOT,
    }

    for script_name, expected_root in expectations.items():
        observed = _evaluate_script_snippet(
            tmp_path,
            script_path=TOOLS_ROOT / script_name,
            start_marker="$repoRoot =",
            end_marker="if (-not $UpstreamPath) {",
            output_variables=("repoRoot",),
        )
        assert Path(observed["repoRoot"]).resolve() == expected_root.resolve()
