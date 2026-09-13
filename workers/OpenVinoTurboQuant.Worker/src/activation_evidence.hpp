#pragma once

#include <cstdint>
#include <filesystem>
#include <string>

#include <nlohmann/json.hpp>

namespace granite::official_worker {

#if defined(GRANITE_TURBOQUANT_PROBE_BUILD)
#define GRANITE_TURBOQUANT_PROBE_API __declspec(dllexport)
#else
#define GRANITE_TURBOQUANT_PROBE_API __declspec(dllimport)
#endif

struct GRANITE_TURBOQUANT_PROBE_API turboquant_activation_evidence final {
    std::string codec;
    std::uint64_t packed_bytes_per_record{};
    std::uint64_t runtime_dispatch_count{};
    std::uint64_t encoded_record_count{};
    std::uint64_t model_sdpa_node_count{};
    std::uint64_t packed_cache_bytes{};
    std::uint64_t full_precision_cache_bytes{};
    bool forced_scalar_negative{};

    [[nodiscard]] nlohmann::ordered_json to_json(
        const std::string& session_id,
        const std::string& turn_id) const;
};

GRANITE_TURBOQUANT_PROBE_API turboquant_activation_evidence measure_turboquant_activation(
    const std::filesystem::path& model_path = {},
    const std::string& kv_cache_precision = "tbq4",
    std::uint64_t prevalidated_model_sdpa_nodes = 0U);

}  // namespace granite::official_worker

#undef GRANITE_TURBOQUANT_PROBE_API
