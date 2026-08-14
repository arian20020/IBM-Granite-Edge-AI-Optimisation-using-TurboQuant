"""Import the exact C1 conversion modules in a new Python process."""

from __future__ import annotations

import importlib
import importlib.metadata
import json

from scripts.testing.workbook05.phase3.dependency_preflight import IMPORT_MODULES


def main() -> int:
    """Import every required module and print stable package observations."""

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
        observations[module_name] = {
            "module_file": str(getattr(module, "__file__", "")),
            "package": package_name,
            "version": importlib.metadata.version(package_name),
        }

    print(json.dumps(observations, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
