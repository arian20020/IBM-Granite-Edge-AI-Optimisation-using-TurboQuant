"""Import the exact C1 conversion modules in a fresh final-Python process."""

from __future__ import annotations

import argparse
import importlib
import importlib.metadata
import json
import os
from pathlib import Path
from typing import Any, Mapping, Sequence

from scripts.testing.workbook05.phase3.dependency_import_identity import (
    observe_module_identity,
)
from scripts.testing.workbook05.phase3.dependency_preflight import IMPORT_MODULES


def collect_import_observations(
    expected_environment_root: Path | None = None,
) -> dict[str, dict[str, Any]]:
    """Import every required module and bind it to the final environment."""

    observations: dict[str, dict[str, Any]] = {}
    for module_name in IMPORT_MODULES:
        # Import only the exact reviewed catalogue. The helper below then binds
        # either a conventional module file or every PEP 420 namespace location.
        module = importlib.import_module(module_name)
        top_level = module_name.split(".", maxsplit=1)[0]
        package_name = {
            "optimum": "optimum",
            "transformers": "transformers",
            "nncf": "nncf",
            "openvino": "openvino",
        }[top_level]

        if expected_environment_root is None:
            # The command-line live boundary always supplies a root. This branch
            # remains available for diagnostic use while still requiring a real
            # import identity rather than converting ``None`` to text.
            module_file = getattr(module, "__file__", None)
            if not isinstance(module_file, str) or not module_file:
                raise ValueError(
                    f"Imported module has no concrete file identity without an "
                    f"expected environment root: {module_name}"
                )
            module_kind = "file"
            module_locations = [module_file]
        else:
            identity = observe_module_identity(
                module,
                expected_environment_root,
                module_name,
            )
            module_file = identity.primary_location
            module_kind = identity.kind
            module_locations = list(identity.locations)

        # Preserve ``module_file`` for compatibility with the existing evidence
        # record while explicitly exposing namespace kind and all locations.
        observations[module_name] = {
            "module_file": module_file,
            "module_kind": module_kind,
            "module_locations": module_locations,
            "package": package_name,
            "version": importlib.metadata.version(package_name),
        }
    return observations


def _write_atomic(path: Path, value: Mapping[str, Any]) -> None:
    """Write the import report once, using a sibling temporary file."""

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
    """Run the fresh-process import check and optionally retain its JSON."""

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
