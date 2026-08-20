#pragma once

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

private:
    friend runtime_context initialize_verified_runtime();
    friend runtime_context initialize_verified_runtime_at(
        const std::filesystem::path&,
        const std::function<void()>&);
    runtime_evidence evidence_;
    std::vector<void*> handles_;
};

runtime_context initialize_verified_runtime();
runtime_context initialize_verified_runtime_at(
    const std::filesystem::path& worker_root,
    const std::function<void()>& after_handles_acquired);
void verify_loaded_module_closure(const std::filesystem::path& worker_root);

}  // namespace granite::official_worker
