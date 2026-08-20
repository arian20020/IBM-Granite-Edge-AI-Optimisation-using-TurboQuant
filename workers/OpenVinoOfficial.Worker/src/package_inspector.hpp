#pragma once

#include <cstdint>
#include <filesystem>
#include <functional>
#include <string>
#include <vector>

namespace granite::official_worker {

struct package_evidence final {
    std::string package_digest;
    std::string model_digest;
    std::uintmax_t model_length{};
};

enum class native_load_stage {
    main_model,
    tokenizer_model,
    detokenizer_model,
    pipeline_construction
};

using native_load_observer = std::function<void(native_load_stage)>;

class package_lease final {
public:
    package_lease() = default;
    ~package_lease();
    package_lease(package_lease&& other) noexcept;
    package_lease& operator=(package_lease&& other) noexcept;
    package_lease(const package_lease&) = delete;
    package_lease& operator=(const package_lease&) = delete;

    [[nodiscard]] const std::filesystem::path& root() const noexcept;
    [[nodiscard]] const package_evidence& evidence() const noexcept;

private:
    friend package_lease acquire_package(
        const std::filesystem::path&,
        const std::string&,
        const std::string&,
        std::uintmax_t);

    std::filesystem::path root_;
    package_evidence evidence_;
    std::vector<void*> handles_;
};

package_lease acquire_package(
    const std::filesystem::path& package,
    const std::string& expected_package_digest,
    const std::string& expected_model_digest,
    std::uintmax_t expected_model_length);

package_evidence inspect_package(
    package_lease& package,
    const native_load_observer& observer = {});

}  // namespace granite::official_worker
