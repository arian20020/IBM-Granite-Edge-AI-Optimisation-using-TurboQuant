"""Supported contributor-facing testing commands."""

from __future__ import annotations

import argparse
import importlib
import sys
from collections.abc import Callable, Mapping, Sequence
from contextlib import contextmanager


LegacyHandler = str | Callable[[], int | None]


def build_mode_parser(
    description: str | None,
    mode_handlers: Mapping[str, object],
) -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=description)
    parser.add_argument("--mode", choices=tuple(mode_handlers), required=True)
    parser.add_argument(
        "args",
        nargs=argparse.REMAINDER,
        help="arguments for the selected mode; prefix forwarded options with --",
    )
    return parser


def forwarded_args(parsed: argparse.Namespace) -> list[str]:
    values = list(parsed.args)
    if values[:1] == ["--"]:
        return values[1:]
    return values


def resolve_handler(reference: LegacyHandler) -> Callable[[], int | None]:
    if callable(reference):
        return reference
    module_name, separator, attribute = reference.partition(":")
    if separator == "" or attribute == "":
        raise ValueError(f"invalid handler reference: {reference}")
    module = importlib.import_module(module_name)
    handler = getattr(module, attribute)
    if not callable(handler):
        raise TypeError(f"handler is not callable: {reference}")
    return handler


@contextmanager
def _temporary_argv(argv: Sequence[str]):
    previous = sys.argv[:]
    sys.argv = [previous[0], *argv]
    try:
        yield
    finally:
        sys.argv = previous


def invoke_legacy_main(handler: LegacyHandler, argv: Sequence[str]) -> int:
    with _temporary_argv(argv):
        result = resolve_handler(handler)()
    return 0 if result is None else int(result)


__all__ = [
    "LegacyHandler",
    "build_mode_parser",
    "forwarded_args",
    "invoke_legacy_main",
    "resolve_handler",
]
