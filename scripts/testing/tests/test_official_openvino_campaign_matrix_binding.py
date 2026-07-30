import copy

import pytest

from scripts.testing.measure_official_openvino import (
    validate_runtime_record_against_matrix_case,
    validate_worker_spec_against_matrix_case,
)


def _case(**overrides):
    value = {
        "test_id": "OV-TQ-03",
        "phase": "formal",
        "model": "granite-3b",
        "weight_precision": "u8",
        "device": "cpu",
        "contexts": [4096],
        "k_algorithm": "tbq4",
        "v_algorithm": "tbq4",
        "k_precision": "u4",
        "v_precision": "u4",
    }
    value.update(overrides)
    algorithms = {value["k_algorithm"], value["v_algorithm"]}
    value.update({
        "key_cache_precision": value["k_precision"],
        "value_cache_precision": value["v_precision"],
        "requested_device": value["device"].upper(),
        "runtime_key_algorithm": (
            "STANDARD"
            if value["k_algorithm"] in {"standard", "scalar", "frozen"}
            else value["k_algorithm"].upper()
        ),
        "runtime_value_algorithm": (
            "STANDARD"
            if value["v_algorithm"] in {"standard", "scalar", "frozen"}
            else value["v_algorithm"].upper()
        ),
        "norm_correction": bool(algorithms & {"tbq3", "tbq4"}),
        "attention_path": (
            "stateful_sdpa_reference_codec"
            if algorithms & {"tbq3", "tbq4"}
            else "stateful_sdpa_standard"
        ),
        "execution_route": (
            "upstream-scalar" if algorithms == {"scalar"} else "patched-stateful"
        ),
        "expected_outcome": "pass",
        "suitable_host_required": False,
        "numeric_generation_metrics_expected": True,
    })
    value.update({
        key: overrides[key]
        for key in {
            "key_cache_precision", "value_cache_precision", "requested_device",
            "runtime_key_algorithm", "runtime_value_algorithm", "norm_correction",
            "attention_path", "execution_route", "expected_outcome",
            "suitable_host_required", "numeric_generation_metrics_expected",
        }
        & overrides.keys()
    })
    return value


def _spec(**overrides):
    value = {
        "controlled_test_id": "OV-TQ-03",
        "context": 4096,
        "device": "CPU",
        "properties": {
            "TURBOQUANT_KEY_ALGORITHM": "TBQ4",
            "TURBOQUANT_VALUE_ALGORITHM": "TBQ4",
            "TURBOQUANT_NORM_CORRECTION": True,
        },
    }
    value.update(overrides)
    return value


def _record(**activation_overrides):
    activation = {
        "status": "activated",
        "requested_key_algorithm": "TBQ4",
        "requested_value_algorithm": "TBQ4",
        "activated_key_algorithm": "TBQ4",
        "activated_value_algorithm": "TBQ4",
        "requested_key_cache_precision": "u4",
        "requested_value_cache_precision": "u4",
        "activated_key_cache_precision": "u4",
        "activated_value_cache_precision": "u4",
        "observed_key_state_precision": "u8+f32+i32",
        "observed_value_state_precision": "u8+f32+i32",
        "norm_correction": True,
        "attention_path": "stateful_sdpa_reference_codec",
        "device": "CPU",
        "actual_device": "CPU",
        "fallback": False,
    }
    activation.update(activation_overrides)
    return {"activation": activation}


def test_worker_spec_is_bound_to_exact_matrix_algorithms_precision_and_norm():
    validate_worker_spec_against_matrix_case(_spec(), _case())

    tampered = _spec()
    tampered["properties"]["TURBOQUANT_VALUE_ALGORITHM"] = "TBQ3"
    with pytest.raises(ValueError, match="value algorithm"):
        validate_worker_spec_against_matrix_case(tampered, _case())

    tampered = _spec()
    tampered["properties"]["TURBOQUANT_NORM_CORRECTION"] = False
    with pytest.raises(ValueError, match="norm correction"):
        validate_worker_spec_against_matrix_case(tampered, _case())


