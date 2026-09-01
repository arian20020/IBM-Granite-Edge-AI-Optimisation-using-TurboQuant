import hashlib

import pytest

from scripts.testing.campaigns.openvino.workload import build_context_workload


@pytest.mark.parametrize("context", [256, 512, 1024, 2048, 4096, 8192])
def test_context_workload_is_deterministic_and_declares_exact_prefill(context):
    workload = build_context_workload(context)

    assert workload["prompt"] == " test" * context
    assert workload["expected_input_tokens"] == context
    assert workload["context"] == context
    assert workload["prompt_sha256"] == hashlib.sha256(
        workload["prompt"].encode("utf-8")
    ).hexdigest()
    assert workload["strategy"] == "granite-single-token-test-prefix/v1"


@pytest.mark.parametrize("context", [0, -1, True, 255, 8193])
def test_context_workload_rejects_out_of_contract_context(context):
    with pytest.raises(ValueError, match="context"):
        build_context_workload(context)
