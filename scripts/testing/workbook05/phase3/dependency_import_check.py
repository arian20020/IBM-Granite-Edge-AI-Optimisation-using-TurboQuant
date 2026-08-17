"""Import the exact C1 conversion modules in a fresh final-Python process."""

from __future__ import annotations

import argparse
import importlib
import importlib.metadata
import json
import os
from pathlib import Path, PureWindowsPath
from typing import Any, Mapping, Sequence

from scripts.testing.workbook05.phase3.dependency_preflight import IMPORT_MODULES


def _windows_child(root: Path, candidate_text: str, module_name: str) -> str:
    root_path = PureWindowsPath(str(root))
    candidate = PureWindowsPath(candidate_text)
    if not root_path.is_absolute() or not candidate.is_absolute():
        raise ValueError(
            f"Imported module path must be an absolute Windows path: {module_name}"
        )
    root_parts = tuple(part.casefold() for part in root_path.parts)
    candidate_parts = tuple(part.casefold() for part in candidate.parts)
    if (
        len(candidate_parts) <= len(root_parts)
        or candidate_parts[: len(root_parts)] != root_parts
    ):
        raise ValueError(
            f"Imported module escaped the expected final environment: {module_name}"
        )
    return str(candidate)


def collect_import_observations(
    expected_environment_root: Path | None = None,
) -> dict[str, dict[str, str]]:
    """Import every required module and bind it to the final environment."""

    observations: dict[str, dict[str, str]] = {}
    for module_name in IMPORT_MODULES:
        module = importlib.import_module(module_name)
        top_level = module_name.split(".", maxsplit=1)[0]
        package_name = {
            "optimum": "optimum",
            "transformers": "transformers",
            "nncf": "nncf",
            "openvino": "openvino",
        }[top_level]
        module_file = str(getattr(module, "__file__", ""))
        if not module_file:
            raise ValueError(f"Imported module has no file identity: {module_name}")
        if expected_environment_root is not None:
            module_file = _windows_child(
                expected_environment_root,
                module_file,
                module_name,
            )
        observations[module_name] = {
            "module_file": module_file,
            "package": package_name,
            "version": importlib.metadata.version(package_name),
        }
    return observations


def _write_atomic(path: Path, value: Mapping[str, Any]) -> None:
    temporary = path.with_name(path.name + ".tmp")
    if path.exists():
        raise FileExistsError(f"Import-check output already exists: {path}")
    if temporary.exists():
        raise FileExistsError(f"Temporary import-check output exists: {temporary}")
    if not path.parent.is_dir():
        raise FileNotFoundError(f"Import-check parent does not exist: {path.parent}")
    payload = json.dumps(value, indent=2, sort_keys=True) + "\n"
    created = False
    try:
        with temporary.open("x", encoding="utf-8", newline="\n") as handle:
            created = True
            handle.write(payload)
            handle.flush()
            os.fsync(handle.fileno())
        os.rename(temporary, path)
    except Exception:
        if created and temporary.exists():
            temporary.unlink()
        raise


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--expected-environment-root", type=Path)
    parser.add_argument("--output", type=Path)
    arguments = parser.parse_args(argv)

    observations = collect_import_observations(arguments.expected_environment_root)
    if arguments.output is None:
        print(json.dumps(observations, indent=2, sort_keys=True))
    else:
        _write_atomic(arguments.output, observations)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