def test_runtime_activation_is_bound_to_matrix_and_rejects_fallback():
    validate_runtime_record_against_matrix_case(_record(), _case())

    with pytest.raises(ValueError, match="activated value algorithm"):
        validate_runtime_record_against_matrix_case(
            _record(activated_value_algorithm="TBQ3"),
            _case(),
        )
    with pytest.raises(ValueError, match="fallback"):
        validate_runtime_record_against_matrix_case(
            _record(fallback=True),
            _case(),
        )


def test_campaign_binding_consumes_the_frozen_explicit_contract_not_a_rederivation():
    case = _case()
    validate_worker_spec_against_matrix_case(_spec(), case)
    validate_runtime_record_against_matrix_case(_record(), case)

    for field, value, message, validator in (
        ("runtime_value_algorithm", "TBQ3", "value algorithm", validate_worker_spec_against_matrix_case),
        ("norm_correction", False, "norm correction", validate_worker_spec_against_matrix_case),
        ("attention_path", "stateful_sdpa_standard", "attention path", validate_runtime_record_against_matrix_case),
        ("expected_outcome", "expected-rejection", "expected outcome", validate_worker_spec_against_matrix_case),
        ("execution_route", "non-runtime", "execution route", validate_worker_spec_against_matrix_case),
        ("numeric_generation_metrics_expected", False, "numeric generation metrics", validate_worker_spec_against_matrix_case),
    ):
        tampered = copy.deepcopy(case)
        tampered[field] = value
        with pytest.raises(ValueError, match=message):
            if validator is validate_worker_spec_against_matrix_case:
                validator(_spec(), tampered)
            else:
                validator(_record(), tampered)


def test_scalar_control_requires_concrete_scalar_state_not_standard_relabel():
    case = _case(
        test_id="OV-TQ-01",
        k_algorithm="scalar",
        v_algorithm="scalar",
        k_precision="u8",
        v_precision="u8",
    )
    spec = _spec(
        controlled_test_id="OV-TQ-01",
        properties={
            "KEY_CACHE_PRECISION": "u8",
            "VALUE_CACHE_PRECISION": "u8",
        },
    )
    validate_worker_spec_against_matrix_case(spec, case)

    scalar = _record(
        status="not_requested",
        requested_key_algorithm="STANDARD",
        requested_value_algorithm="STANDARD",
        activated_key_algorithm="STANDARD",
        activated_value_algorithm="STANDARD",
        requested_key_cache_precision="u8",
        requested_value_cache_precision="u8",
        activated_key_cache_precision="u8",
        activated_value_cache_precision="u8",
        observed_key_state_precision="u8",
        observed_value_state_precision="u8",
        norm_correction=False,
        attention_path="stateful_sdpa_standard",
    )
    validate_runtime_record_against_matrix_case(scalar, case)

    relabelled = copy.deepcopy(scalar)
    relabelled["activation"]["observed_key_state_precision"] = "f32"
    relabelled["activation"]["observed_value_state_precision"] = "f32"
    with pytest.raises(ValueError, match="scalar cache precision"):
        validate_runtime_record_against_matrix_case(relabelled, case)


def test_mixed_turboquant_standard_side_preserves_observed_precision_separately():
    case = _case(
        test_id="OV-TQ-07",
        k_algorithm="tbq4",
        v_algorithm="standard",
        k_precision="u4",
        v_precision="f16",
    )
    spec = _spec(
        controlled_test_id="OV-TQ-07",
        properties={
            "TURBOQUANT_KEY_ALGORITHM": "TBQ4",
            "TURBOQUANT_VALUE_ALGORITHM": "STANDARD",
            "TURBOQUANT_NORM_CORRECTION": True,
            "VALUE_CACHE_PRECISION": "f16",
        },
    )
    validate_worker_spec_against_matrix_case(spec, case)

    mixed = _record(
        requested_value_algorithm="STANDARD",
        activated_value_algorithm="STANDARD",
        requested_value_cache_precision="f16",
        activated_value_cache_precision="f16",
        observed_value_state_precision="f32",
    )
    validate_runtime_record_against_matrix_case(mixed, case)
