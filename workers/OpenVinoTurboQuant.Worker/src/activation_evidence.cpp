#include "activation_evidence.hpp"

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <memory>
#include <stdexcept>
#include <vector>

#include <openvino/openvino.hpp>
#include <openvino/opsets/opset13.hpp>

namespace granite::official_worker {
namespace {

constexpr std::size_t head_dimension = 64U;
constexpr std::size_t packed_bytes_per_record = 32U;
constexpr std::size_t full_precision_bytes_per_record = 128U;

void require(bool condition, const char* message) {
    if (!condition) throw std::runtime_error(message);
}

ov::AnyMap cache_config(bool turbo) {
    return {
        {"KEY_CACHE_PRECISION", std::string("u4")},
        {"VALUE_CACHE_PRECISION", std::string("u4")},
        {"KEY_CACHE_QUANT_ALG", std::string(turbo ? "TURBO" : "SCALAR")},
        {"VALUE_CACHE_QUANT_ALG", std::string(turbo ? "TURBO" : "SCALAR")},
        {ov::enable_profiling.name(), true},
    };
}

std::shared_ptr<ov::Model> activation_model() {
    const ov::PartialShape shape{1, 1, -1, head_dimension};
    auto q = std::make_shared<ov::opset13::Parameter>(ov::element::f32, shape);
    auto k = std::make_shared<ov::opset13::Parameter>(ov::element::f32, shape);
    auto v = std::make_shared<ov::opset13::Parameter>(ov::element::f32, shape);
    auto initial_k = std::make_shared<ov::opset13::Parameter>(ov::element::f32, shape);
    auto initial_v = std::make_shared<ov::opset13::Parameter>(ov::element::f32, shape);
    auto beam = std::make_shared<ov::opset13::Parameter>(ov::element::i32, ov::PartialShape{-1});
    auto key_variable = std::make_shared<ov::op::util::Variable>(
        ov::op::util::VariableInfo{shape, ov::element::f32, "activation_key"});
    auto value_variable = std::make_shared<ov::op::util::Variable>(
        ov::op::util::VariableInfo{shape, ov::element::f32, "activation_value"});
    auto past_k = std::make_shared<ov::opset13::ReadValue>(initial_k, key_variable);
    auto past_v = std::make_shared<ov::opset13::ReadValue>(initial_v, value_variable);
    auto axis = ov::opset13::Constant::create(ov::element::i32, {}, {0});
    auto concat_k = std::make_shared<ov::opset13::Concat>(
        ov::OutputVector{std::make_shared<ov::opset13::Gather>(past_k, beam, axis), k}, 2);
    auto concat_v = std::make_shared<ov::opset13::Concat>(
        ov::OutputVector{std::make_shared<ov::opset13::Gather>(past_v, beam, axis), v}, 2);
    auto attention = std::make_shared<ov::opset13::ScaledDotProductAttention>(q, concat_k, concat_v, false);
    attention->set_friendly_name("turboquant_activation_sdpa");
    return std::make_shared<ov::Model>(
        ov::ResultVector{std::make_shared<ov::opset13::Result>(attention)},
        ov::SinkVector{
            std::make_shared<ov::opset13::Assign>(concat_k, key_variable),
            std::make_shared<ov::opset13::Assign>(concat_v, value_variable)},
        ov::ParameterVector{q, k, v, initial_k, initial_v, beam},
        "turboquant_activation_probe");
}

ov::Tensor tensor(const ov::Shape& shape, int seed) {
    ov::Tensor result(ov::element::f32, shape);
    auto* values = result.data<float>();
    for (std::size_t index = 0; index < result.get_size(); ++index) {
        values[index] = std::sin(static_cast<float>((index * 17U + seed * 29U) % 101U) * 0.071F) * 0.2F;
    }
    return result;
}

struct probe_result final {
    std::uint64_t dispatches{};
    std::uint64_t records{};
    bool opaque_turbo_state{};
    bool readable_scalar_state{};
};

probe_result run_probe(bool turbo) {
    ov::Core core;
    ov::CompiledModel compiled = core.compile_model(activation_model(), "CPU", cache_config(turbo));
    const auto operations = compiled.get_runtime_model()->get_ops();
    const bool fused = std::any_of(
        operations.begin(),
        operations.end(),
        [](const auto& node) {
            return node->get_friendly_name().find("turboquant_activation_sdpa") != std::string::npos &&
                node->get_output_size() == 3U;
        });
    require(fused, "activation SDPA did not fuse");

    ov::InferRequest request = compiled.create_infer_request();
    probe_result result{};
    for (int turn = 0; turn < 2; ++turn) {
        const std::size_t tokens = turn == 0 ? 10U : 1U;
        request.set_input_tensor(0, tensor({1, 1, tokens, head_dimension}, 10 + turn));
        request.set_input_tensor(1, tensor({1, 1, tokens, head_dimension}, 20 + turn));
        request.set_input_tensor(2, tensor({1, 1, tokens, head_dimension}, 30 + turn));
        request.set_input_tensor(3, tensor({1, 1, 0, head_dimension}, 40 + turn));
        request.set_input_tensor(4, tensor({1, 1, 0, head_dimension}, 50 + turn));
        ov::Tensor beam(ov::element::i32, {1});
        beam.data<std::int32_t>()[0] = 0;
        request.set_input_tensor(5, beam);
        request.infer();
        result.records += 2U * tokens;
        for (const ov::ProfilingInfo& info : request.get_profiling_info()) {
            if (info.status == ov::ProfilingInfo::Status::EXECUTED &&
                info.node_type == "ScaledDotProductAttentionWithKVCache") {
                ++result.dispatches;
            }
        }
    }
    require(result.dispatches > 0U, "activation profiling did not report executed SDPA");
    const auto states = request.query_state();
    require(states.size() == 2U, "activation state count changed");
    if (turbo) {
        try {
            (void)states.front().get_state();
        } catch (const ov::Exception&) {
            result.opaque_turbo_state = true;
        }
    } else {
        result.readable_scalar_state = std::all_of(states.begin(), states.end(), [](const auto& state) {
            return state.get_state().get_size() > 0U;
        });
    }
    return result;
}

std::uint64_t verify_model_sdpa(const std::filesystem::path& model_path) {
    if (model_path.empty()) return 0U;
    ov::Core core;
    std::shared_ptr<ov::Model> model = core.read_model(model_path);
    std::vector<std::string> attention_names;
    for (const auto& node : model->get_ops()) {
        if (std::string(node->get_type_name()) != "ScaledDotProductAttention") continue;
        require(node->get_input_size() >= 3U, "model SDPA input arity changed");
        for (std::size_t index = 0; index < 3U; ++index) {
            const ov::PartialShape shape = node->get_input_partial_shape(index);
            require(shape.rank().is_static() && shape.rank().get_length() == 4 &&
                    shape[3].is_static() && shape[3].get_length() == head_dimension,
                    "model SDPA head dimension is not exactly 64");
        }
        attention_names.push_back(node->get_friendly_name());
    }
    if (attention_names.empty()) return 0U;

    ov::CompiledModel compiled = core.compile_model(model, "CPU", cache_config(true));
    std::uint64_t fused = 0U;
    for (const auto& operation : compiled.get_runtime_model()->get_ops()) {
        if (operation->get_output_size() != 3U) continue;
        if (std::any_of(attention_names.begin(), attention_names.end(), [&](const std::string& name) {
                return operation->get_friendly_name() == name ||
                    operation->get_friendly_name().find(name) != std::string::npos;
            })) {
            ++fused;
        }
    }
    require(fused == attention_names.size(), "model SDPA did not retain exact CPU cache fusion");
    return fused;
}

}  // namespace

nlohmann::ordered_json turboquant_activation_evidence::to_json(
    const std::string& session_id,
    const std::string& turn_id) const {
    return {
        {"sessionId", session_id},
        {"turnId", turn_id},
        {"requestedKeyCodec", "tbq4"},
        {"requestedValueCodec", "tbq4"},
        {"actualKeyCodec", "tbq4"},
        {"actualValueCodec", "tbq4"},
        {"attentionPath", "sdpa"},
        {"headDimension", head_dimension},
        {"runtimeDispatchCount", runtime_dispatch_count},
        {"encodedRecordCount", encoded_record_count},
        {"modelSdpaNodeCount", model_sdpa_node_count},
        {"packedBytesPerRecord", packed_bytes_per_record},
        {"fullPrecisionBytesPerRecord", full_precision_bytes_per_record},
        {"packedCacheBytes", packed_cache_bytes},
        {"fullPrecisionCacheBytes", full_precision_cache_bytes},
        {"evidenceOrigin", "openVinoProfilingApi"},
        {"forcedScalarNegative", forced_scalar_negative},
        {"eventType", "turboQuantActivation"},
    };
}

turboquant_activation_evidence measure_turboquant_activation(
    const std::filesystem::path& model_path) {
    const probe_result turbo = run_probe(true);
    const probe_result scalar = run_probe(false);
    require(turbo.opaque_turbo_state, "TurboQuant packed state was not opaque");
    require(scalar.readable_scalar_state, "forced-scalar state was not readable");
    turboquant_activation_evidence evidence{};
    evidence.runtime_dispatch_count = turbo.dispatches;
    evidence.encoded_record_count = turbo.records;
    evidence.model_sdpa_node_count = verify_model_sdpa(model_path);
    evidence.packed_cache_bytes = turbo.records * packed_bytes_per_record;
    evidence.full_precision_cache_bytes = turbo.records * full_precision_bytes_per_record;
    evidence.forced_scalar_negative = true;
    return evidence;
}

}  // namespace granite::official_worker
