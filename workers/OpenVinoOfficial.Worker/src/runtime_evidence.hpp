#pragma once

#include <array>
#include <cstdint>
#include <filesystem>
#include <functional>
#include <string>
#include <vector>

#include <nlohmann/json.hpp>

namespace granite::official_worker {

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

std::filesystem::path executable_directory();
std::string sha256_file(const std::filesystem::path& path);

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
    void verify_topology() const;
    [[nodiscard]] bool contains_approved_file(
        const native_file_identity& identity) const noexcept;

private:
    friend runtime_context initialize_verified_runtime();
    friend runtime_context initialize_verified_runtime_at(
        const std::filesystem::path&,
        const native_path_open_observer&,
        const std::function<void()>&);
    runtime_evidence evidence_;
    std::filesystem::path root_;
    native_file_identity root_identity_;
    native_file_identity manifest_identity_;
    std::vector<retained_runtime_entry> entries_;
    std::vector<void*> handles_;
};

runtime_context initialize_verified_runtime();
runtime_context initialize_verified_runtime_at(
    const std::filesystem::path& worker_root,
    const native_path_open_observer& before_path_open,
    const std::function<void()>& after_handles_acquired);
void verify_loaded_module_closure(const runtime_context& runtime);
void verify_module_file_membership(
    const runtime_context& runtime,
    const std::filesystem::path& module);

}  // namespace granite::official_worker
