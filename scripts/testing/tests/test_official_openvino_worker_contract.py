import pytest

from scripts.testing.official_openvino.measurement_worker import (
    validate_input_token_count,
)


def test_formal_worker_requires_exact_context_token_count():
    assert validate_input_token_count(4096, 4096) == 4096


@pytest.mark.parametrize("actual", [4095, 4097])
def test_formal_worker_rejects_context_token_mismatch(actual):
    with pytest.raises(RuntimeError, match="input token count"):
        validate_input_token_count(actual, 4096)
