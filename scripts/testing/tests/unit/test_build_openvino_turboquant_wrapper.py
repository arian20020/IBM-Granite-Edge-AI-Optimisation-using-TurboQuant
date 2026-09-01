import shutil
import subprocess
import sys
from pathlib import Path


WRAPPER = (
    Path(__file__).parents[2]
    / "tools"
    / "build_openvino_turboquant.ps1"
)


def powershell_executable() -> str:
    powershell = shutil.which("powershell.exe") or shutil.which("powershell")
    assert powershell is not None
    return powershell


def test_wrapper_has_identity_bound_guarded_build_contract():
    script = WRAPPER.read_text(encoding="utf-8")

    for parameter in (
        "SourcePath",
        "IdentityPath",
        "OpenVINODir",
        "BuildPath",
        "Parallelism",
        "EvidenceRoot",
        "MinimumAvailableRamMiB",
        "EnablePython",
    ):
        assert f"${parameter}" in script
    for option in (
        "-DENABLE_TESTS=ON",
        "-DENABLE_SAMPLES=OFF",
        "-DENABLE_TOOLS=OFF",
        "-DENABLE_PYTHON=$pythonOption",
        "-DPython3_EXECUTABLE=$resolvedPythonExecutable",
        "-DENABLE_JS=OFF",
    ):
        assert option in script
    for target in (
        "openvino_genai_obj",
        "turboquant_codec_tests",
        "turboquant_config_tests",
        "turboquant_state_update_decode_tests",
        "turboquant_stateful_graph_tests",
        "turboquant_pipeline_activation_tests",
        "py_openvino_genai",
    ):
        assert target in script

    assert "scripts.testing.tools.verify_openvino_turboquant_build" in script
    assert "scripts.testing.campaigns.openvino.guarded_build" in script
    assert "CMakeCache.txt" in script
    assert "identity_sha256" in script
    assert "patch_commit" in script
    assert "derived_tree" in script
    assert "patches" in script
    assert "cache_sha256" in script
    assert "outputs" in script
    assert "sha256" in script
    assert "'.pyd'" in script
    assert "Python build produced no py_openvino_genai module" in script
    assert "configured Python executable does not match" in script
    assert "openvino-turboquant-build-provenance/v1" in script


def test_wrapper_is_valid_powershell_and_rejects_parallelism_above_one(tmp_path: Path):
    powershell = powershell_executable()
    escaped_wrapper = str(WRAPPER).replace("'", "''")

    parse = subprocess.run(
        [
            powershell,
            "-NoProfile",
            "-NonInteractive",
            "-Command",
            (
                "$tokens=$null;$errors=$null;"
                "[Management.Automation.Language.Parser]::ParseFile("
                f"'{escaped_wrapper}',"
                "[ref]$tokens,[ref]$errors)|Out-Null;"
                "if($errors.Count){$errors|ForEach-Object{$_.Message};exit 1}"
            ),
        ],
        capture_output=True,
        text=True,
    )
    assert parse.returncode == 0, parse.stdout + parse.stderr

    rejected = subprocess.run(
        [
            powershell,
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            str(WRAPPER),
            "-SourcePath",
            str(tmp_path / "source"),
            "-IdentityPath",
            str(tmp_path / "identity.json"),
            "-OpenVINODir",
            str(tmp_path / "openvino"),
            "-BuildPath",
            str(tmp_path / "build"),
            "-Parallelism",
            "2",
            "-EvidenceRoot",
            str(tmp_path / "evidence"),
        ],
        capture_output=True,
        text=True,
    )
    assert rejected.returncode != 0
    assert "Parallelism" in rejected.stdout + rejected.stderr


def test_wrapper_rejects_an_existing_cache_for_another_source(tmp_path: Path):
    source = tmp_path / "source"
    openvino_dir = tmp_path / "openvino"
    build = tmp_path / "build"
    evidence = tmp_path / "evidence"
    foreign_source = tmp_path / "foreign-source"
    for directory in (source, openvino_dir, build, foreign_source):
        directory.mkdir()
    identity = tmp_path / "source.identity.json"
    identity.write_text("{}", encoding="utf-8")
    (build / "CMakeCache.txt").write_text(
        f"CMAKE_HOME_DIRECTORY:INTERNAL={foreign_source}\n"
        f"OpenVINO_DIR:PATH={openvino_dir}\n"
        "ENABLE_TESTS:BOOL=ON\n"
        "ENABLE_SAMPLES:BOOL=OFF\n"
        "ENABLE_TOOLS:BOOL=OFF\n"
        "ENABLE_PYTHON:BOOL=OFF\n"
        "ENABLE_JS:BOOL=OFF\n",
        encoding="utf-8",
    )

    result = subprocess.run(
        [
            powershell_executable(),
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            str(WRAPPER),
            "-SourcePath",
            str(source),
            "-IdentityPath",
            str(identity),
            "-OpenVINODir",
            str(openvino_dir),
            "-BuildPath",
            str(build),
            "-Parallelism",
            "1",
            "-EvidenceRoot",
            str(evidence),
        ],
        capture_output=True,
        text=True,
    )

    assert result.returncode != 0
    assert "different source directory" in result.stdout + result.stderr


def test_python_build_rejects_cache_bound_to_another_interpreter(tmp_path: Path):
    source = tmp_path / "source"
    openvino_dir = tmp_path / "openvino"
    build = tmp_path / "build"
    evidence = tmp_path / "evidence"
    wrong_python = tmp_path / "wrong-python.exe"
    for directory in (source, openvino_dir, build):
        directory.mkdir()
    wrong_python.write_bytes(b"not the selected interpreter")
    identity = tmp_path / "source.identity.json"
    identity.write_text("{}", encoding="utf-8")
    (build / "CMakeCache.txt").write_text(
        f"CMAKE_HOME_DIRECTORY:INTERNAL={source}\n"
        f"OpenVINO_DIR:PATH={openvino_dir}\n"
        "ENABLE_TESTS:BOOL=ON\n"
        "ENABLE_SAMPLES:BOOL=OFF\n"
        "ENABLE_TOOLS:BOOL=OFF\n"
        "ENABLE_PYTHON:BOOL=ON\n"
        "ENABLE_JS:BOOL=OFF\n"
        f"Python3_EXECUTABLE:FILEPATH={wrong_python}\n"
        f"_Python3_EXECUTABLE:INTERNAL={wrong_python}\n",
        encoding="utf-8",
    )

    result = subprocess.run(
        [
            powershell_executable(),
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            str(WRAPPER),
            "-SourcePath",
            str(source),
            "-IdentityPath",
            str(identity),
            "-OpenVINODir",
            str(openvino_dir),
            "-BuildPath",
            str(build),
            "-Parallelism",
            "1",
            "-EvidenceRoot",
            str(evidence),
            "-PythonExecutable",
            sys.executable,
            "-EnablePython",
        ],
        capture_output=True,
        text=True,
    )

    assert result.returncode != 0
    assert (
        "configured Python executable does not match"
        in result.stdout + result.stderr
    )
