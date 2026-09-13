#include "package_inspector.hpp"
#include "protocol.hpp"
#include "session.hpp"

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include <filesystem>
#include <fstream>
#include <iostream>
#include <stdexcept>
#include <thread>
#include <chrono>
#include <cstdlib>

namespace {

constexpr std::string_view package_digest =
    "b5316ac62e1e846b33ec92e5ad555859238af70ffd6b75aa50500995fbe15372";
constexpr std::string_view model_digest =
    "894dd0aac21e588d5cf78994d90aa0dcba8284626c976a4e0c89c0273b452c1c";

void create_junction(
    const std::filesystem::path& link,
    const std::filesystem::path& target) {
    const std::wstring command = L"cmd.exe /d /c mklink /J \"" + link.wstring() +
        L"\" \"" + target.wstring() + L"\" >nul";
    if (_wsystem(command.c_str()) != 0) {
        throw std::runtime_error("directory junction fixture unavailable");
    }
}

std::filesystem::path resource_for(
    const std::filesystem::path& root,
    granite::official_worker::native_load_stage stage) {
    using granite::official_worker::native_load_stage;
    switch (stage) {
        case native_load_stage::tokenizer_extension: return root / L"openvino_model.bin";
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
    if (argc != 3) return 2;
    const std::filesystem::path source = std::filesystem::absolute(argv[1]);
    const std::filesystem::path runtime_stage = std::filesystem::absolute(argv[2]);
    const std::filesystem::path copy = std::filesystem::temp_directory_path() /
        (L"GraniteEdgeAI-NativeLease-" + std::to_wstring(GetCurrentProcessId()));
    try {
        std::size_t transient_package_rejections = 0U;
        std::filesystem::copy(source, copy, std::filesystem::copy_options::recursive);
        const std::filesystem::path swapped_path = copy / L"empty-review-directory";
        const std::filesystem::path displaced_path = copy / L"empty-review-directory.original";
        std::filesystem::create_directory(swapped_path);
        bool swapped = false;
        bool reparse_rejected = false;
        try {
            (void)acquire_package(
                copy,
                std::string(package_digest),
                std::string(model_digest),
                88U,
                [&](const std::filesystem::path& relative, bool directory) {
                    if (!swapped && directory && relative == L"empty-review-directory") {
                        std::filesystem::rename(swapped_path, displaced_path);
                        create_junction(swapped_path, displaced_path);
                        swapped = true;
                    }
                });
        } catch (const std::exception&) {
            if (!swapped) {
                std::error_code restore_error;
                std::filesystem::remove(swapped_path, restore_error);
                if (std::filesystem::exists(displaced_path)) {
                    std::filesystem::rename(displaced_path, swapped_path);
                }
                throw;
            }
            reparse_rejected = swapped;
        }
        std::error_code ignored;
        std::filesystem::remove(swapped_path, ignored);
        if (std::filesystem::exists(displaced_path)) {
            std::filesystem::rename(displaced_path, swapped_path);
        }
        std::filesystem::remove(swapped_path);
        if (!reparse_rejected) {
            throw std::runtime_error("package reparse swap was accepted");
        }

        {
            package_lease topology = acquire_package(
                copy, std::string(package_digest), std::string(model_digest), 88U);
            const std::filesystem::path inserted = copy / L"unlisted-after-acquisition.txt";
            bool insertion_rejected = false;
            std::vector<native_load_stage> completed_before_failure;
            try {
                runtime_context runtime = initialize_verified_runtime_at(runtime_stage, {}, {});
                (void)inspect_package(topology, runtime, [&](native_load_stage stage) {
                    if (stage == native_load_stage::main_model) {
                        std::ofstream(inserted) << "inserted";
                    }
                }, {}, [&](native_load_stage stage) { completed_before_failure.push_back(stage); });
            } catch (const worker_failure& failure) {
                insertion_rejected = failure.support_code() == "package_changed";
            }
            std::filesystem::remove(inserted);
            if (!insertion_rejected) {
                throw std::runtime_error("package child insertion was accepted");
            }
            if (completed_before_failure != std::vector<native_load_stage>{native_load_stage::tokenizer_extension})
                throw std::runtime_error("failed integrity boundary falsely reported completion");
        }

        {
            const std::filesystem::path transient =
                copy / L"transient-package-boundary.tmp";
            std::size_t rejected_variants = 0U;
            for (const bool throw_after_restore : {false, true}) {
                package_lease topology = acquire_package(
                    copy, std::string(package_digest), std::string(model_digest), 88U);
                try {
                    runtime_context runtime =
                        initialize_verified_runtime_at(runtime_stage, {}, {});
                    (void)inspect_package(
                        topology,
                        runtime,
                        [&](native_load_stage stage) {
                            if (stage != native_load_stage::main_model) return;
                            std::ofstream(transient) << "transient";
                            std::filesystem::remove(transient);
                            if (throw_after_restore) {
                                throw std::runtime_error(
                                    "synthetic package load failure");
                            }
                        });
                } catch (const worker_failure& failure) {
                    if (failure.support_code() == "package_changed") {
                        ++rejected_variants;
                    } else {
                        std::cerr << "transient_package_variant="
                                  << (throw_after_restore ? "throw" : "return")
                                  << " code=" << failure.support_code() << '\n';
                    }
                }
            }
            std::filesystem::remove(transient);
            transient_package_rejections = rejected_variants;
        }

        {
            package_lease cancellation_package = acquire_package(
                copy, std::string(package_digest), std::string(model_digest), 88U);
            runtime_context runtime = initialize_verified_runtime_at(runtime_stage, {}, {});
            (void)inspect_package(cancellation_package, runtime);
            std::size_t cancellation_pipeline_constructions = 0U;
            official_session cancellation_session(
                std::move(cancellation_package), runtime, "CPU", 64U, 64U,
                [&](native_load_stage stage) {
                    if (stage == native_load_stage::pipeline_construction) {
                        ++cancellation_pipeline_constructions;
                    }
                });
            turn_control cancellation_control;
            std::atomic_bool cancel_command_written{false};
            bool fragment_observed = false;
            std::thread delayed_input_pump([&] {
                std::unique_lock lock(cancellation_control.mutex);
                fragment_observed = cancellation_control.changed.wait_for(
                    lock,
                    std::chrono::seconds(5),
                    [&] {
                        return cancellation_control.first_fragment_buffered.load(
                            std::memory_order_acquire);
                    });
                if (fragment_observed) {
                    lock.unlock();
                    std::this_thread::sleep_for(std::chrono::milliseconds(250));
                    lock.lock();
                    if (cancel_command_written.load(std::memory_order_acquire)) {
                        cancellation_control.cancel.store(
                            true, std::memory_order_release);
                    }
                    cancellation_control.notify();
                }
            });
            cancel_command_written.store(true, std::memory_order_release);
            const turn_result cancelled = cancellation_session.generate(
                "e39d252d-2144-4624-a055-0350c93f6728",
                "f77fb13c-263d-49a1-8d93-d908968c5832",
                "hello",
                2U,
                cancellation_control);
            delayed_input_pump.join();
            if (!fragment_observed || !cancelled.cancelled ||
                cancellation_pipeline_constructions != 1U ||
                cancelled.streamed_fragments != 0U || !cancelled.answer.empty()) {
                throw std::runtime_error("first-fragment cancellation leaked output");
            }
        }
        if (transient_package_rejections != 2U) {
            throw std::runtime_error(
                "transient package mutation escaped the load boundary: " +
                std::to_string(transient_package_rejections));
        }

        std::size_t denied = 0U;
        std::size_t removal_denied = 0U;
        std::size_t pipeline_constructions = 0U;
        auto observer = [&](native_load_stage stage) {
            if (stage == native_load_stage::pipeline_construction) {
                ++pipeline_constructions;
            }
            const std::filesystem::path resource = resource_for(copy, stage);
            const std::filesystem::path displaced = resource.wstring() + L".displaced";
            std::error_code rename_error;
            std::filesystem::rename(resource, displaced, rename_error);
            if (!rename_error) {
                std::filesystem::rename(displaced, resource);
                throw std::runtime_error("resource removal was not denied");
            }
            ++removal_denied;
            std::ofstream replacement(resource, std::ios::binary | std::ios::trunc);
            if (replacement) throw std::runtime_error("resource replacement was not denied");
            ++denied;
        };
        {
            package_lease lease = acquire_package(
                copy, std::string(package_digest), std::string(model_digest), 88U);
            runtime_context runtime = initialize_verified_runtime_at(runtime_stage, {}, {});
            std::vector<native_load_stage> completed_stages;
            (void)inspect_package(lease, runtime, observer, {},
                [&](native_load_stage stage) { completed_stages.push_back(stage); });
            if (completed_stages != std::vector<native_load_stage>{
                    native_load_stage::tokenizer_extension, native_load_stage::main_model,
                    native_load_stage::tokenizer_model, native_load_stage::detokenizer_model})
                throw std::runtime_error("inspection completion boundaries were not reported in order");
            official_session session(
                std::move(lease), runtime, "CPU", 64U, 64U, observer);
            turn_control control;
            std::jthread input_pump([&] {
                std::unique_lock lock(control.mutex);
                if (control.changed.wait_for(
                        lock,
                        std::chrono::seconds(5),
                        [&] {
                            return control.first_fragment_buffered.load(
                                std::memory_order_acquire);
                        })) {
                    control.first_fragment_release.store(true, std::memory_order_release);
                    control.notify();
                }
            });
            const turn_result result = session.generate(
                "e39d252d-2144-4624-a055-0350c93f6728",
                "f77fb13c-263d-49a1-8d93-d908968c5832",
                "hello",
                2U,
                control);
            if (result.answer != "fixture" || result.generated_tokens != 2U ||
                pipeline_constructions != 2U || denied < 5U ||
                removal_denied < 5U) {
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
