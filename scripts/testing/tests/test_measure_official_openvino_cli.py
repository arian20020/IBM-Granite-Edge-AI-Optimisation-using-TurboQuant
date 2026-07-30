import json
import os
import subprocess
import sys
from pathlib import Path

import pytest

from scripts.testing.measure_official_openvino import (
    build_worker_environment,
    run_single_measurement,
)

SCRIPT = Path(__file__).parents[1] / "measure_official_openvino.py"


def _fake_build(root: Path) -> Path:
    build = root / "build"
    package = build / "openvino_genai"
    package.mkdir(parents=True)
    (package / "__init__.py").write_text("", encoding="utf-8")
    (package / "py_openvino_genai.cp313-win_amd64.pyd").write_bytes(b"pyd")
    (package / "openvino_genai.dll").write_bytes(b"dll")
    return build


def test_worker_environment_prepends_exact_build_and_runtime_libraries(tmp_path):
    build = _fake_build(tmp_path)
    repo = tmp_path / "repo"
    repo.mkdir()
    python_site_packages = tmp_path / "site-packages"
    python_site_packages.mkdir()
    openvino_libraries = tmp_path / "openvino-libs"
    openvino_libraries.mkdir()

    environment = build_worker_environment(
        build_root=build,
        repo_root=repo,
        python_site_packages=python_site_packages,
        openvino_libraries=openvino_libraries,
        base_environment={"PATH": "existing-path", "PYTHONPATH": "old-python"},
    )

    assert environment["PYTHONPATH"].split(os.pathsep) == [
        str(build.resolve()),
        str(repo.resolve()),
        str(python_site_packages.resolve()),
    ]
    assert environment["PATH"].split(os.pathsep)[:3] == [
        str((build / "openvino_genai").resolve()),
        str(openvino_libraries.resolve()),
        "existing-path",
    ]


def test_worker_environment_rejects_build_without_python_module(tmp_path):
    build = tmp_path / "build"
    package = build / "openvino_genai"
    package.mkdir(parents=True)
    (package / "__init__.py").write_text("", encoding="utf-8")
    (package / "openvino_genai.dll").write_bytes(b"dll")
    repo = tmp_path / "repo"
    repo.mkdir()
    libraries = tmp_path / "libs"
    libraries.mkdir()
    site_packages = tmp_path / "site-packages"
    site_packages.mkdir()

    with pytest.raises(ValueError, match="py_openvino_genai"):
        build_worker_environment(
            build_root=build,
            repo_root=repo,
            python_site_packages=site_packages,
            openvino_libraries=libraries,
            base_environment={},
        )


def test_single_measurement_binds_worker_command_and_returns_guard_record(tmp_path):
    build = _fake_build(tmp_path)
    repo = tmp_path / "repo"
    repo.mkdir()
    libraries = tmp_path / "libs"
    libraries.mkdir()
    site_packages = tmp_path / "site-packages"
    site_packages.mkdir()
    sampler = tmp_path / "sampler.ps1"
    sampler.write_text("# sampler\n", encoding="utf-8")
    spec = tmp_path / "spec.json"
    spec.write_text(
        json.dumps(
            {
                "schema": "official-openvino-wb04-worker-spec/v1",
                "role": "pilot",
            }
        ),
        encoding="utf-8",
    )
    output = tmp_path / "output"
    observed = {}

    def fake_run_process(**kwargs):
        observed.update(kwargs)
        return {
            "role": "pilot",
            "valid": True,
            "cleanup_process_count": 0,
        }

    record = run_single_measurement(
        spec_path=spec,
        output_dir=output,
        role="pilot",
        build_root=build,
        repo_root=repo,
        python_executable=Path(sys.executable),
        python_site_packages=site_packages,
        openvino_libraries=libraries,
        sampler_script=sampler,
        timeout_seconds=123,
        minimum_available_ram_mib=2048,
        run_process=fake_run_process,
    )

    assert record["valid"] is True
    assert observed["role"] == "pilot"
    assert observed["timeout_seconds"] == 123
    assert observed["minimum_available_ram_bytes"] == 2048 * 1024**2
    assert observed["command"] == [
        str(Path(sys.executable).resolve()),
        "-m",
        "scripts.testing.official_openvino.measurement_worker",
        "--spec",
        str(spec.resolve()),
    ]
    assert observed["environment"]["PYTHONPATH"].split(os.pathsep) == [
        str(build.resolve()),
        str(repo.resolve()),
        str(site_packages.resolve()),
    ]


def test_single_measurement_rejects_spec_role_mismatch(tmp_path):
    build = _fake_build(tmp_path)
    repo = tmp_path / "repo"
    repo.mkdir()
    libraries = tmp_path / "libs"
    libraries.mkdir()
    site_packages = tmp_path / "site-packages"
    site_packages.mkdir()
    sampler = tmp_path / "sampler.ps1"
    sampler.write_text("# sampler\n", encoding="utf-8")
    spec = tmp_path / "spec.json"
    spec.write_text(
        json.dumps(
            {
                "schema": "official-openvino-wb04-worker-spec/v1",
                "role": "sample-1",
            }
        ),
        encoding="utf-8",
    )

    with pytest.raises(ValueError, match="role"):
        run_single_measurement(
            spec_path=spec,
            output_dir=tmp_path / "output",
            role="pilot",
            build_root=build,
            repo_root=repo,
            python_executable=Path(sys.executable),
            python_site_packages=site_packages,
            openvino_libraries=libraries,
            sampler_script=sampler,
        )


def test_cli_can_be_invoked_directly_from_repository_root():
    result = subprocess.run(
        [sys.executable, str(SCRIPT), "--help"],
        cwd=SCRIPT.parents[2],
        capture_output=True,
        text=True,
    )

    assert result.returncode == 0, result.stderr
    assert "--build-root" in result.stdout
    assert "--python-site-packages" in result.stdout
    assert "--campaign-root" in result.stdout
    assert "--matrix" in result.stdout
    assert "--artifact-manifest" in result.stdout
    assert "--build-provenance" in result.stdout
