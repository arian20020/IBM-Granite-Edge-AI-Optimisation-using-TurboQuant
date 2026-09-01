"""Deterministic prefill workloads for the controlled WB-04 context screen."""

from __future__ import annotations

import hashlib
from typing import Any


MIN_CONTEXT = 256
MAX_CONTEXT = 8192
STRATEGY = "granite-single-token-test-prefix/v1"


def build_context_workload(context: int) -> dict[str, Any]:
    """Build a stable prompt that the pinned Granite tokenizer maps 1:1.

    The leading-space ``test`` token is one token under the pinned Granite
    tokenizer. The runtime worker still verifies the measured input-token
    count, so a tokenizer or artifact change fails closed.
    """

    if (
        isinstance(context, bool)
        or not isinstance(context, int)
        or not MIN_CONTEXT <= context <= MAX_CONTEXT
    ):
        raise ValueError(
            f"context must be an integer from {MIN_CONTEXT} through {MAX_CONTEXT}"
        )
    prompt = " test" * context
    return {
        "strategy": STRATEGY,
        "context": context,
        "expected_input_tokens": context,
        "prompt": prompt,
        "prompt_sha256": hashlib.sha256(prompt.encode("utf-8")).hexdigest(),
    }
