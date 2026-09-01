from __future__ import annotations

import ctypes
import hashlib
import importlib
import importlib.util
import inspect
import json
import os
import py_compile
import shutil
import struct
import subprocess
import sys
import threading
import time
from contextlib import contextmanager
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPT_DIR = ROOT / "scripts" / "testing"
GUARD_TEST_FILE = Path(__file__).resolve()
GIB = 1024 * 1024 * 1024
APPROVED_PYTHON = Path(sys.executable).resolve()
APPROVED_PYTHON_DLL = APPROVED_PYTHON.with_name(
    f"python{sys.version_info.major}{sys.version_info.minor}.dll"
)
APPROVED_PYTHON_SHA256 = hashlib.sha256(
    APPROVED_PYTHON.read_bytes()
).hexdigest()
APPROVED_PYTHON_DLL_SHA256 = hashlib.sha256(
    APPROVED_PYTHON_DLL.read_bytes()
).hexdigest()
WRAPPER_DYNAMIC_COMMAND_VARIABLES = {
    "$assertJsonArray",
    "$assertJsonBoolean",
    "$assertJsonInteger",
    "$assertJsonNull",
    "$assertJsonNumber",
    "$assertJsonObjectShape",
    "$assertJsonString",
    "$assertNoReparseDirectoryAncestry",
    "$assertNotReparsePoint",
    "$bindTrustedCmdlet",
    "$convertFromJson",
    "$convertToJson",
    "$forEachObject",
    "$getAuthenticodeSignature",
    "$getChildItem",
    "$getItem",
    "$getSha256Hex",
    "$joinPath",
    "$newObject",
    "$outNull",
    "$popLocation",
    "$pushLocation",
    "$python",
    "$resolvePath",
    "$setStrictMode",
    "$TrustedRemoveModule",
    "$whereObject",
    "$writeOutput",
}


def _guarded_build():
    return importlib.import_module(
        "scripts.testing.campaigns.openvino.guarded_build"
    )


def _owned_guard():
    return importlib.import_module(
        "scripts.testing.campaigns.openvino.owned_process_guard"
    )


def _limits(
    guard,
    *,
    minimum_available_ram_bytes: int = 0,
    maximum_runtime_seconds: float = 5.0,
):
    return guard.GuardLimits(
        minimum_available_ram_bytes=minimum_available_ram_bytes,
        poll_interval_seconds=0.02,
        cleanup_timeout_seconds=2.0,
        maximum_runtime_seconds=maximum_runtime_seconds,
    )


def _run(
    tmp_path: Path,
    command: list[str],
    *,
    expected_exit: str = "zero",
    limits=None,
    environment=None,
    bound_inputs=None,
):
    guard = _guarded_build()
    return guard.run_guarded_command(
        command,
        cwd=ROOT,
        log_path=tmp_path / "child.log",
        evidence_path=tmp_path / "evidence.json",
        expected_exit=expected_exit,
        limits=limits or _limits(guard),
        environment=environment,
        bound_inputs=bound_inputs,
    )


def _assert_zero_survivors(record: dict[str, object]) -> None:
    job = record["job_object"]
    assert job["setup_ok"] is True
    assert job["query_ok"] is True
    assert job["queried_active_process_count_after_cleanup"] == 0
    assert job["survivor_pids_after_cleanup"] == []


def _assert_memory_evidence(record: dict[str, object]) -> None:
    assert record["memory_sample_count"] >= 1
    assert record["peak_working_set_bytes"] > 0
    assert record["peak_private_bytes"] > 0
    failed_pids = record["memory_query_failed_pids"]
    assert failed_pids == sorted(set(failed_pids))
    configured = record["configured_minimum_available_ram_bytes"]
    observed = record["observed_available_ram_bytes"]
    assert observed["before"] >= configured
    assert observed["minimum"] >= configured
    assert observed["after"] >= configured


