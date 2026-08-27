#include <algorithm>
#include <array>
#include <atomic>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <future>
#include <iostream>
#include <limits>
#include <random>
#include <span>
#include <stdexcept>
#include <vector>

#include "turboq_tables.hpp"

namespace {

constexpr int head_dim = 64;
constexpr int packed_bytes = head_dim / 2;
constexpr std::uint64_t turboq_seed = 0x517cc1b727220a95ULL;
constexpr std::uint64_t wht_seed = turboq_seed ^ 0xfedcba9876543210ULL;

struct Record {
    std::array<std::uint8_t, packed_bytes> packed{};
    float norm = 0.0F;
};

void require(bool condition, const char* message) {
    if (!condition) {
        throw std::runtime_error(message);
    }
}

std::array<float, head_dim> signs() {
    std::array<float, head_dim> result{};
    std::mt19937_64 rng(wht_seed);
    std::uniform_int_distribution<int> distribution(0, 1);
    for (float& value : result) {
        value = distribution(rng) ? 1.0F : -1.0F;
    }
    return result;
}

void wht(std::span<float> values) {
    require(values.size() == head_dim, "head dimension must be exactly 64");
    for (int half = 1; half < head_dim; half <<= 1) {
        for (int base = 0; base < head_dim; base += half << 1) {
            for (int lane = 0; lane < half; ++lane) {
                const float low = values[base + lane];
                const float high = values[base + lane + half];
                values[base + lane] = low + high;
                values[base + lane + half] = low - high;
            }
        }
    }
}

std::uint8_t quantize(float value) {
    const float* boundaries = turboq_boundaries(4, head_dim);
    std::uint8_t index = 0;
    for (int boundary = 0; boundary < 15; ++boundary) {
        index = static_cast<std::uint8_t>(index + (value > boundaries[boundary] ? 1 : 0));
    }
    return index;
}

Record encode(std::span<const float> input) {
    if (input.size() != head_dim) {
        throw std::invalid_argument("head dimension must be exactly 64");
    }
    double norm_squared = 0.0;
    for (float value : input) {
        if (!std::isfinite(value)) {
            throw std::invalid_argument("non-finite cache value");
        }
        norm_squared += static_cast<double>(value) * value;
    }

    Record result;
    result.norm = static_cast<float>(std::sqrt(norm_squared));
    std::array<float, head_dim> transformed{};
    const auto sign_values = signs();
    const float inverse_norm = result.norm < 1.0e-30F ? 0.0F : 1.0F / result.norm;
    for (int lane = 0; lane < head_dim; ++lane) {
        transformed[lane] = input[lane] * inverse_norm * sign_values[lane];
    }
    wht(transformed);
    for (int lane = 0; lane < head_dim; lane += 2) {
        const auto low = quantize(transformed[lane]);
        const auto high = quantize(transformed[lane + 1]);
        result.packed[lane / 2] = static_cast<std::uint8_t>((high << 4) | low);
    }
    return result;
}

std::array<float, head_dim> decode(const Record& record) {
    if (!std::isfinite(record.norm) || record.norm < 0.0F) {
        throw std::invalid_argument("malformed norm metadata");
    }
    const float* codebook = turboq_codebook(4, head_dim);
    std::array<float, head_dim> transformed{};
    for (int lane = 0; lane < head_dim; lane += 2) {
        const std::uint8_t byte = record.packed[lane / 2];
        transformed[lane] = codebook[byte & 0x0F];
        transformed[lane + 1] = codebook[byte >> 4];
    }
    wht(transformed);
    const auto sign_values = signs();
    const float scale = record.norm / static_cast<float>(head_dim);
    for (int lane = 0; lane < head_dim; ++lane) {
        transformed[lane] *= sign_values[lane] * scale;
    }
    return transformed;
}

std::array<float, head_dim> ramp() {
    std::array<float, head_dim> values{};
    for (int lane = 0; lane < head_dim; ++lane) {
        values[lane] = static_cast<float>(lane - 32) / 8.0F;
    }
    return values;
}

void golden_packing_and_round_trip() {
    static constexpr std::array<std::uint8_t, packed_bytes> expected = {
        0x45, 0xe7, 0x84, 0xd4, 0x99, 0xcc, 0xde, 0xb4,
        0x56, 0xc6, 0x6a, 0x78, 0x9d, 0x9c, 0x80, 0x47,
        0x77, 0xc7, 0x8c, 0x84, 0x9d, 0xb7, 0xa1, 0x7a,
        0x45, 0xca, 0xa1, 0xc4, 0x4b, 0xbb, 0x8a, 0xb8,
    };
    const auto input = ramp();
    const Record record = encode(input);
    if (record.packed != expected) {
        std::cerr << "golden_actual=";
        for (std::uint8_t byte : record.packed) {
            std::cerr << std::hex << static_cast<int>(byte) << ',';
        }
        std::cerr << std::dec << '\n';
        throw std::runtime_error("golden packed bytes differ");
    }
    const auto output = decode(record);
    double error = 0.0;
    double energy = 0.0;
    for (int lane = 0; lane < head_dim; ++lane) {
        const double delta = static_cast<double>(output[lane]) - input[lane];
        error += delta * delta;
        energy += static_cast<double>(input[lane]) * input[lane];
    }
    require(std::sqrt(error / energy) < 0.20, "round-trip relative error exceeded bound");
}

void zero_saturation_and_nonfinite_policy() {
    std::array<float, head_dim> zero{};
    const Record zero_record = encode(zero);
    require(zero_record.norm == 0.0F, "zero norm changed");
    require(std::all_of(zero_record.packed.begin(), zero_record.packed.end(),
                        [](std::uint8_t byte) { return byte == 0x77; }),
            "zero packing changed");
    const auto zero_output = decode(zero_record);
    require(std::all_of(zero_output.begin(), zero_output.end(),
                        [](float value) { return value == 0.0F; }),
            "zero did not round trip");

    auto extreme = ramp();
    extreme[0] = 1.0e18F;
    extreme[1] = -1.0e18F;
    const Record saturated = encode(extreme);
    require(std::isfinite(saturated.norm), "finite saturation input overflowed metadata");

    for (float invalid : {std::numeric_limits<float>::quiet_NaN(),
                          std::numeric_limits<float>::infinity(),
                          -std::numeric_limits<float>::infinity()}) {
        auto malformed = ramp();
        malformed[7] = invalid;
        bool rejected = false;
        try {
            (void)encode(malformed);
        } catch (const std::invalid_argument&) {
            rejected = true;
        }
        require(rejected, "non-finite input was accepted");
    }

    Record malformed = zero_record;
    malformed.norm = std::numeric_limits<float>::quiet_NaN();
    bool rejected = false;
    try {
        (void)decode(malformed);
    } catch (const std::invalid_argument&) {
        rejected = true;
    }
    require(rejected, "malformed norm metadata was accepted");
}

void dimensions_alignment_lanes_and_threads() {
    std::array<float, head_dim - 1> short_head{};
    bool rejected = false;
    try {
        (void)encode(short_head);
    } catch (const std::invalid_argument&) {
        rejected = true;
    }
    require(rejected, "unsupported tail/head dimension was accepted");

    const auto input = ramp();
    const Record baseline = encode(input);
    std::array<std::uint8_t, packed_bytes + 3> unaligned{};
    std::memcpy(unaligned.data() + 1, baseline.packed.data(), baseline.packed.size());
    require(std::memcmp(unaligned.data() + 1, baseline.packed.data(), baseline.packed.size()) == 0,
            "unaligned packed record changed");

    std::vector<std::future<Record>> work;
    for (int task = 0; task < 32; ++task) {
        work.emplace_back(std::async(std::launch::async, [input] { return encode(input); }));
    }
    for (auto& future : work) {
        const Record candidate = future.get();
        require(candidate.packed == baseline.packed && candidate.norm == baseline.norm,
                "multithreaded encoding was nondeterministic");
    }
}

}  // namespace

int main() {
    try {
        golden_packing_and_round_trip();
        zero_saturation_and_nonfinite_policy();
        dimensions_alignment_lanes_and_threads();
        std::cout << "tbq4_codec_tests_passed\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
