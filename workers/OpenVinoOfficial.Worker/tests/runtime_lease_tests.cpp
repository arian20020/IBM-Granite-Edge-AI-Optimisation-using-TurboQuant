#include "runtime_evidence.hpp"
#include "protocol.hpp"

#include <filesystem>
#include <fstream>
#include <iostream>
#include <stdexcept>
#include <cstdlib>

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

namespace {

void remove_reparse_and_restore(
    const std::filesystem::path& link,
    const std::filesystem::path& original) {
    std::error_code ignored;
    std::filesystem::remove(link, ignored);
    if (std::filesystem::exists(original)) {
        std::filesystem::rename(original, link);
    }
}

void create_junction(
    const std::filesystem::path& link,
    const std::filesystem::path& target) {
    const std::wstring command = L"cmd.exe /d /c mklink /J \"" + link.wstring() +
        L"\" \"" + target.wstring() + L"\" >nul";
    if (_wsystem(command.c_str()) != 0) {
        throw std::runtime_error("directory junction fixture unavailable");
    }
}

}  // namespace

int main(int argc, char** argv) {
    if (argc != 2) return 2;
    try {
        const std::filesystem::path stage = std::filesystem::absolute(argv[1]);
        const std::filesystem::path licenses = stage / L"licenses";
        const std::filesystem::path displaced = stage / L"licenses.review-original";
        bool swapped = false;
        bool reparse_rejected = false;
        try {
            (void)granite::official_worker::initialize_verified_runtime_at(
                stage,
                [&](const std::filesystem::path& relative, bool directory) {
                    if (!swapped && directory && relative == L"licenses") {
                        std::filesystem::rename(licenses, displaced);
                        create_junction(licenses, displaced);
                        swapped = true;
                    }
                },
                {});
        } catch (const std::exception&) {
            if (!swapped) {
                remove_reparse_and_restore(licenses, displaced);
                throw;
            }
            reparse_rejected = swapped;
        }
        remove_reparse_and_restore(licenses, displaced);
        if (!reparse_rejected) {
            throw std::runtime_error("runtime reparse swap was accepted");
        }

        const std::filesystem::path inserted = stage / L"unlisted-after-acquisition.txt";
        bool insertion_rejected = false;
        try {
            (void)granite::official_worker::initialize_verified_runtime_at(
                stage,
                {},
                [&] { std::ofstream(inserted) << "inserted"; });
        } catch (const std::exception&) {
            insertion_rejected = true;
        }
        std::filesystem::remove(inserted);
        if (!insertion_rejected) {
            throw std::runtime_error("runtime child insertion was accepted");
        }

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
                    {},
                    [&] {
                        attempt_replacement(stage / L"worker-manifest.json");
                        attempt_replacement(stage / L"openvino.dll");
                    });
            if (denied != 2U || runtime.evidence().worker_manifest_digest.empty()) {
                throw std::runtime_error("runtime lease evidence was incomplete");
            }

            wchar_t system[MAX_PATH]{};
            if (GetSystemDirectoryW(system, MAX_PATH) == 0U) {
                throw std::runtime_error("system directory unavailable");
            }
            const std::filesystem::path unlisted = stage / L"unlisted-module.dll";
            std::filesystem::copy_file(
                std::filesystem::path(system) / L"version.dll",
                unlisted,
                std::filesystem::copy_options::overwrite_existing);
            const HMODULE module = LoadLibraryW(unlisted.c_str());
            if (module == nullptr) throw std::runtime_error("unlisted module did not load");
            bool module_rejected = false;
            try {
                granite::official_worker::verify_module_file_membership(runtime, unlisted);
            } catch (const granite::official_worker::worker_failure& failure) {
                module_rejected = failure.support_code() == "runtime_integrity_failed";
            }
            FreeLibrary(module);
            std::filesystem::remove(unlisted);
            if (!module_rejected) {
                throw std::runtime_error("unlisted loaded module was accepted");
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
