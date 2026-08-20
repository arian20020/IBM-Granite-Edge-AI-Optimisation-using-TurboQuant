#pragma once

#include <filesystem>
#include <string>

namespace granite::official_worker {

struct package_evidence final {
    std::string package_digest;
    std::string model_digest;
    std::uintmax_t model_length{};
};

package_evidence inspect_package(
    const std::filesystem::path& package,
    const std::string& expected_package_digest,
    const std::string& expected_model_digest,
    std::uintmax_t expected_model_length);

}  // namespace granite::official_worker
