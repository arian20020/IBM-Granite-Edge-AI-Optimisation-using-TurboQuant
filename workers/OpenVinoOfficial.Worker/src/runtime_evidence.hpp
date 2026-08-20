#pragma once

#include <filesystem>
#include <string>

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
runtime_evidence initialize_verified_runtime();
void verify_loaded_module_closure(const std::filesystem::path& worker_root);

}  // namespace granite::official_worker
