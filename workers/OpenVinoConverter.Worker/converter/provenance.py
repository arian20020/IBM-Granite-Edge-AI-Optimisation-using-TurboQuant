"""Sanitized converter identity helpers; paths and environment never cross IPC."""

from __future__ import annotations

from importlib.metadata import PackageNotFoundError, version

_PACKAGES = (
    "optimum-intel",
    "optimum",
    "transformers",
    "openvino",
    "openvino-genai",
    "nncf",
)


def package_versions() -> dict[str, str]:
    result: dict[str, str] = {}
    for package in _PACKAGES:
        try:
            result[package] = version(package)
        except PackageNotFoundError:
            result[package] = "unavailable"
    return result


def fixed_options(weight_precision: str) -> dict[str, object]:
    return {
        "library": "transformers",
        "localFilesOnly": True,
        "task": "text-generation-with-past",
        "trustRemoteCode": False,
        "weightFormat": weight_precision,
    }
