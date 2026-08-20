#include "runtime_evidence.hpp"

#include <filesystem>
#include <fstream>
#include <iostream>
#include <stdexcept>

int main(int argc, char** argv) {
    if (argc != 2) return 2;
    try {
        const std::filesystem::path stage = std::filesystem::absolute(argv[1]);
        std::size_t denied = 0U;
        auto attempt_replacement = [&](const std::filesystem::path& path) {
            std::ofstream replacement(path, std::ios::binary | std::ios::trunc);
            if (replacement) {
                throw std::runtime_error("verified runtime replacement was not denied");
            }
            ++denied;
        };
        {
            granite::official_worker::runtime_context runtime =
                granite::official_worker::initialize_verified_runtime_at(
                    stage,
                    [&] {
                        attempt_replacement(stage / L"worker-manifest.json");
                        attempt_replacement(stage / L"openvino.dll");
                    });
            if (denied != 2U || runtime.evidence().worker_manifest_digest.empty()) {
                throw std::runtime_error("runtime lease evidence was incomplete");
            }
        }
        std::fstream manifest(
            stage / L"worker-manifest.json",
            std::ios::in | std::ios::out | std::ios::binary);
        if (!manifest) {
            throw std::runtime_error("runtime lease survived context disposal");
        }
        std::cout << "runtime_lease_tests_passed\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