def _ps_literal(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def _ps_runtime_arguments(
    *,
    python_executable: Path = APPROVED_PYTHON,
    python_sha256: str = APPROVED_PYTHON_SHA256,
    python_dll_sha256: str = APPROVED_PYTHON_DLL_SHA256,
) -> str:
    return (
        f"-PythonExecutable {_ps_literal(str(python_executable))} "
        f"-PythonSha256 {python_sha256} "
        f"-PythonDllSha256 {python_dll_sha256}"
    )


def _sha256_path(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _flip_pe_text_byte(path: Path) -> None:
    value = bytearray(path.read_bytes())
    pe_offset = struct.unpack_from("<I", value, 0x3C)[0]
    assert value[pe_offset : pe_offset + 4] == b"PE\0\0"
    section_count = struct.unpack_from("<H", value, pe_offset + 6)[0]
    optional_header_size = struct.unpack_from(
        "<H",
        value,
        pe_offset + 20,
    )[0]
    section_table = pe_offset + 24 + optional_header_size
    for index in range(section_count):
        section = section_table + (index * 40)
        name = bytes(value[section : section + 8]).rstrip(b"\0")
        if name != b".text":
            continue
        raw_size = struct.unpack_from("<I", value, section + 16)[0]
        raw_offset = struct.unpack_from("<I", value, section + 20)[0]
        assert raw_size > 32
        mutation_offset = raw_offset + (raw_size // 2)
        value[mutation_offset] ^= 0x01
        path.write_bytes(value)
        return
    raise AssertionError(f"PE .text section not found: {path}")


_KERNEL32 = ctypes.WinDLL("kernel32", use_last_error=True)
_QUERY_DOS_DEVICE = _KERNEL32.QueryDosDeviceW
_QUERY_DOS_DEVICE.argtypes = [
    ctypes.c_wchar_p,
    ctypes.POINTER(ctypes.c_wchar),
    ctypes.c_uint32,
]
_QUERY_DOS_DEVICE.restype = ctypes.c_uint32
_DEFINE_DOS_DEVICE = _KERNEL32.DefineDosDeviceW
_DEFINE_DOS_DEVICE.argtypes = [
    ctypes.c_uint32,
    ctypes.c_wchar_p,
    ctypes.c_wchar_p,
]
_DEFINE_DOS_DEVICE.restype = ctypes.c_int
_DDD_RAW_TARGET_PATH = 0x00000001
_DDD_REMOVE_DEFINITION = 0x00000002
_DDD_EXACT_MATCH_ON_REMOVE = 0x00000004
_DDD_NO_BROADCAST_SYSTEM = 0x00000008
_ERROR_FILE_NOT_FOUND = 2


def _dos_target(root: Path) -> str:
    return "\\??\\" + str(root.resolve())


def _query_dos_device(device: str) -> tuple[str, ...]:
    buffer = ctypes.create_unicode_buffer(32768)
    length = _QUERY_DOS_DEVICE(device, buffer, len(buffer))
    if length == 0:
        error = ctypes.get_last_error()
        if error == _ERROR_FILE_NOT_FOUND:
            return ()
        raise ctypes.WinError(error)
    return tuple(
        value for value in "".join(buffer[:length]).split("\0") if value
    )


def _remove_exact_dos_target(device: str, target: str) -> None:
    observed_targets = _query_dos_device(device)
    assert target in observed_targets
    remove_flags = (
        _DDD_RAW_TARGET_PATH
        | _DDD_REMOVE_DEFINITION
        | _DDD_EXACT_MATCH_ON_REMOVE
        | _DDD_NO_BROADCAST_SYSTEM
    )
    if not _DEFINE_DOS_DEVICE(remove_flags, device, target):
        raise ctypes.WinError(ctypes.get_last_error())
    assert target not in _query_dos_device(device)


@contextmanager
def _owned_subst(root: Path, preferred_letter: str | None = None):
    target = _dos_target(root)
    define_flags = _DDD_RAW_TARGET_PATH | _DDD_NO_BROADCAST_SYSTEM
    letters = (
        (preferred_letter,)
        if preferred_letter is not None
        else tuple(reversed("DEFGHIJKLMNOPQRSTUVWXYZ"))
    )
    for letter in letters:
        device = f"{letter}:"
        if _query_dos_device(device):
            continue
        if not _DEFINE_DOS_DEVICE(define_flags, device, target):
            continue
        try:
            observed_targets = _query_dos_device(device)
            assert observed_targets == (target,)
            yield letter
        finally:
            observed_targets = _query_dos_device(device)
            assert target in observed_targets
            _remove_exact_dos_target(device, target)
            remaining_targets = _query_dos_device(device)
            assert remaining_targets == ()
        return
    raise AssertionError("could not allocate an unused drive letter for subst")


def _write_controller_hook(directory: Path) -> None:
    directory.mkdir()
    (directory / "sitecustomize.py").write_text(
        "\n".join(
            [
                "import atexit",
                "import builtins",
                "import ctypes",
                "import hashlib",
                "import json",
                "import os",
                "import pathlib",
                "import shutil",
                "import sys",
                "",
                "_original_print = builtins.print",
                "",
                "def _controller_loaded():",
                "    main = sys.modules.get('__main__')",
                "    spec = getattr(main, '__spec__', None)",
                "    return getattr(spec, 'name', None) == (",
                "        'scripts.testing.campaigns.openvino.guarded_build'",
                "    )",
                "",
                "def _rewrite_record_and_bind_hash(value, mode):",
                "    evidence_path = pathlib.Path(value['evidence_path'])",
                "    record = json.loads(",
                "        evidence_path.read_text(encoding='utf-8')",
                "    )",
                "    if mode == 'record_schema_array':",
                "        record['schema'] = [record['schema']]",
                "    elif mode == 'record_expected_exit_array':",
                "        record['expected_exit'] = [record['expected_exit']]",
                "    elif mode == 'record_scalar_command':",
                "        record['command'] = record['command'][0]",
                "    elif mode == 'record_maximum_runtime_string':",
                "        record['maximum_runtime_seconds'] = str(",
                "            record['maximum_runtime_seconds']",
                "        )",
                "    elif mode == 'record_created_suspended_array':",
                "        record['launch_governance']['created_suspended'] = [",
                "            record['launch_governance']['created_suspended']",
                "        ]",
                "    elif mode == 'record_observed_ram_array':",
                "        record['observed_available_ram_bytes'] = [",
                "            record['observed_available_ram_bytes']",
                "        ]",
                "    elif mode == 'record_memory_query_failed_pid':",
                "        record['memory_query_failed_pids'] = [",
                "            record['root_pid']",
                "        ]",
                "    else:",
                "        raise AssertionError(f'unhandled record mutation: {mode}')",
                "    encoded = (",
                "        json.dumps(",
                "            record,",
                "            indent=2,",
                "            sort_keys=True,",
                "            allow_nan=False,",
                "        )",
                "        + '\\n'",
                "    ).encode('utf-8')",
                "    temporary = evidence_path.with_name(",
                "        evidence_path.name + '.hook.tmp'",
                "    )",
                "    temporary.write_bytes(encoded)",
                "    os.replace(temporary, evidence_path)",
                "    value['evidence_sha256'] = hashlib.sha256(encoded).hexdigest()",
                "    return value",
                "",
                "def _controlled_print(*values, **kwargs):",
                "    mode = os.environ.get('OV_GUARD_TEST_STDOUT_MODE')",
                "    if not mode or not _controller_loaded():",
                "        return _original_print(*values, **kwargs)",
                "    text = kwargs.get('sep', ' ').join(map(str, values))",
                "    if mode == 'empty':",
                "        return None",
                "    if mode == 'malformed':",
                "        return _original_print('{malformed')",
                "    if mode == 'multiple':",
                "        _original_print(text)",
                "        return _original_print(text)",
                "    if mode == 'tamper_run_id':",
                "        value = json.loads(text)",
                "        value['run_id'] = '0' * 64",
                "        return _original_print(",
                "            json.dumps(value, sort_keys=True)",
                "        )",
                "    if mode == 'tamper_evidence_hash':",
                "        value = json.loads(text)",
                "        value['evidence_sha256'] = '0' * 64",
                "        return _original_print(",
                "            json.dumps(value, sort_keys=True)",
                "        )",
                "    if mode == 'tamper_evidence_path':",
                "        value = json.loads(text)",
                "        source = pathlib.Path(value['evidence_path'])",
                "        alternate = source.with_name(",
                "            source.stem + '.alternate.json'",
                "        )",
                "        shutil.copyfile(source, alternate)",
                "        value['evidence_path'] = str(alternate.resolve())",
                "        value['evidence_sha256'] = hashlib.sha256(",
                "            alternate.read_bytes()",
                "        ).hexdigest()",
                "        return _original_print(",
                "            json.dumps(value, sort_keys=True)",
                "        )",
                "    if mode.startswith('record_'):",
                "        value = _rewrite_record_and_bind_hash(",
                "            json.loads(text), mode",
                "        )",
                "        return _original_print(",
                "            json.dumps(value, sort_keys=True)",
                "        )",
                "    return _original_print(*values, **kwargs)",
                "",
                "builtins.print = _controlled_print",
                "",
                "def _retarget_at_controller_exit():",
                "    if not _controller_loaded():",
                "        return",
                "    device = os.environ.get('OV_GUARD_TEST_RETARGET_DEVICE')",
                "    target = os.environ.get('OV_GUARD_TEST_RETARGET_TARGET')",
                "    if not device or not target:",
                "        return",
                "    kernel32 = ctypes.WinDLL('kernel32', use_last_error=True)",
                "    define = kernel32.DefineDosDeviceW",
                "    define.argtypes = [",
                "        ctypes.c_uint32,",
                "        ctypes.c_wchar_p,",
                "        ctypes.c_wchar_p,",
                "    ]",
                "    define.restype = ctypes.c_int",
                "    if not define(0x00000009, device, target):",
                "        raise ctypes.WinError(ctypes.get_last_error())",
                "",
                "atexit.register(_retarget_at_controller_exit)",
                "",
            ]
        ),
        encoding="utf-8",
    )


def _write_wrapper_driver(
    driver: Path,
    *,
    wrapper: Path,
    label: str,
    working_directory: Path,
    evidence_root: Path,
    program: str,
    command: list[str] | None = None,
    python_executable: Path = APPROVED_PYTHON,
    python_sha256: str = APPROVED_PYTHON_SHA256,
    python_dll_sha256: str = APPROVED_PYTHON_DLL_SHA256,
    minimum_available_ram_mib: int = 2048,
    verification_output_path: Path | None = None,
) -> None:
    child_command = command or [sys.executable, "-c", program]
    child_command_lines = ["$childCommand = @("]
    for index, argument in enumerate(child_command):
        suffix = "," if index + 1 < len(child_command) else ""
        child_command_lines.append(f"  {_ps_literal(argument)}{suffix}")
    child_command_lines.append(")")
    runtime_arguments = _ps_runtime_arguments(
        python_executable=python_executable,
        python_sha256=python_sha256,
        python_dll_sha256=python_dll_sha256,
    )
    verification_argument = (
        " -VerificationOutputPath "
        f"{_ps_literal(str(verification_output_path))}"
        if verification_output_path is not None
        else ""
    )
    driver.write_text(
        "\n".join(
            [
                "$ErrorActionPreference = 'Stop'",
                *child_command_lines,
                (
                    f"& {_ps_literal(str(wrapper))} "
                    f"-Label {_ps_literal(label)} "
                    "-WorkingDirectory "
                    f"{_ps_literal(str(working_directory))} "
                    "-EvidenceRoot "
                    f"{_ps_literal(str(evidence_root))} "
                    "-ExpectedExit Zero -TimeoutSeconds 5 "
                    f"-MinimumAvailableRamMiB {minimum_available_ram_mib} "
                    f"{runtime_arguments} "
                    f"-Command $childCommand{verification_argument}"
                ),
            ]
        )
        + "\n",
        encoding="utf-8",
    )


def _invoke_wrapper_driver(driver: Path, *, environment=None, cwd=ROOT):
    return subprocess.run(
        [
            "powershell.exe",
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            str(driver),
        ],
        cwd=cwd,
        env=environment,
        text=True,
        capture_output=True,
        timeout=20,
    )


def _powershell_command_asts(script: Path) -> list[dict[str, object]]:
    parser_script = "\n".join(
        [
            "$tokens = $null",
            "$parseErrors = $null",
            (
                "$ast = "
                "[Management.Automation.Language.Parser]::ParseFile("
                f"{_ps_literal(str(script))}, "
                "[ref]$tokens, [ref]$parseErrors)"
            ),
            "if ($parseErrors.Count -ne 0) {",
            "  throw ($parseErrors | "
            "Microsoft.PowerShell.Core\\ForEach-Object { $_.Message })",
            "}",
            "$commands = @(",
            "  $ast.FindAll(",
            "    {",
            "      param($node)",
            (
                "      $node -is "
                "[Management.Automation.Language.CommandAst]"
            ),
            "    },",
            "    $true",
            "  ) | Microsoft.PowerShell.Core\\ForEach-Object {",
            "    [PSCustomObject]@{",
            "      name = $_.GetCommandName()",
            "      text = $_.Extent.Text",
            "      line = $_.Extent.StartLineNumber",
            "      start = $_.Extent.StartOffset",
            "    }",
            "  }",
            ")",
            (
                "$commands | "
                "Microsoft.PowerShell.Utility\\ConvertTo-Json "
                "-Depth 3 -Compress"
            ),
        ]
    )
    completed = subprocess.run(
        [
            "powershell.exe",
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-Command",
            parser_script,
        ],
        cwd=ROOT,
        text=True,
        capture_output=True,
        timeout=10,
    )
    assert completed.returncode == 0, completed.stderr
    commands = json.loads(completed.stdout)
    assert isinstance(commands, list)
    return commands


def _copy_guard_controller_package(
    destination_root: Path,
    *,
    publication_hook: bool = False,
    controller_hook: Path | None = None,
) -> None:
    source = ROOT / "scripts" / "testing" / "campaigns" / "openvino"
    destination = (
        destination_root / "scripts" / "testing" / "campaigns" / "openvino"
    )
    shutil.copytree(
        source,
        destination,
        ignore=shutil.ignore_patterns("__pycache__", "*.pyc"),
    )
    wrapper_source = (
        ROOT / "scripts" / "testing" / "tools" / "invoke_guarded_command.ps1"
    )
    wrapper_destination = (
        destination_root / "scripts" / "testing" / "tools"
    )
    wrapper_destination.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(
        wrapper_source,
        wrapper_destination / "invoke_guarded_command.ps1",
    )
    if not publication_hook and controller_hook is None:
        return
    controller = destination / "guarded_build.py"
    controller_source = controller.read_text(encoding="utf-8")
    main_marker = '\nif __name__ == "__main__":'
    assert controller_source.count(main_marker) == 1
    hook = "\n".join(
        [
            "",
            "_test_original_replace = os.replace",
            "",
            "def _test_publication_replace(source, destination):",
            "    _test_original_replace(source, destination)",
            "    expected = os.environ.get(",
            "        'OV_GUARD_TEST_PUBLICATION_TARGET'",
            "    )",
            "    if not expected:",
            "        return",
            "    observed = os.path.normcase(",
            "        os.path.abspath(os.fspath(destination))",
            "    )",
            "    if observed != os.path.normcase(os.path.abspath(expected)):",
            "        return",
            "    ready = Path(os.environ['OV_GUARD_TEST_PUBLICATION_READY'])",
            "    done = Path(os.environ['OV_GUARD_TEST_PUBLICATION_DONE'])",
            "    ready.write_text('published', encoding='ascii')",
            "    deadline = time.monotonic() + 10.0",
            "    while not done.is_file():",
            "        if time.monotonic() >= deadline:",
            "            raise RuntimeError(",
            "                'publication replacement watcher timed out'",
            "            )",
            "        time.sleep(0.005)",
            "",
            "os.replace = _test_publication_replace",
            "",
        ]
    )
    if controller_hook is not None:
        hook += "\n" + controller_hook.read_text(encoding="utf-8")
    controller.write_text(
        controller_source.replace(main_marker, hook + main_marker),
        encoding="utf-8",
    )


def _assert_path_provenance(
    record: dict[str, object],
    *,
    working_directory: Path,
    log_path: Path,
    evidence_path: Path,
) -> None:
    assert len(record["run_id"]) == 64
    assert all(character in "0123456789abcdef" for character in record["run_id"])
    assert record["requested_path_provenance"] == {
        "working_directory": str(working_directory),
        "log_path": str(log_path),
        "evidence_path": str(evidence_path),
    }
    assert record["path_identity_verified"] == {
        "working_directory": True,
        "log_path": True,
        "evidence_path": True,
    }


def _assert_controller_runtime(receipt: dict[str, object]) -> None:
    runtime = receipt["controller_runtime"]
    assert set(runtime) == {
        "trusted_install_root",
        "interpreter_path",
        "interpreter_sha256",
        "signature_status",
        "signature_type",
        "signer_subject",
        "signer_thumbprint",
        "runtime_dll_path",
        "runtime_dll_sha256",
        "runtime_dll_signature_status",
        "runtime_dll_signature_type",
        "runtime_dll_signer_subject",
        "runtime_dll_signer_thumbprint",
        "isolation_flags",
    }
    signer_subject = (
        "CN=Python Software Foundation, O=Python Software Foundation, "
        "L=Beaverton, S=Oregon, C=US"
    )
    assert runtime["trusted_install_root"] == str(APPROVED_PYTHON.parent)
    assert runtime["interpreter_path"] == str(APPROVED_PYTHON)
    assert runtime["interpreter_sha256"] == APPROVED_PYTHON_SHA256
    assert runtime["signature_status"] == "Valid"
    assert runtime["signature_type"] == "Authenticode"
    assert runtime["signer_subject"] == signer_subject
    assert runtime["runtime_dll_path"] == str(APPROVED_PYTHON_DLL)
    assert runtime["runtime_dll_sha256"] == APPROVED_PYTHON_DLL_SHA256
    assert runtime["runtime_dll_signature_status"] == "Valid"
    assert runtime["runtime_dll_signature_type"] == "Authenticode"
    assert runtime["runtime_dll_signer_subject"] == signer_subject
    for thumbprint_field in (
        "signer_thumbprint",
        "runtime_dll_signer_thumbprint",
    ):
        thumbprint = runtime[thumbprint_field]
        assert len(thumbprint) == 40
        assert all(character in "0123456789ABCDEF" for character in thumbprint)
    flags = runtime["isolation_flags"]
    assert flags[:5] == ["-E", "-s", "-S", "-B", "-X"]
    assert flags[-1] == "-m"
    assert len(flags) == 7
    prefix_label, prefix_value = flags[5].split("=", 1)
    assert prefix_label == "pycache_prefix"
    prefix = Path(prefix_value)
    assert prefix.is_absolute()
    assert prefix.name.startswith("official-openvino-controller-pycache-")
    assert not prefix.exists()


def _assert_wrapper_controller_binding(
    receipt: dict[str, object],
    record: dict[str, object],
) -> None:
    assert set(receipt) == {
        "schema",
        "run_id",
        "command",
        "working_directory",
        "log_path",
        "evidence_path",
        "requested_path_provenance",
        "path_identity_verified",
        "expected_exit",
        "actual_exit_code",
        "log_sha256",
        "evidence_sha256",
        "controller_runtime",
        "controller_binding",
        "valid",
    }
    _assert_controller_runtime(receipt)
    evidence_path = Path(record["evidence_path"])
    log_path = Path(record["log_path"])
    assert receipt["run_id"] == record["run_id"]
    assert receipt["controller_binding"] == {
        "schema": "official-openvino-controller-result/v1",
        "record_schema": "official-openvino-owned-process-guard/v1",
        "run_id": record["run_id"],
        "evidence_path": record["evidence_path"],
        "evidence_sha256": _sha256_path(evidence_path),
        "log_path": record["log_path"],
        "log_sha256": _sha256_path(log_path),
        "actual_exit_code": record["exit_code"],
        "valid": True,
    }


def test_private_temporary_prefixes_are_bounded_and_name_independent(
    tmp_path,
    monkeypatch,
):
    guard = _guarded_build()
    original_mkstemp = guard.tempfile.mkstemp
    original_named_temporary_file = guard.tempfile.NamedTemporaryFile
    observed_log_prefixes: list[str] = []
    observed_evidence_prefixes: list[str] = []

    def capture_mkstemp(*args, **kwargs):
        observed_log_prefixes.append(kwargs["prefix"])
        return original_mkstemp(*args, **kwargs)

    def capture_named_temporary_file(*args, **kwargs):
        observed_evidence_prefixes.append(kwargs["prefix"])
        return original_named_temporary_file(*args, **kwargs)

    monkeypatch.setattr(guard.tempfile, "mkstemp", capture_mkstemp)
    monkeypatch.setattr(
        guard.tempfile,
        "NamedTemporaryFile",
        capture_named_temporary_file,
    )

    record = guard.run_guarded_command(
        [sys.executable, "-c", "print('bounded-prefix',flush=True)"],
        cwd=ROOT,
        log_path=tmp_path / ("l" * 96 + ".log"),
        evidence_path=tmp_path / ("e" * 96 + ".json"),
        expected_exit="zero",
        limits=_limits(guard),
    )

    assert record["valid"] is True
    assert observed_log_prefixes == [".ovg-log-"]
    assert observed_evidence_prefixes == [".ovg-evidence-"]


def test_zero_exit_atomically_replaces_log_and_evidence(tmp_path):
    guard = _guarded_build()
    log_path = tmp_path / "child.log"
    evidence_path = tmp_path / "evidence.json"
    ready_path = tmp_path / "ready"
    release_path = tmp_path / "release"
    log_path.write_text("old-log\n", encoding="utf-8")
    evidence_path.write_text("old-evidence\n", encoding="utf-8")
    child = (
        "import pathlib,sys,time;"
        "ready=pathlib.Path(sys.argv[1]);"
        "release=pathlib.Path(sys.argv[2]);"
        "ready.write_text('ready',encoding='ascii');"
        "\nwhile not release.exists(): time.sleep(0.01)\n"
        "print('replacement-stdout',flush=True);"
        "print('replacement-stderr',file=sys.stderr,flush=True)"
    )
    result: dict[str, object] = {}
    failure: list[BaseException] = []

    def invoke() -> None:
        try:
            result.update(
                guard.run_guarded_command(
                    [sys.executable, "-c", child, str(ready_path), str(release_path)],
                    cwd=ROOT,
                    log_path=log_path,
                    evidence_path=evidence_path,
                    expected_exit="zero",
                    limits=_limits(guard),
                )
            )
        except BaseException as error:
            failure.append(error)

    worker = threading.Thread(target=invoke)
    worker.start()
    deadline = time.monotonic() + 5.0
    while not ready_path.exists() and time.monotonic() < deadline:
        time.sleep(0.01)
    assert ready_path.exists()
    assert log_path.read_text(encoding="utf-8") == "old-log\n"
    assert evidence_path.read_text(encoding="utf-8") == "old-evidence\n"
    release_path.write_text("release\n", encoding="ascii")
    worker.join(timeout=10.0)
    assert not worker.is_alive()
    assert failure == []
    assert result["valid"] is True
    combined_log = log_path.read_text(encoding="utf-8")
    assert "replacement-stdout" in combined_log
    assert "replacement-stderr" in combined_log
    persisted = json.loads(evidence_path.read_text(encoding="utf-8"))
    assert persisted == result
    assert list(tmp_path.glob(".ovg-log-*")) == []
    assert list(tmp_path.glob(".ovg-evidence-*")) == []
    _assert_path_provenance(
        result,
        working_directory=ROOT,
        log_path=log_path,
        evidence_path=evidence_path,
    )
    _assert_memory_evidence(result)
    _assert_zero_survivors(result)


def test_guard_keeps_original_private_log_descriptor_until_hash(
    tmp_path,
    monkeypatch,
):
    original_path_open = Path.open

    def reject_private_log_path_reopen(path, *args, **kwargs):
        if (
            path.parent == tmp_path
            and path.name.startswith(".ovg-log-")
        ):
            raise AssertionError("private log was reopened by pathname")
        return original_path_open(path, *args, **kwargs)

    monkeypatch.setattr(Path, "open", reject_private_log_path_reopen)
    record = _run(
        tmp_path,
        [sys.executable, "-c", "print('descriptor-retained',flush=True)"],
    )
    assert record["valid"] is True
    assert "descriptor-retained" in (
        tmp_path / "child.log"
    ).read_text(encoding="utf-8")


def test_working_directory_identity_failure_prevents_child_launch(
    monkeypatch,
    tmp_path,
):
    guard = _guarded_build()
    marker = tmp_path / "must-not-run"
    real_samefile = guard.os.path.samefile

    def reject_working_directory(left, right):
        if Path(left) == ROOT and Path(right) == ROOT:
            return False
        return real_samefile(left, right)

    monkeypatch.setattr(guard.os.path, "samefile", reject_working_directory)
    child = (
        "import pathlib,sys;"
        "pathlib.Path(sys.argv[1]).write_text('ran',encoding='ascii')"
    )
    record = _run(
        tmp_path,
        [sys.executable, "-c", child, str(marker)],
    )
    assert marker.exists() is False
    assert record["path_identity_verified"] == {
        "working_directory": False,
        "log_path": True,
        "evidence_path": True,
    }
    assert any(
        "working_directory identity verification failed" in error
        for error in record["validation_errors"]
    )
    assert record["valid"] is False
    assert json.loads(
        (tmp_path / "evidence.json").read_text(encoding="utf-8")
    ) == record


def test_evidence_parent_identity_failure_persists_invalid_record(
    monkeypatch,
    tmp_path,
):
    guard = _guarded_build()
    real_samefile = guard.os.path.samefile

    def reject_evidence_parent(left, right):
        if Path(left) == tmp_path and Path(right) == tmp_path:
            return False
        return real_samefile(left, right)

    monkeypatch.setattr(guard.os.path, "samefile", reject_evidence_parent)
    record = _run(tmp_path, [sys.executable, "-c", "print('ran')"])
    assert record["exit_code"] == 0
    assert record["path_identity_verified"] == {
        "working_directory": True,
        "log_path": True,
        "evidence_path": False,
    }
    assert any(
        "evidence_path identity verification failed" in error
        for error in record["validation_errors"]
    )
    assert record["valid"] is False
    assert json.loads(
        (tmp_path / "evidence.json").read_text(encoding="utf-8")
    ) == record


def test_expected_nonzero_exit_is_valid(tmp_path):
    record = _run(
        tmp_path,
        [sys.executable, "-c", "raise SystemExit(7)"],
        expected_exit="nonzero",
    )
    assert record["exit_code"] == 7
    assert record["expected_exit"] == "nonzero"
    assert record["valid"] is True
    _assert_zero_survivors(record)


def test_guard_hash_binds_named_input_bytes_before_launch(tmp_path):
    worker_spec = tmp_path / "worker-spec.json"
    worker_spec.write_bytes(b'{"controlled":"quality-worker-spec"}\n')
    expected_sha256 = hashlib.sha256(worker_spec.read_bytes()).hexdigest()

    record = _run(
        tmp_path,
        [sys.executable, "-c", "print('bound input observed')"],
        bound_inputs={"quality_worker_spec": worker_spec},
    )

    assert record["bound_inputs"] == [
        {
            "name": "quality_worker_spec",
            "path": str(worker_spec.resolve()),
            "sha256": expected_sha256,
        }
    ]
    persisted = json.loads(
        (tmp_path / "evidence.json").read_text(encoding="utf-8")
    )
    assert persisted["bound_inputs"] == record["bound_inputs"]
    worker_spec.write_bytes(b'{"controlled":"replacement"}\n')
    assert record["bound_inputs"][0]["sha256"] == expected_sha256


def test_guard_injects_quality_worker_authority_from_reopened_bound_input(
    tmp_path,
):
    worker_spec = tmp_path / "worker-spec.json"
    worker_spec.write_bytes(b'{"controlled":"quality-worker-spec"}\n')
    observed_path = tmp_path / "observed-authority.json"
    program = (
        "import hashlib,json,os,pathlib,sys;"
        "pathlib.Path(sys.argv[1]).write_text(json.dumps({"
        "'path':os.environ.get("
        "'OFFICIAL_OPENVINO_GUARD_BOUND_QUALITY_WORKER_SPEC_PATH'),"
        "'sha256':os.environ.get("
        "'OFFICIAL_OPENVINO_GUARD_BOUND_QUALITY_WORKER_SPEC_SHA256'),"
        "'environment_sha256':hashlib.sha256(json.dumps(dict(os.environ),"
        "ensure_ascii=False,sort_keys=True,separators=(',',':'),"
        "allow_nan=False).encode('utf-8')).hexdigest()"
        "},sort_keys=True),encoding='utf-8')"
    )

    record = _run(
        tmp_path,
        [sys.executable, "-c", program, str(observed_path)],
        bound_inputs={"quality_worker_spec": worker_spec},
    )

    observed = json.loads(observed_path.read_text(encoding="utf-8"))
    assert observed == {
        "path": str(worker_spec.resolve()),
        "sha256": hashlib.sha256(worker_spec.read_bytes()).hexdigest(),
        "environment_sha256": record["environment_sha256"],
    }
    assert record["valid"] is True


def test_guard_does_not_inject_quality_authority_for_other_bound_inputs(
    tmp_path,
):
    other_input = tmp_path / "other-input.json"
    other_input.write_text("{}\n", encoding="utf-8")
    program = (
        "import os;"
        "print(os.environ.get("
        "'OFFICIAL_OPENVINO_GUARD_BOUND_QUALITY_WORKER_SPEC_PATH','absent'));"
        "print(os.environ.get("
        "'OFFICIAL_OPENVINO_GUARD_BOUND_QUALITY_WORKER_SPEC_SHA256','absent'))"
    )

    record = _run(
        tmp_path,
        [sys.executable, "-c", program],
        bound_inputs={"other_input": other_input},
    )

    assert (tmp_path / "child.log").read_text(encoding="utf-8").splitlines() == [
        "absent",
        "absent",
    ]
    assert record["valid"] is True


@pytest.mark.parametrize(
    "reserved_key",
    (
        "official_openvino_guard_bound_quality_worker_spec_path",
        "Official_OpenVINO_Guard_Bound_Quality_Worker_Spec_Sha256",
    ),
)
def test_guard_rejects_case_insensitive_quality_authority_override(
    tmp_path,
    reserved_key,
):
    with pytest.raises(ValueError, match="reserved guard-owned"):
        _run(
            tmp_path,
            [sys.executable, "-c", "raise SystemExit(0)"],
            environment={
                "SYSTEMROOT": os.environ["SYSTEMROOT"],
                reserved_key: "caller-controlled",
            },
        )

    assert not (tmp_path / "child.log").exists()


def test_unexpected_exit_is_invalid_but_persists_evidence(tmp_path):
    record = _run(tmp_path, [sys.executable, "-c", "raise SystemExit(9)"])
    assert record["exit_code"] == 9
    assert record["valid"] is False
    assert any(
        "did not match expected_exit=zero" in value
        for value in record["validation_errors"]
    )
    assert json.loads(
        (tmp_path / "evidence.json").read_text(encoding="utf-8")
    ) == record
    _assert_zero_survivors(record)


def test_ram_floor_terminates_owned_tree(monkeypatch, tmp_path):
    guard = _guarded_build()
    samples = iter([4 * GIB, 1 * GIB, 4 * GIB])

    def available_ram() -> int:
        return next(samples, 4 * GIB)

    monkeypatch.setattr(guard, "available_ram_bytes", available_ram)
    record = _run(
        tmp_path,
        [sys.executable, "-c", "import time; time.sleep(60)"],
        limits=_limits(
            guard,
            minimum_available_ram_bytes=2 * GIB,
            maximum_runtime_seconds=5.0,
        ),
    )
    assert record["low_memory_stop"] is True
    assert record["termination_reason"] == "minimum_available_ram"
    assert record["configured_minimum_available_ram_bytes"] == 2 * GIB
    assert record["observed_available_ram_bytes"]["minimum"] == 1 * GIB
    assert record["valid"] is False
    _assert_zero_survivors(record)


def test_maximum_runtime_terminates_owned_tree(tmp_path):
    guard = _guarded_build()
    record = _run(
        tmp_path,
        [sys.executable, "-c", "import time; time.sleep(60)"],
        limits=_limits(guard, maximum_runtime_seconds=0.2),
    )
    assert record["timed_out"] is True
    assert record["termination_reason"] == "maximum_runtime"
    assert record["maximum_runtime_seconds"] == 0.2
    assert record["elapsed_seconds"] < 5.0
    assert record["valid"] is False
    _assert_zero_survivors(record)


def test_normal_root_exit_with_live_descendant_is_cleaned_up(tmp_path):
    guard = _guarded_build()
    owned = _owned_guard()
    child_pid_path = tmp_path / "child.pid"
    root_program = (
        "import pathlib,subprocess,sys,time;"
        "child=subprocess.Popen([sys.executable,'-c','import time;time.sleep(60)']);"
        "pathlib.Path(sys.argv[1]).write_text(str(child.pid),encoding='ascii');"
        "time.sleep(0.25);"
        "raise SystemExit(0)"
    )
    record = _run(
        tmp_path,
        [sys.executable, "-c", root_program, str(child_pid_path)],
        limits=_limits(guard, maximum_runtime_seconds=5.0),
    )
    child_pid = int(child_pid_path.read_text(encoding="ascii"))
    assert record["exit_code"] == 0
    assert record["timed_out"] is False
    assert record["low_memory_stop"] is False
    assert record["termination_reason"] is None
    assert child_pid in record["observed_pids"]
    assert record["child_process_observed"] is True
    assert record["job_object"]["terminate_job_called"] is True
    assert record["valid"] is True
    _assert_memory_evidence(record)
    _assert_zero_survivors(record)
    deadline = time.monotonic() + 2.0
    while (
        owned.process_memory_bytes(child_pid) is not None
        and time.monotonic() < deadline
    ):
        time.sleep(0.02)
    assert owned.process_memory_bytes(child_pid) is None


def test_generic_guard_disables_cpu_caps_and_msbuild_node_reuse(tmp_path):
    guard = _guarded_build()
    program = (
        "import os;"
        "print(os.environ.get('MSBUILDDISABLENODEREUSE','missing'),flush=True)"
    )
    record = _run(tmp_path, [sys.executable, "-c", program])
    source = inspect.getsource(guard.run_guarded_command)
    assert "set_cpu_rate_hard_cap" not in source
    assert "_set_process_affinity_mask" not in source
    assert record["launch_governance"] == {
        "created_suspended": True,
        "assigned_before_resume": True,
        "cpu_affinity_mask": None,
        "cpu_rate_hard_cap_percent": None,
    }
    assert record["msbuild_disable_node_reuse"] == "1"
    assert (tmp_path / "child.log").read_text(encoding="utf-8").strip() == "1"
    assert record["valid"] is True
    _assert_memory_evidence(record)


def test_guard_uses_supplied_environment_and_records_the_exact_effective_hash(
    tmp_path,
):
    environment = {
        "SYSTEMROOT": os.environ["SYSTEMROOT"],
        "TASK3_ENV": "retained-value",
    }
    program = (
        "import os,json;print(json.dumps({key:os.environ[key] for key in "
        "('SYSTEMROOT','TASK3_ENV','MSBUILDDISABLENODEREUSE')},sort_keys=True))"
    )

    record = _run(
        tmp_path,
        [sys.executable, "-c", program],
        environment=environment,
    )

    effective = {**environment, "MSBUILDDISABLENODEREUSE": "1"}
    assert json.loads((tmp_path / "child.log").read_text(encoding="utf-8")) == effective
    assert record["environment_sha256"] == hashlib.sha256(
        json.dumps(
            effective,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")
    ).hexdigest()


@pytest.mark.parametrize(
    "environment",
    (True, {"": "value"}, {"KEY": ""}, {"Path": "one", "PATH": "two"}),
)
def test_guard_rejects_invalid_environment_before_launching(tmp_path, environment):
    with pytest.raises((TypeError, ValueError), match="environment"):
        _run(
            tmp_path,
            [sys.executable, "-c", "raise SystemExit(0)"],
            environment=environment,
        )

    assert not (tmp_path / "child.log").exists()


def test_guard_fails_closed_when_any_job_pid_memory_query_fails(
    tmp_path,
    monkeypatch,
):
    guard = _guarded_build()
    monkeypatch.setattr(guard, "process_memory_bytes", lambda _pid: None)
    record = _run(
        tmp_path,
        [sys.executable, "-c", "import time;time.sleep(0.15)"],
    )
    assert record["memory_query_failed_pids"] != []
    assert record["valid"] is False
    assert "Job-PID memory query failed" in record["validation_errors"]


def test_job_memory_sample_ignores_only_confirmed_exited_pid(monkeypatch):
    guard = _guarded_build()

    class ExitingJob:
        def __init__(self):
            self.queries = 0

        def active_pids(self):
            self.queries += 1
            return [4312] if self.queries == 1 else []

    monkeypatch.setattr(guard, "process_memory_bytes", lambda _pid: None)
    active, working_set, private, failed = guard._sample_job_processes(
        ExitingJob()
    )
    assert active == [4312]
    assert working_set == 0
    assert private == 0
    assert failed == []


def test_cli_requires_literal_separator_and_preserves_argv(tmp_path):
    log_path = tmp_path / "cli.log"
    evidence_path = tmp_path / "cli.json"
    program = "import json,sys;print(json.dumps(sys.argv[1:]),flush=True)"
    child_argv = ["space value", "--switch=value", "semi;colon", "dollar$(literal)"]
    command = [
        sys.executable,
        "-m",
        "scripts.testing.campaigns.openvino.guarded_build",
        "--cwd",
        str(ROOT),
        "--log",
        str(log_path),
        "--evidence",
        str(evidence_path),
        "--expected-exit",
        "zero",
        "--minimum-available-ram-mib",
        "0",
        "--timeout-seconds",
        "5",
        "--",
        sys.executable,
        "-c",
        program,
        *child_argv,
    ]
    completed = subprocess.run(
        command,
        cwd=ROOT,
        text=True,
        capture_output=True,
        timeout=15,
    )
    assert completed.returncode == 0, completed.stderr
    assert json.loads(log_path.read_text(encoding="utf-8")) == child_argv
    record = json.loads(evidence_path.read_text(encoding="utf-8"))
    assert record["command"][-len(child_argv) :] == child_argv
    assert record["valid"] is True
    controller_result_lines = completed.stdout.splitlines()
    assert len(controller_result_lines) == 1
    controller_result = json.loads(controller_result_lines[0])
    assert controller_result == {
        "schema": "official-openvino-controller-result/v1",
        "record_schema": "official-openvino-owned-process-guard/v1",
        "run_id": record["run_id"],
        "evidence_path": record["evidence_path"],
        "evidence_sha256": _sha256_path(evidence_path),
        "log_path": record["log_path"],
        "log_sha256": _sha256_path(log_path),
        "actual_exit_code": 0,
        "valid": True,
    }


def test_cli_rejects_command_without_literal_separator(tmp_path):
    completed = subprocess.run(
        [
            sys.executable,
            "-m",
            "scripts.testing.campaigns.openvino.guarded_build",
            "--cwd",
            str(ROOT),
            "--log",
            str(tmp_path / "missing-separator.log"),
            "--evidence",
            str(tmp_path / "missing-separator.json"),
            "--expected-exit",
            "zero",
            "--minimum-available-ram-mib",
            "0",
            "--timeout-seconds",
            "5",
            sys.executable,
            "-c",
            "print('must not run')",
        ],
        cwd=ROOT,
        text=True,
        capture_output=True,
        timeout=10,
    )
    assert completed.returncode == 2
    assert "literal -- separator" in completed.stderr
    assert not (tmp_path / "missing-separator.log").exists()
    assert not (tmp_path / "missing-separator.json").exists()


def test_powershell_51_wrapper_preserves_command_array(tmp_path):
    wrapper = ROOT / "scripts" / "testing" / "tools" / "invoke_guarded_command.ps1"
    evidence_root = tmp_path / "powershell evidence"
    driver = tmp_path / "driver.ps1"
    program = "import json,sys;print(json.dumps(sys.argv[1:]),flush=True)"
    child_argv = ["space value", "--equals=x", "dollar$(literal)"]
    driver.write_text(
        "\n".join(
            [
                "$ErrorActionPreference = 'Stop'",
                "$childCommand = @(",
                f"  {_ps_literal(sys.executable)},",
                "  '-c',",
                f"  {_ps_literal(program)},",
                f"  {_ps_literal(child_argv[0])},",
                f"  {_ps_literal(child_argv[1])},",
                f"  {_ps_literal(child_argv[2])}",
                ")",
                (
                    f"& {_ps_literal(str(wrapper))} "
                    f"-Label 'ps51-literal-argv' "
                    f"-WorkingDirectory {_ps_literal(str(ROOT))} "
                    f"-EvidenceRoot {_ps_literal(str(evidence_root))} "
                    "-ExpectedExit Zero -TimeoutSeconds 5 "
                    f"{_ps_runtime_arguments()} "
                    "-Command $childCommand"
                ),
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    completed = subprocess.run(
        [
            "powershell.exe",
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            str(driver),
        ],
        cwd=ROOT,
        text=True,
        capture_output=True,
        timeout=20,
    )
    assert completed.returncode == 0, completed.stderr
    assert json.loads(
        (evidence_root / "ps51-literal-argv.log").read_text(encoding="utf-8")
    ) == child_argv
    record = json.loads(
        (evidence_root / "ps51-literal-argv.json").read_text(encoding="utf-8")
    )
    assert record["valid"] is True
    assert record["maximum_runtime_seconds"] == 5.0
    receipt = json.loads(completed.stdout)
    assert receipt["schema"] == "official-openvino-wrapper-verification/v1"
    assert receipt["actual_exit_code"] == 0
    assert receipt["expected_exit"] == "zero"
    assert receipt["command"] == [
        sys.executable,
        "-c",
        program,
        *child_argv,
    ]
    assert receipt["log_sha256"] == record["log_sha256"]
    assert len(receipt["evidence_sha256"]) == 64
    _assert_path_provenance(
        record,
        working_directory=ROOT,
        log_path=evidence_root / "ps51-literal-argv.log",
        evidence_path=evidence_root / "ps51-literal-argv.json",
    )
    assert receipt["working_directory"] == record["working_directory"]
    assert receipt["log_path"] == record["log_path"]
    assert receipt["evidence_path"] == record["evidence_path"]
    assert receipt["requested_path_provenance"] == (
        record["requested_path_provenance"]
    )
    assert receipt["path_identity_verified"] == (
        record["path_identity_verified"]
    )
    _assert_wrapper_controller_binding(receipt, record)
    _assert_memory_evidence(record)
    _assert_zero_survivors(record)


@pytest.mark.skipif(os.name != "nt", reason="Windows Job Objects are required")
def test_guard_assigns_containing_job_before_fine_job_and_resume(
    tmp_path,
    monkeypatch,
):
    guard = _guarded_build()
    containing = guard.KillOnCloseJob(
        f"WB04-test-containing-{hashlib.sha256(str(tmp_path).encode()).hexdigest()[:16]}"
    )
    real_assign = guard.KillOnCloseJob.assign_pid
    real_resume = guard._resume_suspended_process
    events = []

    def record_assign(job, pid):
        events.append(("containing" if job is containing else "fine", pid, job))
        return real_assign(job, pid)

    def record_resume(pid):
        events.append(("resume", pid, None))
        return real_resume(pid)

    monkeypatch.setattr(guard.KillOnCloseJob, "assign_pid", record_assign)
    monkeypatch.setattr(guard, "_resume_suspended_process", record_resume)
    try:
        record = guard.run_guarded_command(
            [sys.executable, "-c", "print('contained', flush=True)"],
            cwd=ROOT,
            log_path=tmp_path / "contained.log",
            evidence_path=tmp_path / "contained.json",
            expected_exit="zero",
            limits=_limits(guard),
            _containing_job=containing,
        )
        assert containing.active_pids() == []
    finally:
        containing.close()

    assert [event[0] for event in events[:3]] == [
        "containing",
        "fine",
        "resume",
    ]
    assert events[0][1] == events[1][1] == events[2][1]
    assert record["containing_job_assignment"] == {
        "requested": True,
        "assigned_before_fine_job": True,
        "assigned_pid": record["root_pid"],
        "query_ok_after_cleanup": True,
        "active_pids_after_cleanup": [],
    }


def test_powershell_wrapper_has_no_pre_guard_helper_launch():
    wrapper = ROOT / "scripts" / "testing" / "tools" / "invoke_guarded_command.ps1"
    with wrapper.open("r", encoding="utf-8", newline="") as stream:
        source = stream.read()
    invocation = "$guardNativeOutput = @(& $python @guardArguments)"
    prefix, separator, _ = source.partition(invocation)
    assert separator == invocation
    lowered_prefix = prefix.casefold()
    assert "add-type" not in lowered_prefix
    assert "csc.exe" not in lowered_prefix
    assert "start-process" not in lowered_prefix
    assert "invoke-expression" not in lowered_prefix
    assert ".exe" not in lowered_prefix
    pre_controller_commands = [
        command
        for command in _powershell_command_asts(wrapper)
        if command["start"] < len(prefix)
    ]
    assert pre_controller_commands
    for command in pre_controller_commands:
        assert command["name"] is None
        words = command["text"].strip().split()
        assert len(words) >= 2 and words[0] == "&"
        variable = words[1]
        assert variable in WRAPPER_DYNAMIC_COMMAND_VARIABLES
        assert variable != "$python"


def test_wrapper_module_qualifies_every_external_powershell_command():
    wrapper = ROOT / "scripts" / "testing" / "tools" / "invoke_guarded_command.ps1"
    with wrapper.open("r", encoding="utf-8", newline="") as stream:
        source = stream.read()
    commands = _powershell_command_asts(wrapper)
    observed_dynamic_variables = set()
    python_invocations = []
    for command in commands:
        name = command["name"]
        if name is None:
            words = command["text"].strip().split()
            assert len(words) >= 2 and words[0] == "&", (
                f"line {command['line']} has an unrecognized dynamic "
                f"invocation: {command['text']}"
            )
            variable = words[1]
            assert variable in WRAPPER_DYNAMIC_COMMAND_VARIABLES, (
                f"line {command['line']} invokes unapproved dynamic command "
                f"{variable!r}: {command['text']}"
            )
            observed_dynamic_variables.add(variable)
            if variable == "$python":
                python_invocations.append(command)
            continue
        pytest.fail(
            f"line {command['line']} uses literal command name "
            f"{name!r}: {command['text']}"
        )
    assert observed_dynamic_variables == WRAPPER_DYNAMIC_COMMAND_VARIABLES
    assert len(python_invocations) == 1
    assert python_invocations[0]["text"] == "& $python @guardArguments"
    controller_assignment = (
        "$guardNativeOutput = @(& $python @guardArguments)"
    )
    assignment_start = source.index(controller_assignment)
    expected_python_start = assignment_start + controller_assignment.index("&")
    assert python_invocations[0]["start"] == expected_python_start
    assert "Get-FileHash" not in source


def test_wrapper_ignores_hostile_same_runspace_command_shadows(tmp_path):
    wrapper = ROOT / "scripts" / "testing" / "tools" / "invoke_guarded_command.ps1"
    driver = tmp_path / "hostile-command-shadow-driver.ps1"
    evidence_root = tmp_path / "hostile-command-shadow-evidence"
    whoami = Path(os.environ["SystemRoot"]) / "System32" / "whoami.exe"
    _write_wrapper_driver(
        driver,
        wrapper=wrapper,
        label="hostile-command-shadow",
        working_directory=ROOT,
        evidence_root=evidence_root,
        program="",
        command=[str(whoami)],
    )
    external_commands = {
        "Remove-Module": "Microsoft.PowerShell.Core",
        "Set-StrictMode": "Microsoft.PowerShell.Core",
        "ConvertFrom-Json": "Microsoft.PowerShell.Utility",
        "ConvertTo-Json": "Microsoft.PowerShell.Utility",
        "ForEach-Object": "Microsoft.PowerShell.Core",
        "Get-AuthenticodeSignature": "Microsoft.PowerShell.Security",
        "Get-ChildItem": "Microsoft.PowerShell.Management",
        "Get-FileHash": "Microsoft.PowerShell.Utility",
        "Get-Item": "Microsoft.PowerShell.Management",
        "Join-Path": "Microsoft.PowerShell.Management",
        "New-Object": "Microsoft.PowerShell.Utility",
        "Out-Null": "Microsoft.PowerShell.Core",
        "Push-Location": "Microsoft.PowerShell.Management",
        "Pop-Location": "Microsoft.PowerShell.Management",
        "Resolve-Path": "Microsoft.PowerShell.Management",
        "Where-Object": "Microsoft.PowerShell.Core",
        "Write-Output": "Microsoft.PowerShell.Utility",
    }
    former_internal_helpers = (
        "Assert-NoReparseDirectoryAncestry",
        "Assert-NotReparsePoint",
        "Assert-JsonArray",
        "Assert-JsonBoolean",
        "Assert-JsonInteger",
        "Assert-JsonNull",
        "Assert-JsonNumber",
        "Assert-JsonObjectShape",
        "Assert-JsonString",
        "Get-Sha256Hex",
    )
    shadow_names = list(former_internal_helpers)
    for basename, module_name in external_commands.items():
        shadow_names.extend((basename, f"{module_name}\\{basename}"))
    shadow_lines = []
    markers = []
    for index, shadow_name in enumerate(shadow_names):
        marker = tmp_path / f"shadowed-command-{index}.marker"
        markers.append(marker)
        target = f"Invoke-GuardHostileShadow{index}"
        body = (
            f"[IO.File]::WriteAllText({_ps_literal(str(marker))}, "
            f"{_ps_literal(shadow_name)}); "
            f"throw {_ps_literal('hostile shadow: ' + shadow_name)}"
        )
        shadow_lines.extend(
            [
                f"function {target} {{ {body} }}",
                f"function {shadow_name} {{ {body} }}",
                (
                    "Microsoft.PowerShell.Utility\\Set-Alias "
                    f"-Name {_ps_literal(shadow_name)} "
                    f"-Value {_ps_literal(target)} -Scope Local -Force"
                ),
            ]
        )
    original_driver = driver.read_text(encoding="utf-8")
    driver.write_text(
        "\n".join(shadow_lines) + "\n" + original_driver,
        encoding="utf-8",
    )
    completed = _invoke_wrapper_driver(driver)
    assert completed.returncode == 0, completed.stderr
    receipt = json.loads(completed.stdout)
    _assert_controller_runtime(receipt)
    assert [marker for marker in markers if marker.exists()] == []


def _invoke_wrapper_with_publication_replacement(
    tmp_path: Path,
    artifact: str,
):
    trusted_root = tmp_path / f"{artifact}-trusted-root"
    _copy_guard_controller_package(
        trusted_root,
        publication_hook=True,
    )
    wrapper = (
        trusted_root
        / "scripts"
        / "testing"
        / "tools"
        / "invoke_guarded_command.ps1"
    )
    child_working_directory = tmp_path / f"{artifact}-child-working"
    child_working_directory.mkdir()
    evidence_root = tmp_path / f"{artifact}-evidence"
    label = f"publication-{artifact}"
    driver = tmp_path / f"{artifact}-publication-driver.ps1"
    _write_wrapper_driver(
        driver,
        wrapper=wrapper,
        label=label,
        working_directory=child_working_directory,
        evidence_root=evidence_root,
        program="print('trusted-publication-bytes',flush=True)",
    )
    suffix = ".log" if artifact == "log" else ".json"
    target = evidence_root / f"{label}{suffix}"
    ready = tmp_path / f"{artifact}-publication-ready"
    done = tmp_path / f"{artifact}-publication-done"
    watcher_result: dict[str, bytes] = {}
    watcher_failures: list[BaseException] = []

    def replace_public_artifact() -> None:
        try:
            deadline = time.monotonic() + 10.0
            while not ready.is_file():
                if time.monotonic() >= deadline:
                    raise AssertionError(
                        f"{artifact} publication was not observed"
                    )
                time.sleep(0.005)
            original_bytes = target.read_bytes()
            if artifact == "log":
                forged_bytes = b"forged-public-log\\n"
            else:
                forged_record = json.loads(original_bytes)
                forged_record["started_utc"] = "forged-public-evidence"
                forged_bytes = (
                    json.dumps(
                        forged_record,
                        indent=2,
                        sort_keys=True,
                        allow_nan=False,
                    )
                    + "\n"
                ).encode("utf-8")
            replacement = target.with_name(target.name + ".watcher.tmp")
            replacement.write_bytes(forged_bytes)
            os.replace(replacement, target)
            watcher_result["original"] = original_bytes
            watcher_result["forged"] = forged_bytes
        except BaseException as error:
            watcher_failures.append(error)
        finally:
            done.write_text("replaced", encoding="ascii")

    watcher = threading.Thread(
        target=replace_public_artifact,
        name=f"{artifact}-publication-watcher",
        daemon=True,
    )
    watcher.start()
    environment = os.environ.copy()
    environment["OV_GUARD_TEST_PUBLICATION_TARGET"] = str(target.resolve())
    environment["OV_GUARD_TEST_PUBLICATION_READY"] = str(ready)
    environment["OV_GUARD_TEST_PUBLICATION_DONE"] = str(done)
    completed = _invoke_wrapper_driver(
        driver,
        environment=environment,
        cwd=trusted_root,
    )
    watcher.join(timeout=2.0)
    assert not watcher.is_alive()
    assert watcher_failures == []
    assert target.read_bytes() == watcher_result["forged"]
    assert watcher_result["original"] != watcher_result["forged"]
    return completed


def test_wrapper_rejects_public_log_replacement_after_publication(tmp_path):
    completed = _invoke_wrapper_with_publication_replacement(tmp_path, "log")
    assert completed.returncode != 0
    assert "Controller-bound log SHA-256 does not match" in completed.stderr


def test_wrapper_rejects_public_evidence_replacement_after_publication(
    tmp_path,
):
    completed = _invoke_wrapper_with_publication_replacement(
        tmp_path,
        "evidence",
    )
    assert completed.returncode != 0
    assert "Controller-bound evidence SHA-256 does not match" in (
        completed.stderr
    )


def test_wrapper_ignores_inherited_pythonpath_sitecustomize(tmp_path):
    marker = tmp_path / "sitecustomize-executed"
    hook_directory = tmp_path / "untrusted-pythonpath"
    hook_directory.mkdir()
    (hook_directory / "sitecustomize.py").write_text(
        "\n".join(
            [
                "from pathlib import Path",
                f"Path({str(marker)!r}).write_text(",
                "    'executed', encoding='ascii'",
                ")",
                "",
            ]
        ),
        encoding="utf-8",
    )
    wrapper = ROOT / "scripts" / "testing" / "tools" / "invoke_guarded_command.ps1"
    driver = tmp_path / "isolated-startup-driver.ps1"
    whoami = Path(os.environ["SystemRoot"]) / "System32" / "whoami.exe"
    _write_wrapper_driver(
        driver,
        wrapper=wrapper,
        label="isolated-startup",
        working_directory=ROOT,
        evidence_root=tmp_path / "isolated-startup-evidence",
        program="",
        command=[str(whoami)],
    )
    environment = os.environ.copy()
    environment["PYTHONPATH"] = os.pathsep.join(
        filter(
            None,
            (str(hook_directory), environment.get("PYTHONPATH")),
        )
    )
    completed = _invoke_wrapper_driver(driver, environment=environment)
    assert completed.returncode == 0, completed.stderr
    _assert_controller_runtime(json.loads(completed.stdout))
    assert not marker.exists()


def test_wrapper_ignores_python_path_shim_with_approved_runtime(tmp_path):
    shim_directory = tmp_path / "python-path-shim"
    shim_directory.mkdir()
    marker = tmp_path / "unsigned-python-shim-executed"
    (shim_directory / "python.cmd").write_text(
        "\n".join(
            [
                "@echo off",
                f">\"{marker}\" echo executed",
                "echo {}",
                "exit /b 0",
                "",
            ]
        ),
        encoding="ascii",
    )
    wrapper = ROOT / "scripts" / "testing" / "tools" / "invoke_guarded_command.ps1"
    driver = tmp_path / "unsigned-python-shim-driver.ps1"
    whoami = Path(os.environ["SystemRoot"]) / "System32" / "whoami.exe"
    _write_wrapper_driver(
        driver,
        wrapper=wrapper,
        label="unsigned-python-shim",
        working_directory=ROOT,
        evidence_root=tmp_path / "unsigned-python-shim-evidence",
        program="",
        command=[str(whoami)],
    )
    environment = os.environ.copy()
    environment["PATH"] = os.pathsep.join(
        (str(shim_directory), environment["PATH"])
    )
    completed = _invoke_wrapper_driver(driver, environment=environment)
    assert completed.returncode == 0, completed.stderr
    _assert_controller_runtime(json.loads(completed.stdout))
    assert not marker.exists()


@pytest.mark.parametrize(
    ("runtime_overrides", "expected_error"),
    [
        (
            {"python_sha256": "0" * 64},
            "interpreter SHA-256 does not match",
        ),
        (
            {"python_dll_sha256": "0" * 64},
            "runtime DLL SHA-256 does not match",
        ),
    ],
)
def test_wrapper_rejects_unapproved_runtime_hashes_before_launch(
    tmp_path,
    runtime_overrides,
    expected_error,
):
    wrapper = ROOT / "scripts" / "testing" / "tools" / "invoke_guarded_command.ps1"
    driver = tmp_path / "unapproved-runtime-driver.ps1"
    marker = tmp_path / "runtime-child-must-not-run"
    child = (
        "import pathlib,sys;"
        "pathlib.Path(sys.argv[1]).write_text('ran',encoding='ascii')"
    )
    _write_wrapper_driver(
        driver,
        wrapper=wrapper,
        label="unapproved-runtime",
        working_directory=ROOT,
        evidence_root=tmp_path / "unapproved-runtime-evidence",
        program="",
        command=[sys.executable, "-c", child, str(marker)],
        **runtime_overrides,
    )
    completed = _invoke_wrapper_driver(driver)
    assert completed.returncode != 0
    assert expected_error in completed.stderr
    assert not marker.exists()


def test_wrapper_rejects_unsigned_approved_runtime_before_launch(tmp_path):
    fake_install = tmp_path / "unsigned-approved-runtime"
    fake_install.mkdir()
    fake_python = fake_install / "python.exe"
    fake_dll = fake_install / "python999.dll"
    fake_python.write_bytes(b"unsigned-python-executable")
    fake_dll.write_bytes(b"unsigned-python-runtime-dll")
    wrapper = ROOT / "scripts" / "testing" / "tools" / "invoke_guarded_command.ps1"
    driver = tmp_path / "unsigned-approved-runtime-driver.ps1"
    marker = tmp_path / "unsigned-runtime-child-must-not-run"
    child = (
        "import pathlib,sys;"
        "pathlib.Path(sys.argv[1]).write_text('ran',encoding='ascii')"
    )
    _write_wrapper_driver(
        driver,
        wrapper=wrapper,
        label="unsigned-approved-runtime",
        working_directory=ROOT,
        evidence_root=tmp_path / "unsigned-approved-runtime-evidence",
        program="",
        command=[sys.executable, "-c", child, str(marker)],
        python_executable=fake_python,
        python_sha256=hashlib.sha256(fake_python.read_bytes()).hexdigest(),
        python_dll_sha256=hashlib.sha256(fake_dll.read_bytes()).hexdigest(),
    )
    completed = _invoke_wrapper_driver(driver)
    assert completed.returncode != 0
    assert "signature is not valid and PSF-signed" in completed.stderr
    assert not marker.exists()


@pytest.mark.parametrize(
    "tampered_component",
    ["interpreter", "runtime_dll"],
)
def test_wrapper_rejects_hash_approved_pe_text_tamper(
    tmp_path,
    tampered_component,
):
    copied_install = tmp_path / f"{tampered_component}-tampered-install"
    copied_install.mkdir()
    copied_python = copied_install / APPROVED_PYTHON.name
    copied_dll = copied_install / APPROVED_PYTHON_DLL.name
    shutil.copyfile(APPROVED_PYTHON, copied_python)
    shutil.copyfile(APPROVED_PYTHON_DLL, copied_dll)
    target = (
        copied_python
        if tampered_component == "interpreter"
        else copied_dll
    )
    _flip_pe_text_byte(target)
    wrapper = ROOT / "scripts" / "testing" / "tools" / "invoke_guarded_command.ps1"
    driver = tmp_path / f"{tampered_component}-pe-tamper-driver.ps1"
    marker = tmp_path / f"{tampered_component}-child-must-not-run"
    child = (
        "import pathlib,sys;"
        "pathlib.Path(sys.argv[1]).write_text('ran',encoding='ascii')"
    )
    _write_wrapper_driver(
        driver,
        wrapper=wrapper,
        label=f"{tampered_component}-pe-tamper",
        working_directory=ROOT,
        evidence_root=tmp_path / f"{tampered_component}-pe-tamper-evidence",
        program="",
        command=[sys.executable, "-c", child, str(marker)],
        python_executable=copied_python,
        python_sha256=hashlib.sha256(copied_python.read_bytes()).hexdigest(),
        python_dll_sha256=hashlib.sha256(copied_dll.read_bytes()).hexdigest(),
    )
    completed = _invoke_wrapper_driver(driver)
    assert completed.returncode != 0
    assert "status=HashMismatch" in completed.stderr
    assert not marker.exists()


def test_wrapper_rejects_runtime_install_junction(tmp_path):
    junction = tmp_path / "approved-runtime-junction"
    cmd = Path(os.environ["SystemRoot"]) / "System32" / "cmd.exe"
    created = subprocess.run(
        [
            str(cmd),
            "/d",
            "/c",
            "mklink",
            "/J",
            str(junction),
            str(APPROVED_PYTHON.parent),
        ],
        text=True,
        capture_output=True,
        timeout=10,
    )
    if created.returncode != 0:
        pytest.skip(f"directory junction unavailable: {created.stderr}")
    wrapper = ROOT / "scripts" / "testing" / "tools" / "invoke_guarded_command.ps1"
    driver = tmp_path / "runtime-junction-driver.ps1"
    marker = tmp_path / "runtime-junction-child-must-not-run"
    child = (
        "import pathlib,sys;"
        "pathlib.Path(sys.argv[1]).write_text('ran',encoding='ascii')"
    )
    try:
        _write_wrapper_driver(
            driver,
            wrapper=wrapper,
            label="runtime-junction",
            working_directory=ROOT,
            evidence_root=tmp_path / "runtime-junction-evidence",
            program="",
            command=[sys.executable, "-c", child, str(marker)],
            python_executable=junction / APPROVED_PYTHON.name,
        )
        completed = _invoke_wrapper_driver(driver)
    finally:
        junction.rmdir()
    assert completed.returncode != 0
    assert "install ancestry contains a reparse point" in completed.stderr
    assert not marker.exists()
    assert APPROVED_PYTHON.is_file()


def test_wrapper_resolves_controller_only_from_wrapper_repo_root(tmp_path):
    attacker_root = tmp_path / "attacker-current-directory"
    attacker_module = (
        attacker_root
        / "scripts"
        / "testing"
        / "campaigns"
        / "openvino"
        / "guarded_build.py"
    )
    attacker_module.parent.mkdir(parents=True)
    marker = tmp_path / "attacker-controller-executed"
    attacker_module.write_text(
        "\n".join(
            [
                "from pathlib import Path",
                f"Path({str(marker)!r}).write_text(",
                "    'executed', encoding='ascii'",
                ")",
                "raise SystemExit(97)",
                "",
            ]
        ),
        encoding="utf-8",
    )
    wrapper = ROOT / "scripts" / "testing" / "tools" / "invoke_guarded_command.ps1"
    driver = tmp_path / "pinned-working-directory-driver.ps1"
    whoami = Path(os.environ["SystemRoot"]) / "System32" / "whoami.exe"
    child_working_directory = tmp_path / "separate-child-working-directory"
    child_working_directory.mkdir()
    _write_wrapper_driver(
        driver,
        wrapper=wrapper,
        label="pinned-working-directory",
        working_directory=child_working_directory,
        evidence_root=tmp_path / "pinned-working-directory-evidence",
        program="",
        command=[str(whoami)],
    )
    completed = _invoke_wrapper_driver(driver, cwd=attacker_root)
    assert completed.returncode == 0, completed.stderr
    _assert_controller_runtime(json.loads(completed.stdout))
    assert not marker.exists()


@pytest.mark.parametrize(
    "module_name",
    ["guarded_build", "owned_process_guard"],
)
def test_wrapper_ignores_poisoned_repo_bytecode_cache(
    tmp_path,
    module_name,
):
    trusted_root = tmp_path / f"{module_name}-bytecode-trusted-root"
    _copy_guard_controller_package(trusted_root)
    target_module = (
        trusted_root
        / "scripts"
        / "testing"
        / "campaigns"
        / "openvino"
        / f"{module_name}.py"
    )
    marker = tmp_path / f"{module_name}-poisoned-bytecode-executed"
    malicious_source = tmp_path / f"poisoned-{module_name}.py"
    malicious_source.write_text(
        "\n".join(
            [
                "from pathlib import Path",
                f"Path({str(marker)!r}).write_text(",
                "    'executed', encoding='ascii'",
                ")",
                "raise SystemExit(96)",
                "",
            ]
        ),
        encoding="utf-8",
    )
    poisoned_cache = Path(
        importlib.util.cache_from_source(str(target_module))
    )
    poisoned_cache.parent.mkdir(parents=True)
    py_compile.compile(
        str(malicious_source),
        cfile=str(poisoned_cache),
        doraise=True,
        invalidation_mode=py_compile.PycInvalidationMode.UNCHECKED_HASH,
    )
    direct = subprocess.run(
        [
            str(APPROVED_PYTHON),
            "-E",
            "-s",
            "-S",
            "-B",
            "-m",
            "scripts.testing.campaigns.openvino.guarded_build",
        ],
        cwd=trusted_root,
        text=True,
        capture_output=True,
        timeout=10,
    )
    assert direct.returncode == 96
    assert marker.read_text(encoding="ascii") == "executed"
    marker.unlink()
    child_working_directory = tmp_path / f"{module_name}-bytecode-child-working"
    child_working_directory.mkdir()
    wrapper = (
        trusted_root
        / "scripts"
        / "testing"
        / "tools"
        / "invoke_guarded_command.ps1"
    )
    driver = tmp_path / f"{module_name}-bytecode-cache-driver.ps1"
    whoami = Path(os.environ["SystemRoot"]) / "System32" / "whoami.exe"
    _write_wrapper_driver(
        driver,
        wrapper=wrapper,
        label=f"{module_name}-bytecode-cache",
        working_directory=child_working_directory,
        evidence_root=tmp_path / f"{module_name}-bytecode-cache-evidence",
        program="",
        command=[str(whoami)],
    )
    completed = _invoke_wrapper_driver(driver, cwd=tmp_path)
    assert completed.returncode == 0, completed.stderr
    _assert_controller_runtime(json.loads(completed.stdout))
    assert not marker.exists()


def _invoke_wrapper_with_controller_stdout_mode(
    tmp_path: Path,
    mode: str,
    *,
    command: list[str] | None = None,
):
    hook_directory = tmp_path / f"{mode}-hook"
    _write_controller_hook(hook_directory)
    trusted_root = tmp_path / f"{mode}-trusted-controller"
    _copy_guard_controller_package(
        trusted_root,
        controller_hook=hook_directory / "sitecustomize.py",
    )
    wrapper = (
        trusted_root
        / "scripts"
        / "testing"
        / "tools"
        / "invoke_guarded_command.ps1"
    )
    child_working_directory = tmp_path / f"{mode}-child-working"
    child_working_directory.mkdir()
    evidence_root = tmp_path / f"{mode}-evidence"
    driver = tmp_path / f"{mode}-driver.ps1"
    _write_wrapper_driver(
        driver,
        wrapper=wrapper,
        label=f"controller-{mode}",
        working_directory=child_working_directory,
        evidence_root=evidence_root,
        program="print('controller-output-mode',flush=True)",
        command=command,
    )
    environment = os.environ.copy()
    environment["OV_GUARD_TEST_STDOUT_MODE"] = mode
    return _invoke_wrapper_driver(driver, environment=environment)


def test_wrapper_rejects_malformed_controller_result(tmp_path):
    completed = _invoke_wrapper_with_controller_stdout_mode(
        tmp_path,
        "malformed",
    )
    assert completed.returncode != 0
    assert "Controller result is not valid JSON" in completed.stderr


def test_wrapper_rejects_empty_controller_result(tmp_path):
    completed = _invoke_wrapper_with_controller_stdout_mode(
        tmp_path,
        "empty",
    )
    assert completed.returncode != 0
    assert "exactly one controller result" in completed.stderr


def test_wrapper_rejects_multiple_controller_results(tmp_path):
    completed = _invoke_wrapper_with_controller_stdout_mode(
        tmp_path,
        "multiple",
    )
    assert completed.returncode != 0
    assert "exactly one controller result" in completed.stderr


def test_wrapper_rejects_controller_run_id_tamper(tmp_path):
    completed = _invoke_wrapper_with_controller_stdout_mode(
        tmp_path,
        "tamper_run_id",
    )
    assert completed.returncode != 0
    assert "run_id does not match" in completed.stderr


def test_wrapper_rejects_controller_evidence_hash_tamper(tmp_path):
    completed = _invoke_wrapper_with_controller_stdout_mode(
        tmp_path,
        "tamper_evidence_hash",
    )
    assert completed.returncode != 0
    assert "evidence SHA-256 does not match" in completed.stderr


def test_wrapper_rejects_controller_evidence_path_tamper(tmp_path):
    completed = _invoke_wrapper_with_controller_stdout_mode(
        tmp_path,
        "tamper_evidence_path",
    )
    assert completed.returncode != 0
    assert "evidence path does not match" in completed.stderr


@pytest.mark.parametrize(
    ("mode", "expected_error"),
    [
        (
            "record_schema_array",
            "Guard evidence record schema must be a JSON string",
        ),
        (
            "record_expected_exit_array",
            "Guard expected_exit must be a JSON string",
        ),
        (
            "record_maximum_runtime_string",
            "Guard maximum_runtime_seconds must be a JSON number",
        ),
        (
            "record_created_suspended_array",
            (
                "Guard launch_governance.created_suspended "
                "must be a JSON boolean"
            ),
        ),
        (
            "record_observed_ram_array",
            "Guard observed_available_ram_bytes must be a JSON object",
        ),
        (
            "record_memory_query_failed_pid",
            "Guard memory_query_failed_pids must be empty",
        ),
    ],
)
def test_wrapper_rejects_hash_consistent_record_shape_tamper(
    tmp_path,
    mode,
    expected_error,
):
    completed = _invoke_wrapper_with_controller_stdout_mode(tmp_path, mode)
    assert completed.returncode != 0
    assert expected_error in completed.stderr


def test_wrapper_rejects_hash_consistent_scalar_command_tamper(tmp_path):
    whoami = Path(os.environ["SystemRoot"]) / "System32" / "whoami.exe"
    completed = _invoke_wrapper_with_controller_stdout_mode(
        tmp_path,
        "record_scalar_command",
        command=[str(whoami)],
    )
    assert completed.returncode != 0
    assert "Guard command must be a JSON array" in completed.stderr


def test_wrapper_binds_to_completed_controller_run_after_subst_retarget(
    tmp_path,
):
    stale_root = tmp_path / "stale-root"
    current_root = tmp_path / "current-root"
    for root in (stale_root, current_root):
        (root / "working").mkdir(parents=True)
    hook_directory = tmp_path / "controller-hook"
    _write_controller_hook(hook_directory)
    trusted_root = tmp_path / "retarget-trusted-controller"
    _copy_guard_controller_package(
        trusted_root,
        controller_hook=hook_directory / "sitecustomize.py",
    )
    wrapper = (
        trusted_root
        / "scripts"
        / "testing"
        / "tools"
        / "invoke_guarded_command.ps1"
    )
    driver = tmp_path / "binding-driver.ps1"
    label = "controller-binding"
    program = "print('controller-binding',flush=True)"

    with _owned_subst(stale_root) as drive:
        substituted_root = Path(f"{drive}:\\")
        _write_wrapper_driver(
            driver,
            wrapper=wrapper,
            label=label,
            working_directory=substituted_root / "working",
            evidence_root=substituted_root / "evidence",
            program=program,
        )
        stale_completed = _invoke_wrapper_driver(driver)
        assert stale_completed.returncode == 0, stale_completed.stderr
    stale_evidence_path = stale_root / "evidence" / f"{label}.json"
    stale_record = json.loads(
        stale_evidence_path.read_text(encoding="utf-8")
    )

    current_target = _dos_target(current_root)
    stale_target = _dos_target(stale_root)
    with _owned_subst(current_root, preferred_letter=drive) as current_drive:
        assert current_drive == drive
        environment = os.environ.copy()
        environment["OV_GUARD_TEST_RETARGET_DEVICE"] = f"{drive}:"
        environment["OV_GUARD_TEST_RETARGET_TARGET"] = stale_target
        try:
            completed = _invoke_wrapper_driver(
                driver,
                environment=environment,
            )
            assert _query_dos_device(f"{drive}:") == (
                stale_target,
                current_target,
            )
        finally:
            if stale_target in _query_dos_device(f"{drive}:"):
                _remove_exact_dos_target(f"{drive}:", stale_target)
            assert _query_dos_device(f"{drive}:") == (current_target,)

    assert completed.returncode == 0, completed.stderr
    current_evidence_path = current_root / "evidence" / f"{label}.json"
    current_record = json.loads(
        current_evidence_path.read_text(encoding="utf-8")
    )
    receipt = json.loads(completed.stdout)
    assert receipt["evidence_path"] == current_record["evidence_path"]
    assert receipt["evidence_path"] != stale_record["evidence_path"]
    assert receipt["run_id"] == current_record["run_id"]
    assert receipt["run_id"] != stale_record["run_id"]
    assert receipt["controller_binding"]["evidence_path"] == (
        current_record["evidence_path"]
    )
    _assert_wrapper_controller_binding(receipt, current_record)


def test_powershell_51_wrapper_accepts_subst_aliases_by_filesystem_identity(
    tmp_path,
):
    wrapper = ROOT / "scripts" / "testing" / "tools" / "invoke_guarded_command.ps1"
    subst_root = tmp_path / "subst-root"
    physical_working_directory = subst_root / "working"
    physical_working_directory.mkdir(parents=True)
    driver = tmp_path / "subst-driver.ps1"
    with _owned_subst(subst_root) as owned_drive:
        substituted_root = Path(f"{owned_drive}:\\")
        substituted_working_directory = substituted_root / "working"
        substituted_evidence_root = substituted_root / "evidence"
        program = (
            "import pathlib;"
            "print(str(pathlib.Path.cwd()),flush=True)"
        )
        driver.write_text(
            "\n".join(
                [
                    "$ErrorActionPreference = 'Stop'",
                    "$childCommand = @(",
                    f"  {_ps_literal(sys.executable)},",
                    "  '-c',",
                    f"  {_ps_literal(program)}",
                    ")",
                    (
                        f"& {_ps_literal(str(wrapper))} "
                        f"-Label 'subst-filesystem-identity' "
                        "-WorkingDirectory "
                        f"{_ps_literal(str(substituted_working_directory))} "
                        "-EvidenceRoot "
                        f"{_ps_literal(str(substituted_evidence_root))} "
                        "-ExpectedExit Zero -TimeoutSeconds 5 "
                        f"{_ps_runtime_arguments()} "
                        "-Command $childCommand"
                    ),
                ]
            )
            + "\n",
            encoding="utf-8",
        )
        completed = subprocess.run(
            [
                "powershell.exe",
                "-NoLogo",
                "-NoProfile",
                "-NonInteractive",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                str(driver),
            ],
            cwd=ROOT,
            text=True,
            capture_output=True,
            timeout=20,
        )
        assert completed.returncode == 0, (
            "wrapper rejected equivalent subst and physical locations:\n"
            f"{completed.stderr}"
        )
        physical_evidence_root = subst_root / "evidence"
        evidence_path = (
            physical_evidence_root / "subst-filesystem-identity.json"
        )
        log_path = physical_evidence_root / "subst-filesystem-identity.log"
        assert evidence_path.is_file()
        assert log_path.is_file()
        record = json.loads(evidence_path.read_text(encoding="utf-8"))
        assert record["valid"] is True
        assert os.path.samefile(
            substituted_working_directory,
            record["working_directory"],
        )
        assert os.path.samefile(
            substituted_evidence_root / evidence_path.name,
            record["evidence_path"],
        )
        assert os.path.samefile(
            substituted_evidence_root / log_path.name,
            record["log_path"],
        )
        _assert_path_provenance(
            record,
            working_directory=substituted_working_directory,
            log_path=substituted_evidence_root / log_path.name,
            evidence_path=substituted_evidence_root / evidence_path.name,
        )
        receipt = json.loads(completed.stdout)
        assert receipt["schema"] == (
            "official-openvino-wrapper-verification/v1"
        )
        assert receipt["valid"] is True
        assert receipt["working_directory"] == record["working_directory"]
        assert receipt["log_path"] == record["log_path"]
        assert receipt["evidence_path"] == record["evidence_path"]
        assert receipt["requested_path_provenance"] == (
            record["requested_path_provenance"]
        )
        assert receipt["path_identity_verified"] == (
            record["path_identity_verified"]
        )
        _assert_wrapper_controller_binding(receipt, record)
        _assert_memory_evidence(record)
        _assert_zero_survivors(record)


def test_powershell_wrapper_forwards_configured_ram_floor(tmp_path):
    wrapper = ROOT / "scripts" / "testing" / "tools" / "invoke_guarded_command.ps1"
    evidence_root = tmp_path / "configured-ram-floor-evidence"
    driver = tmp_path / "configured-ram-floor-driver.ps1"
    requested_mib = 4096
    _write_wrapper_driver(
        driver,
        wrapper=wrapper,
        label="configured-ram-floor",
        working_directory=ROOT,
        evidence_root=evidence_root,
        program="print('ram-floor',flush=True)",
        minimum_available_ram_mib=requested_mib,
    )
    completed = _invoke_wrapper_driver(driver)
    assert completed.returncode == 0, completed.stderr
    record = json.loads(
        (evidence_root / "configured-ram-floor.json").read_text(
            encoding="utf-8"
        )
    )
    assert record["configured_minimum_available_ram_bytes"] == (
        requested_mib * 1024 * 1024
    )


def test_powershell_wrapper_persists_exact_verification_stdout(tmp_path):
    wrapper = ROOT / "scripts" / "testing" / "tools" / "invoke_guarded_command.ps1"
    evidence_root = tmp_path / "verification-output-evidence"
    verification_path = (
        evidence_root / "verification-output.verification.json"
    )
    driver = tmp_path / "verification-output-driver.ps1"
    _write_wrapper_driver(
        driver,
        wrapper=wrapper,
        label="verification-output",
        working_directory=ROOT,
        evidence_root=evidence_root,
        program="print('verification-output',flush=True)",
        verification_output_path=verification_path,
    )
    completed = _invoke_wrapper_driver(driver)
    assert completed.returncode == 0, completed.stderr
    assert verification_path.is_file()
    assert verification_path.read_text(encoding="utf-8") == (
        completed.stdout.strip()
    )
    verification = json.loads(verification_path.read_text(encoding="utf-8"))
    record = json.loads(
        (evidence_root / "verification-output.json").read_text(
            encoding="utf-8"
        )
    )
    assert verification["schema"] == (
        "official-openvino-wrapper-verification/v1"
    )
    assert verification["run_id"] == record["run_id"]
    _assert_wrapper_controller_binding(verification, record)
    assert record["valid"] is True


def test_guard_primitives_have_one_owner_and_legacy_modules_reexport():
    guard_path = (
        ROOT
        / "scripts"
        / "testing"
        / "campaigns"
        / "openvino"
        / "owned_process_guard.py"
    )
    capability_path = (
        ROOT / "scripts" / "testing" / "tools" / "run_openvino_reference_capability.py"
    )
    measurement_path = ROOT / "scripts/testing/campaigns/llama_cpp/measure_run.py"
    sources = {
        path: path.read_text(encoding="utf-8")
        for path in (guard_path, capability_path, measurement_path)
    }
    binding_owners = [
        path
        for path, source in sources.items()
        if "CreateJobObjectW.argtypes" in source
    ]
    class_owners = [
        path for path, source in sources.items() if "class KillOnCloseJob" in source
    ]
    assert binding_owners == [guard_path]
    assert class_owners == [guard_path]

    sys.path.insert(0, str(SCRIPT_DIR))
    try:
        owned = importlib.import_module(
            "scripts.testing.campaigns.openvino.owned_process_guard"
        )
        guard = importlib.import_module(
            "scripts.testing.campaigns.openvino.guarded_build"
        )
        capability = importlib.import_module("run_openvino_reference_capability")
        measurement = importlib.import_module(
            "scripts.testing.campaigns.llama_cpp.measure_run"
        )
    finally:
        sys.path.remove(str(SCRIPT_DIR))
    assert guard.owned_process_guard is owned
    assert capability.owned_process_guard is owned
    assert measurement.owned_process_guard is owned
    assert guard.CREATE_SUSPENDED == 0x00000004
    assert guard.CREATE_SUSPENDED is owned.CREATE_SUSPENDED
    assert capability.CREATE_SUSPENDED is owned.CREATE_SUSPENDED
    assert capability.KillOnCloseJob is owned.KillOnCloseJob
    assert capability.available_ram_bytes is owned.available_ram_bytes
    assert capability.process_memory_bytes is owned.process_memory_bytes
    assert measurement.available_ram_bytes is owned.available_ram_bytes
    assert measurement.process_memory_bytes is owned.process_memory_bytes
    assert measurement.process_tree_pids is owned.process_tree_pids
    assert measurement.process_tree_working_set_bytes is (
        owned.process_tree_working_set_bytes
    )
    owners = [
        name
        for name, module in sys.modules.items()
        if getattr(module, "__file__", None)
        and Path(module.__file__).resolve() == guard_path.resolve()
    ]
    assert owners == [
        "scripts.testing.campaigns.openvino.owned_process_guard"
    ]
