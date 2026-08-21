#define NOMINMAX
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include <chrono>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <sstream>
#include <stdexcept>
#include <string>
#include <thread>
#include <vector>

#include "package_inspector.hpp"
#include "protocol.hpp"
#include "runtime_evidence.hpp"
#include "session.hpp"
#include "terminal_publication.hpp"

namespace {

constexpr std::string_view package_digest =
    "b5316ac62e1e846b33ec92e5ad555859238af70ffd6b75aa50500995fbe15372";
constexpr std::string_view model_digest =
    "894dd0aac21e588d5cf78994d90aa0dcba8284626c976a4e0c89c0273b452c1c";

void require_terminal_mutation_rejected(
    const std::filesystem::path& source,
    const granite::official_worker::runtime_context& runtime,
    const nlohmann::json& event,
    std::size_t ordinal,
    bool turn,
    bool stopped) {
    using namespace granite::official_worker;
    const auto copy = std::filesystem::temp_directory_path() /
        (L"GraniteEdgeAI-TerminalPublication-" + std::to_wstring(GetCurrentProcessId()) +
         L"-" + std::to_wstring(ordinal));
    std::error_code ignored;
    std::filesystem::remove_all(copy, ignored);
    std::filesystem::copy(source, copy, std::filesystem::copy_options::recursive);

    try {
        package_lease package = acquire_package(
            copy, std::string(package_digest), std::string(model_digest), 88U);
        (void)inspect_package(package, runtime);

        std::unique_ptr<official_session> session;
        if (turn) {
            session = std::make_unique<official_session>(
                std::move(package), runtime, 64U, 64U);
            turn_control control;
            bool fragment_buffered = false;
            std::jthread pump([&] {
                std::unique_lock lock(control.mutex);
                fragment_buffered = control.changed.wait_for(
                    lock,
                    std::chrono::seconds(5),
                    [&] {
                        return control.first_fragment_buffered.load(
                            std::memory_order_acquire);
                    });
                if (!fragment_buffered) return;
                control.first_fragment_release.store(true, std::memory_order_release);
                control.notify();
                if (stopped) {
                    lock.unlock();
                    std::this_thread::sleep_for(std::chrono::milliseconds(50));
                    lock.lock();
                    control.stop.store(true, std::memory_order_release);
                    control.notify();
                }
            });
            const turn_result result = session->generate(
                "e39d252d-2144-4624-a055-0350c93f6728",
                "f77fb13c-263d-49a1-8d93-d908968c5832",
                "hello",
                2U,
                control);
            if (!fragment_buffered || result.stopped != stopped || result.answer.empty()) {
                throw std::runtime_error("real terminal turn fixture did not reach requested disposition");
            }
        }

        std::size_t package_checks = 0U;
        std::size_t runtime_checks = 0U;
        std::size_t module_checks = 0U;
        std::ostringstream emitted;
        auto* original = std::cout.rdbuf(emitted.rdbuf());
        bool rejected = false;
        try {
            publish_terminal_event(
                event,
                {
                    [&] {
                        ++package_checks;
                        if (session) {
                            session->verify_terminal_integrity();
                        } else {
                            package.verify_terminal_topology();
                        }
                    },
                    [&] {
                        ++runtime_checks;
                        runtime.verify_terminal_topology();
                    },
                    [&] {
                        ++module_checks;
                    },
                },
                [&] {
                    const auto transient = copy / L"after-final-native-boundary.tmp";
                    std::ofstream(transient) << "transient";
                    std::filesystem::remove(transient);
                });
        } catch (const worker_failure& failure) {
            rejected = failure.support_code() == "package_changed" && failure.fatal();
        }
        std::cout.rdbuf(original);
        if (!rejected || !emitted.str().empty()) {
            throw std::runtime_error("terminal namespace mutation published a success event");
        }
        if (package_checks != 1U || runtime_checks != 1U || module_checks != 1U) {
            throw std::runtime_error("terminal integrity checks were not independent");
        }
    } catch (...) {
        std::filesystem::remove_all(copy, ignored);
        throw;
    }
    std::filesystem::remove_all(copy, ignored);
}

}  // namespace

int main(int argc, char** argv) {
    using namespace granite::official_worker;
    if (argc != 3) return 2;
    try {
        const auto source = std::filesystem::absolute(argv[1]);
        runtime_context runtime = initialize_verified_runtime_at(
            std::filesystem::absolute(argv[2]), {}, {});
        require_terminal_mutation_rejected(
            source, runtime,
            {{"inspectionRunId", "e39d252d-2144-4624-a055-0350c93f6728"},
             {"eventType", "inspectionCompleted"}},
            1U, false, false);
        require_terminal_mutation_rejected(
            source, runtime,
            {{"sessionId", "e39d252d-2144-4624-a055-0350c93f6728"},
             {"turnId", "f77fb13c-263d-49a1-8d93-d908968c5832"},
             {"disposition", "completed"},
             {"eventType", "turnCompleted"}},
            2U, true, false);
        require_terminal_mutation_rejected(
            source, runtime,
            {{"sessionId", "e39d252d-2144-4624-a055-0350c93f6728"},
             {"turnId", "f77fb13c-263d-49a1-8d93-d908968c5832"},
             {"disposition", "stopped"},
             {"eventType", "turnCompleted"}},
            3U, true, true);
        std::cout << "terminal_publication_tests_passed\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
#include <memory>
#include <mutex>
