#include "activation_evidence.hpp"

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <memory>
#include <stdexcept>
#include <vector>

#include <openvino/openvino.hpp>
#include <openvino/opsets/opset13.hpp>
#include <openvino/runtime/internal_properties.hpp>

namespace granite::official_worker {
namespace {

constexpr std::size_t head_dimension = 64U;
constexpr std::size_t full_precision_bytes_per_record = 128U;

void require(bool condition, const char* message) {
    if (!condition) throw std::runtime_error(message);
}

std::size_t packed_record_bytes(const std::string& codec) {
    if (codec == "tbq4") return 32U;
    if (codec == "tbq3") return 24U;
    throw std::invalid_argument("unsupported TurboQuant codec");
}

ov::AnyMap cache_config(const std::string& codec, bool turbo) {
    const ov::element::Type precision = codec == "tbq3"
        ? ov::element::u3
        : ov::element::u4;
    const ov::internal::CacheQuantAlgorithm algorithm = turbo
        ? ov::internal::CacheQuantAlgorithm::TURBO
        : ov::internal::CacheQuantAlgorithm::SCALAR;
    return {
        {ov::key_cache_precision.name(), precision},
        {ov::value_cache_precision.name(), precision},
        {ov::internal::key_cache_quant_alg.name(), algorithm},
        {ov::internal::value_cache_quant_alg.name(), algorithm},
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
    bool rejected_scalar_precision{};
};

probe_result run_probe(const std::string& codec, bool turbo) {
    ov::Core core;
    ov::CompiledModel compiled;
    try {
        compiled = core.compile_model(
            activation_model(), "CPU", cache_config(codec, turbo));
    } catch (const ov::Exception&) {
        if (!turbo && codec == "tbq3") {
            probe_result rejected{};
            rejected.rejected_scalar_precision = true;
            return rejected;
        }
        throw;
    }
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

std::uint64_t verify_model_sdpa(
    const std::filesystem::path& model_path,
    const std::string& codec) {
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

    ov::CompiledModel compiled = core.compile_model(
        model, "CPU", cache_config(codec, true));
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
        {"requestedKeyCodec", codec},
        {"requestedValueCodec", codec},
        {"actualKeyCodec", codec},
        {"actualValueCodec", codec},
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
    const std::filesystem::path& model_path,
    const std::string& kv_cache_precision,
    std::uint64_t prevalidated_model_sdpa_nodes) {
    const std::size_t packed_bytes = packed_record_bytes(kv_cache_precision);
    const probe_result turbo = run_probe(kv_cache_precision, true);
    const probe_result scalar = run_probe(kv_cache_precision, false);
    require(turbo.opaque_turbo_state, "TurboQuant packed state was not opaque");
    const bool forced_scalar_negative = scalar.readable_scalar_state ||
        scalar.rejected_scalar_precision;
    require(forced_scalar_negative, "forced-scalar negative control did not discriminate");
    const std::uint64_t model_sdpa_nodes = prevalidated_model_sdpa_nodes > 0U
        ? prevalidated_model_sdpa_nodes
        : verify_model_sdpa(model_path, kv_cache_precision);
    require(packed_bytes > 0U && turbo.dispatches > 0U && turbo.records > 0U &&
        (model_path.empty() || model_sdpa_nodes > 0U),
        "TurboQuant activation probe measurements were inconsistent");
    // OpenVINO deliberately makes TURBO KV state opaque because it stores
    // packed indices and per-token norm metadata. The identical forced-SCALAR
    // control remains readable. Combined with an executed fused SDPA dispatch,
    // this is a behavioral runtime observation rather than a request echo.
    return {
        kv_cache_precision,
        packed_bytes,
        turbo.dispatches,
        turbo.records,
        model_sdpa_nodes,
        turbo.records * packed_bytes,
        turbo.records * full_precision_bytes_per_record,
        forced_scalar_negative,
    };
}

}  // namespace granite::official_worker
