#pragma once

#include <cstdint>
#include <filesystem>
#include <functional>
#include <memory>
#include <string>
#include <vector>

#include "runtime_evidence.hpp"

namespace granite::official_worker {

struct package_evidence final {
    std::string package_digest;
    std::string model_digest;
    std::uintmax_t model_length{};
};

enum class native_load_stage {
    tokenizer_extension,
    main_model,
    tokenizer_model,
    detokenizer_model,
    pipeline_construction
};

using native_load_observer = std::function<void(native_load_stage)>;
using native_module_verifier = std::function<void()>;
using package_path_open_observer =
    std::function<void(const std::filesystem::path&, bool)>;

struct retained_package_entry final {
    std::filesystem::path relative;
    bool directory{};
    native_file_identity identity;
};

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
    void verify_topology(bool drain_notifications = false) const;
    void verify_terminal_topology(
        const std::function<void()>& during_rescan = {}) const;

private:
    friend package_lease acquire_package(
        const std::filesystem::path&,
        const std::string&,
        const std::string&,
        std::uintmax_t,
        const package_path_open_observer&);

    std::filesystem::path root_;
    native_file_identity root_identity_;
    package_evidence evidence_;
    std::vector<retained_package_entry> entries_;
    std::vector<void*> handles_;
    std::unique_ptr<namespace_monitor> monitor_;
};

package_lease acquire_package(
    const std::filesystem::path& package,
    const std::string& expected_package_digest,
    const std::string& expected_model_digest,
    std::uintmax_t expected_model_length,
    const package_path_open_observer& before_path_open = {});

package_evidence inspect_package(
    package_lease& package,
    const runtime_context& runtime,
    const native_load_observer& observer = {},
    const native_module_verifier& module_verifier = {});

}  // namespace granite::official_worker
#include <array>
