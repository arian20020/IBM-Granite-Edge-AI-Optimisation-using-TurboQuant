#pragma once

#include <array>
#include <cstdint>
#include <filesystem>
#include <functional>
#include <memory>
#include <string>
#include <string_view>
#include <vector>

#include <nlohmann/json.hpp>

namespace granite::official_worker {

class namespace_monitor;

struct runtime_evidence final {
    std::string runtime_build;
    std::string genai_build;
    std::string tokenizers_build;
    std::string worker_manifest_digest;

    [[nodiscard]] nlohmann::ordered_json to_json() const;
};

struct native_file_identity final {
    std::uint64_t volume_serial{};
    std::array<std::uint8_t, 16> file_id{};

    bool operator==(const native_file_identity&) const noexcept = default;
};

struct retained_runtime_entry final {
    std::filesystem::path relative;
    bool directory{};
    native_file_identity identity;
};

using native_path_open_observer =
    std::function<void(const std::filesystem::path&, bool)>;

enum class runtime_load_stage {
    runtime_version,
    genai_version,
    tokenizers_version
};

using runtime_load_observer = std::function<void(runtime_load_stage)>;

std::filesystem::path executable_directory();
std::string sha256_file(const std::filesystem::path& path);

struct verified_execution_device final {
    std::string requested;
    std::vector<std::string> actual;
};

bool is_explicit_execution_device(std::string_view device) noexcept;
void require_execution_device_match(
    std::string_view requested,
    const std::vector<std::string>& actual);
verified_execution_device verify_execution_device(
    const std::filesystem::path& model_path,
    const std::string& requested,
    const std::function<void()>& module_verifier = {});

class runtime_context final {
public:
    runtime_context() = default;
    ~runtime_context();
    runtime_context(runtime_context&& other) noexcept;
    runtime_context& operator=(runtime_context&& other) noexcept;
    runtime_context(const runtime_context&) = delete;
    runtime_context& operator=(const runtime_context&) = delete;

    [[nodiscard]] const runtime_evidence& evidence() const noexcept;
    [[nodiscard]] const std::filesystem::path& root() const noexcept;
    void verify_topology(bool drain_notifications = false) const;
    void verify_terminal_topology(
        const std::function<void()>& during_rescan = {}) const;
    [[nodiscard]] bool contains_approved_file(
        const native_file_identity& identity) const noexcept;

private:
    static runtime_context initialize(
        const std::filesystem::path&,
        const native_path_open_observer&,
        const std::function<void()>&,
        const runtime_load_observer&,
        bool verify_modules);
    friend runtime_context initialize_verified_runtime();
    friend runtime_context initialize_verified_runtime_at(
        const std::filesystem::path&,
        const native_path_open_observer&,
        const std::function<void()>&);
    friend runtime_context initialize_verified_runtime_at(
        const std::filesystem::path&,
        const native_path_open_observer&,
        const std::function<void()>&,
        const runtime_load_observer&);
    runtime_evidence evidence_;
    std::filesystem::path root_;
    native_file_identity root_identity_;
    native_file_identity manifest_identity_;
    std::vector<retained_runtime_entry> entries_;
    std::vector<void*> handles_;
    std::unique_ptr<namespace_monitor> monitor_;
};

runtime_context initialize_verified_runtime();
runtime_context initialize_verified_runtime_at(
    const std::filesystem::path& worker_root,
    const native_path_open_observer& before_path_open,
    const std::function<void()>& after_handles_acquired);
runtime_context initialize_verified_runtime_at(
    const std::filesystem::path& worker_root,
    const native_path_open_observer& before_path_open,
    const std::function<void()>& after_handles_acquired,
    const runtime_load_observer& load_observer);
void verify_loaded_module_closure(const runtime_context& runtime);
void verify_module_file_membership(
    const runtime_context& runtime,
    const std::filesystem::path& module);
bool is_module_in_validated_os_roots(
    const std::filesystem::path& module,
    const std::vector<std::filesystem::path>& roots);

}  // namespace granite::official_worker
