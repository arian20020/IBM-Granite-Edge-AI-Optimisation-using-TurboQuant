#include "activation_evidence.hpp"

#include <iostream>
#include <stdexcept>
#include <utility>

int main(int argc, char** argv) {
    try {
        const std::filesystem::path model_path = argc == 2
            ? std::filesystem::path(argv[1])
            : std::filesystem::path{};
        for (const std::string codec : {"tbq4", "tbq3"}) {
            const auto evidence =
                granite::official_worker::measure_turboquant_activation(model_path, codec);
            const std::uint64_t expected_record_bytes = codec == "tbq3" ? 24U : 32U;
            if (evidence.codec != codec ||
                evidence.packed_bytes_per_record != expected_record_bytes ||
                evidence.runtime_dispatch_count == 0U ||
                evidence.encoded_record_count == 0U ||
                evidence.packed_cache_bytes !=
                    evidence.encoded_record_count * expected_record_bytes ||
                evidence.full_precision_cache_bytes !=
                    evidence.encoded_record_count * 128U ||
                evidence.packed_cache_bytes >= evidence.full_precision_cache_bytes ||
                !evidence.forced_scalar_negative) {
                throw std::runtime_error("activation evidence is inconsistent");
            }
            if (!model_path.empty() && evidence.model_sdpa_node_count == 0U) {
                throw std::runtime_error("model SDPA activation was not observed");
            }

            const auto json = evidence.to_json(
                "00000000-0000-0000-0000-000000000001",
                "00000000-0000-0000-0000-000000000002");
            if (json.at("eventType") != "turboQuantActivation" ||
                json.at("actualKeyCodec") != codec ||
                json.at("actualValueCodec") != codec ||
                json.at("forcedScalarNegative") != true) {
                throw std::runtime_error("activation JSON is inconsistent");
            }
        }
        std::cout << "turboquant_activation_probe_passed\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
