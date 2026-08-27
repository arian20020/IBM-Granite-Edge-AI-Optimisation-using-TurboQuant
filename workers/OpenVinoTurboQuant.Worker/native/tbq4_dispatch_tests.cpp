#include <algorithm>
#include <cmath>
#include <cstdint>
#include <iostream>
#include <stdexcept>
#include <string>
#include <vector>

#include <openvino/openvino.hpp>
#include <openvino/opsets/opset13.hpp>

namespace {

constexpr size_t kHeadDimension = 64;

void require(bool condition, const char* message) {
    if (!condition) {
        throw std::runtime_error(message);
    }
}

ov::AnyMap exact_tbq4() {
    return {
        {"KEY_CACHE_PRECISION", "u4"},
        {"VALUE_CACHE_PRECISION", "u4"},
        {"KEY_CACHE_QUANT_ALG", "TURBO"},
        {"VALUE_CACHE_QUANT_ALG", "TURBO"},
    };
}

ov::AnyMap scalar_u4() {
    return {
        {"KEY_CACHE_PRECISION", "u4"},
        {"VALUE_CACHE_PRECISION", "u4"},
        {"KEY_CACHE_QUANT_ALG", "SCALAR"},
        {"VALUE_CACHE_QUANT_ALG", "SCALAR"},
    };
}

std::shared_ptr<ov::Model> probe_model() {
    auto input = std::make_shared<ov::op::v0::Parameter>(ov::element::f32, ov::Shape{1});
    auto result = std::make_shared<ov::op::v0::Result>(input);
    return std::make_shared<ov::Model>(ov::ResultVector{result}, ov::ParameterVector{input}, "tbq4_config_probe");
}

std::shared_ptr<ov::Model> stateful_sdpa_model() {
    const ov::PartialShape current_shape{1, 1, -1, kHeadDimension};
    const ov::PartialShape past_shape{1, 1, -1, kHeadDimension};

    auto q = std::make_shared<ov::opset13::Parameter>(ov::element::f32, current_shape);
    auto k = std::make_shared<ov::opset13::Parameter>(ov::element::f32, current_shape);
    auto v = std::make_shared<ov::opset13::Parameter>(ov::element::f32, current_shape);
    auto initial_k = std::make_shared<ov::opset13::Parameter>(ov::element::f32, past_shape);
    auto initial_v = std::make_shared<ov::opset13::Parameter>(ov::element::f32, past_shape);
    auto beam = std::make_shared<ov::opset13::Parameter>(ov::element::i32, ov::PartialShape{-1});
    q->set_friendly_name("q");
    k->set_friendly_name("k");
    v->set_friendly_name("v");
    initial_k->set_friendly_name("initial_k");
    initial_v->set_friendly_name("initial_v");
    beam->set_friendly_name("beam_idx");

    auto key_variable = std::make_shared<ov::op::util::Variable>(
        ov::op::util::VariableInfo{past_shape, ov::element::f32, "past_key"});
    auto value_variable = std::make_shared<ov::op::util::Variable>(
        ov::op::util::VariableInfo{past_shape, ov::element::f32, "past_value"});
    auto past_k = std::make_shared<ov::opset13::ReadValue>(initial_k, key_variable);
    auto past_v = std::make_shared<ov::opset13::ReadValue>(initial_v, value_variable);
    auto axis_zero = ov::opset13::Constant::create(ov::element::i32, {}, {0});
    auto gathered_k = std::make_shared<ov::opset13::Gather>(past_k, beam, axis_zero);
    auto gathered_v = std::make_shared<ov::opset13::Gather>(past_v, beam, axis_zero);
    auto concat_k = std::make_shared<ov::opset13::Concat>(ov::OutputVector{gathered_k, k}, 2);
    auto concat_v = std::make_shared<ov::opset13::Concat>(ov::OutputVector{gathered_v, v}, 2);
    auto attention = std::make_shared<ov::opset13::ScaledDotProductAttention>(q, concat_k, concat_v, false);
    attention->set_friendly_name("mha");
    auto assign_k = std::make_shared<ov::opset13::Assign>(concat_k, key_variable);
    auto assign_v = std::make_shared<ov::opset13::Assign>(concat_v, value_variable);

    return std::make_shared<ov::Model>(
        ov::ResultVector{std::make_shared<ov::opset13::Result>(attention)},
        ov::SinkVector{assign_k, assign_v},
        ov::ParameterVector{q, k, v, initial_k, initial_v, beam},
        "tbq4_stateful_sdpa");
}

ov::Tensor f32_tensor(const ov::Shape& shape, int seed) {
    ov::Tensor tensor(ov::element::f32, shape);
    auto* data = tensor.data<float>();
    for (size_t i = 0; i < tensor.get_size(); ++i) {
        const auto phase = static_cast<float>((i * 17 + static_cast<size_t>(seed) * 29) % 101);
        data[i] = std::sin(phase * 0.071F) * 0.2F;
    }
    return tensor;
}

std::vector<float> run_two_turns(const ov::AnyMap& config) {
    ov::Core core;
    auto compiled = core.compile_model(stateful_sdpa_model(), "CPU", config);
    bool fused = false;
    for (const auto& node : compiled.get_runtime_model()->get_ops()) {
        if ((node->get_friendly_name() == "mha" || node->get_friendly_name().find("mha") != std::string::npos) &&
            node->get_output_size() == 3) {
            fused = true;
            break;
        }
    }
    require(fused, "stateful SDPA was not retained as a fused CPU execution node");

    auto request = compiled.create_infer_request();
    for (int turn = 0; turn < 2; ++turn) {
        const size_t tokens = turn == 0 ? 10 : 1;
        request.set_input_tensor(0, f32_tensor({1, 1, tokens, kHeadDimension}, 10 + turn));
        request.set_input_tensor(1, f32_tensor({1, 1, tokens, kHeadDimension}, 20 + turn));
        request.set_input_tensor(2, f32_tensor({1, 1, tokens, kHeadDimension}, 30 + turn));
        request.set_input_tensor(3, f32_tensor({1, 1, 0, kHeadDimension}, 40 + turn));
        request.set_input_tensor(4, f32_tensor({1, 1, 0, kHeadDimension}, 50 + turn));
        ov::Tensor beam(ov::element::i32, {1});
        beam.data<int32_t>()[0] = 0;
        request.set_input_tensor(5, beam);
        request.infer();
    }

    const auto output = request.get_output_tensor();
    const auto* values = output.data<const float>();
    std::vector<float> copy(values, values + output.get_size());
    require(std::all_of(copy.begin(), copy.end(), [](float value) { return std::isfinite(value); }),
            "stateful SDPA produced a non-finite value");
    return copy;
}

void exact_configuration_executes_stateful_sdpa() {
    const auto turbo = run_two_turns(exact_tbq4());
    const auto scalar = run_two_turns(scalar_u4());
    require(turbo.size() == scalar.size() && !turbo.empty(), "stateful SDPA output shape mismatch");
    float maximum_error = 0.0F;
    for (size_t i = 0; i < turbo.size(); ++i) {
        maximum_error = std::max(maximum_error, std::abs(turbo[i] - scalar[i]));
    }
    require(maximum_error <= 0.10F, "TurboQuant output exceeded the scalar-u4 conformance bound");
}

void forced_disabled_configuration_is_not_turbo() {
    (void)run_two_turns(scalar_u4());

    ov::Core core;
    bool rejected = false;
    try {
        auto malformed = exact_tbq4();
        malformed["KEY_CACHE_QUANT_ALG"] = std::string("DISABLED");
        (void)core.compile_model(probe_model(), "CPU", malformed);
    } catch (const ov::Exception&) {
        rejected = true;
    }
    require(rejected, "unknown TurboQuant dispatch algorithm was accepted");
}

void malformed_precision_is_rejected() {
    ov::Core core;
    bool rejected = false;
    try {
        auto malformed = exact_tbq4();
        malformed["KEY_CACHE_PRECISION"] = std::string("u5");
        (void)core.compile_model(probe_model(), "CPU", malformed);
    } catch (const ov::Exception&) {
        rejected = true;
    }
    require(rejected, "malformed cache precision was accepted");
}

}  // namespace

int main() {
    try {
        exact_configuration_executes_stateful_sdpa();
        forced_disabled_configuration_is_not_turbo();
        malformed_precision_is_rejected();
        std::cout << "tbq4_dispatch_tests_passed\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
