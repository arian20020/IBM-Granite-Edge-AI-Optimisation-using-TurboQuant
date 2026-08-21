#include "runtime_evidence.hpp"
#include "protocol.hpp"

#include <filesystem>
#include <fstream>
#include <functional>
#include <iostream>
#include <stdexcept>
#include <cstdlib>
#include <vector>

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

        wchar_t system[MAX_PATH]{};
        if (GetSystemDirectoryW(system, MAX_PATH) == 0U) {
            throw std::runtime_error("system directory unavailable");
        }

        std::size_t transient_runtime_rejections = 0U;
        for (const bool throw_after_restore : {false, true}) {
            const std::filesystem::path transient = stage /
                (throw_after_restore
                    ? L"transient-runtime-throw.dll"
                    : L"transient-runtime-return.dll");
            try {
                (void)granite::official_worker::initialize_verified_runtime_at(
                    stage,
                    {},
                    {},
                    [&](granite::official_worker::runtime_load_stage load_stage) {
                        if (load_stage !=
                            granite::official_worker::runtime_load_stage::runtime_version) {
                            return;
                        }
                        std::filesystem::copy_file(
                            std::filesystem::path(system) / L"version.dll",
                            transient,
                            std::filesystem::copy_options::overwrite_existing);
                        const HMODULE module = LoadLibraryW(transient.c_str());
                        if (module == nullptr) {
                            throw std::runtime_error("transient runtime module did not load");
                        }
                        FreeLibrary(module);
                        std::filesystem::remove(transient);
                        if (throw_after_restore) {
                            throw std::runtime_error("synthetic runtime load failure");
                        }
                    });
            } catch (const granite::official_worker::worker_failure& error) {
                if (error.support_code() == "runtime_integrity_failed") {
                    ++transient_runtime_rejections;
                }
            }
            std::filesystem::remove(transient);
        }
        if (transient_runtime_rejections != 2U) {
            throw std::runtime_error(
                "transient runtime module escaped the load boundary");
        }

        const std::filesystem::path os_fixture =
            std::filesystem::temp_directory_path() /
            (L"GraniteEdgeAI-SystemRoots-" + std::to_wstring(GetCurrentProcessId()));
        const std::filesystem::path system32 = os_fixture / L"Windows" / L"System32";
        const std::filesystem::path winsxs = os_fixture / L"Windows" / L"WinSxS";
        const std::filesystem::path windows_temp = os_fixture / L"Windows" / L"Temp";
        std::filesystem::create_directories(system32);
        std::filesystem::create_directories(winsxs / L"amd64_fixture");
        std::filesystem::create_directories(windows_temp);
        const std::filesystem::path source_module =
            std::filesystem::path(system) / L"version.dll";
        const std::filesystem::path system_module = system32 / L"version.dll";
        const std::filesystem::path winsxs_module =
            winsxs / L"amd64_fixture" / L"version.dll";
        const std::filesystem::path temp_module = windows_temp / L"version.dll";
        std::filesystem::copy_file(source_module, system_module);
        std::filesystem::copy_file(source_module, winsxs_module);
        std::filesystem::copy_file(source_module, temp_module);
        const std::vector<std::filesystem::path> allowed_roots{system32, winsxs};
        const bool roots_classified =
            granite::official_worker::is_module_in_validated_os_roots(
                system_module, allowed_roots) &&
            granite::official_worker::is_module_in_validated_os_roots(
                winsxs_module, allowed_roots) &&
            !granite::official_worker::is_module_in_validated_os_roots(
                temp_module, allowed_roots);
        std::filesystem::remove_all(os_fixture);
        if (!roots_classified) {
            throw std::runtime_error("Windows subtree received a system-module exemption");
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
