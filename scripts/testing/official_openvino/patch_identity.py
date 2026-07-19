"""Validation helpers for the project-patched OpenVINO GenAI source identity."""


def validate_patch_identity(record: dict, expected_base: str) -> None:
    """Reject evidence that does not identify a clean, pinned patch workspace."""
    if record.get("base_commit") != expected_base:
        raise ValueError("base commit mismatch")
    if record.get("dirty") is not False:
        raise ValueError("dirty patch workspace")
    if not record.get("patch_commit"):
        raise ValueError("patch commit missing")
