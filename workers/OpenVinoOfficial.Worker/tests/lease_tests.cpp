#include "package_inspector.hpp"
#include "session.hpp"

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include <filesystem>
#include <fstream>
#include <iostream>
#include <stdexcept>

namespace {

constexpr std::string_view package_digest =
    "b5316ac62e1e846b33ec92e5ad555859238af70ffd6b75aa50500995fbe15372";
constexpr std::string_view model_digest =
    "894dd0aac21e588d5cf78994d90aa0dcba8284626c976a4e0c89c0273b452c1c";

std::filesystem::path resource_for(
    const std::filesystem::path& root,
    granite::official_worker::native_load_stage stage) {
    using granite::official_worker::native_load_stage;
    switch (stage) {
        case native_load_stage::main_model: return root / L"openvino_model.xml";
        case native_load_stage::tokenizer_model: return root / L"openvino_tokenizer.xml";
        case native_load_stage::detokenizer_model: return root / L"openvino_detokenizer.xml";
        case native_load_stage::pipeline_construction: return root / L"openvino_model.bin";
    }
    throw std::runtime_error("unknown load stage");
}

}  // namespace

int main(int argc, char** argv) {
    using namespace granite::official_worker;
    if (argc != 2) return 2;
    const std::filesystem::path source = std::filesystem::absolute(argv[1]);
    const std::filesystem::path copy = std::filesystem::temp_directory_path() /
        (L"GraniteEdgeAI-NativeLease-" + std::to_wstring(GetCurrentProcessId()));
    try {
        std::filesystem::copy(source, copy, std::filesystem::copy_options::recursive);
        std::size_t denied = 0U;
        auto observer = [&](native_load_stage stage) {
            std::ofstream replacement(resource_for(copy, stage), std::ios::binary | std::ios::trunc);
            if (replacement) throw std::runtime_error("resource replacement was not denied");
            ++denied;
        };
        {
            package_lease lease = acquire_package(
                copy, std::string(package_digest), std::string(model_digest), 88U);
            (void)inspect_package(lease, observer);
            official_session session(std::move(lease), 64U, 64U, observer);
            turn_control control;
            const turn_result result = session.generate(
                "e39d252d-2144-4624-a055-0350c93f6728",
                "f77fb13c-263d-49a1-8d93-d908968c5832",
                "hello",
                2U,
                control);
            if (result.answer != "fixture" || result.generated_tokens != 2U || denied < 5U) {
                throw std::runtime_error("real leased load evidence was incomplete");
            }
        }
        std::filesystem::remove_all(copy);
        if (std::filesystem::exists(copy)) {
            throw std::runtime_error("package lease survived operation disposal");
        }
        std::cout << "lease_tests_passed\n";
        return 0;
    } catch (const std::exception& error) {
        std::error_code ignored;
        std::filesystem::remove_all(copy, ignored);
        std::cerr << error.what() << '\n';
        return 1;
    }
}
