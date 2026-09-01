"""Conservative source evidence audit for the official OpenVINO KV codec boundary."""

from __future__ import annotations

from dataclasses import dataclass
import os
from pathlib import Path
import re
import subprocess
from typing import Any, Iterable, Mapping


@dataclass(frozen=True)
class SourceMatch:
    path: str
    line: int
    feature: str
    text: str


@dataclass(frozen=True)
class CodecBoundary:
    turbo_enum: bool
    key_property: bool
    value_property: bool
    u3_precision: bool
    u4_precision: bool
    norm_switch: bool
    packed_storage: bool
    cpu_sdpa_path: bool
    qjl_implementation: bool
    polar_implementation: bool
    matches: tuple[SourceMatch, ...]


@dataclass(frozen=True)
class Activation:
    activated: bool
    classification: str


def _token_patterns(*tokens: str) -> tuple[re.Pattern[str], ...]:
    return tuple(re.compile(rf"(?<!\w){re.escape(token)}(?!\w)", re.IGNORECASE)
                 for token in tokens)


_FEATURE_PATTERNS = {
    "turbo_enum": _token_patterns("tbq3", "tbq4", "turboquant"),
    "key_property": _token_patterns(
        "key_cache_precision", "key_cache_quant_mode", "key_cache_algorithm",
    ),
    "value_property": _token_patterns(
        "value_cache_precision", "value_cache_quant_mode", "value_cache_algorithm",
    ),
    "u3_precision": _token_patterns("element::u3", "precision::u3", "tbq3"),
    "u4_precision": _token_patterns("element::u4", "precision::u4", "tbq4"),
    "norm_switch": _token_patterns("cache_norm", "norm_before_quant", "normalize_cache"),
    "packed_storage": _token_patterns("packed_storage", "packed_bytes", "pack_u3", "pack_u4"),
    "cpu_sdpa_path": _token_patterns("scaled_dot_product_attention", "sdpa"),
}


def _is_source(path: Path) -> bool:
    return path.suffix.lower() in {".c", ".cc", ".cpp", ".h", ".hh", ".hpp", ".py"}


def _implementation_line(line: str) -> bool:
    stripped = line.strip()
    return bool(stripped) and not stripped.startswith(("#", "//", "/*", "*"))


def _physical_path(path: Path) -> Path:
    value = str(path.resolve())
    if os.name == "nt" and not value.startswith("\\\\?\\"):
        value = "\\\\?\\" + value
    return Path(value)


def _source_files(root: Path) -> Iterable[tuple[Path, Path]]:
    if (root / ".git").exists():
        completed = subprocess.run(
            ["git", "-c", "core.longpaths=true", "-C", str(root),
             "ls-files", "-z"],
            check=True, capture_output=True,
        )
        for relative in completed.stdout.decode("utf-8", errors="surrogateescape").split("\0"):
            if relative:
                logical = root / relative
                if _is_source(logical):
                    yield logical, _physical_path(logical)
        return
    for logical in sorted(root.rglob("*")):
        if logical.is_file() and _is_source(logical):
            yield logical, logical


def audit_codec_boundary(source_roots: Iterable[Path]) -> CodecBoundary:
    """Scan implementation files and retain line-level positive evidence."""
    matches: list[SourceMatch] = []
    flags = {feature: False for feature in _FEATURE_PATTERNS}
    qjl = False
    polar = False
    for root in source_roots:
        for path, physical_path in _source_files(root):
            try:
                lines = physical_path.read_text(encoding="utf-8", errors="replace").splitlines()
            except OSError:
                continue
            cache_context = any(part.lower() in {"cache", "kv_cache", "paged_attention"}
                                or "cache" in part.lower() for part in path.parts)
            for number, line in enumerate(lines, 1):
                if not _implementation_line(line):
                    continue
                lowered = line.lower()
                for feature, patterns in _FEATURE_PATTERNS.items():
                    if any(pattern.search(lowered) for pattern in patterns):
                        flags[feature] = True
                        matches.append(SourceMatch(str(path), number, feature, line.strip()))
                if cache_context and "qjl" in lowered:
                    qjl = True
                    matches.append(SourceMatch(str(path), number, "qjl_implementation", line.strip()))
                if cache_context and "polar" in lowered:
                    polar = True
                    matches.append(SourceMatch(str(path), number, "polar_implementation", line.strip()))
    return CodecBoundary(**flags, qjl_implementation=qjl,
                         polar_implementation=polar, matches=tuple(matches))


def classify_activation(source: CodecBoundary, runtime: Mapping[str, Any]) -> Activation:
    """Require complete source capability plus direct runtime allocation proof."""
    if runtime.get("fallback") or (
        runtime.get("requested_device")
        and runtime.get("actual_device") != runtime.get("requested_device")
    ):
        return Activation(False, "fallback-not-activated")
    source_complete = all((
        source.turbo_enum, source.key_property, source.value_property,
        source.u3_precision, source.u4_precision, source.norm_switch,
        source.packed_storage, source.cpu_sdpa_path,
    ))
    if not source_complete:
        return Activation(False, "source-boundary-incomplete")
    if not runtime.get("accepted"):
        return Activation(False, "configuration-rejected")
    if not runtime.get("allocation_proven"):
        return Activation(False, "allocation-unproven")
    return Activation(True, "activated")
