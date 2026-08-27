#include <windows.h>

#include <atomic>
#include <filesystem>
#include <iostream>
#include <memory>
#include <stdexcept>
#include <string>

#include "namespace_monitor.hpp"

namespace {

using granite::official_worker::namespace_monitor;
using granite::official_worker::namespace_monitor_forced_outcome;
using granite::official_worker::namespace_monitor_lifecycle_probe;
using granite::official_worker::namespace_monitor_test_options;

[[noreturn]] void fail(const std::string& message) {
    throw std::runtime_error(message);
}

HANDLE open_directory(const std::filesystem::path& path) {
    const HANDLE handle = CreateFileW(
        path.c_str(),
        FILE_LIST_DIRECTORY | FILE_READ_ATTRIBUTES,
        FILE_SHARE_READ,
        nullptr,
        OPEN_EXISTING,
        FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT |
            FILE_FLAG_OVERLAPPED,
        nullptr);
    if (handle == INVALID_HANDLE_VALUE) fail("directory handle unavailable");
    return handle;
}

std::filesystem::path make_directory(const wchar_t* suffix) {
    const auto path = std::filesystem::temp_directory_path() /
        (std::wstring(L"granite-monitor-") + std::to_wstring(GetCurrentProcessId()) + suffix);
    std::error_code ignored;
    std::filesystem::remove_all(path, ignored);
    if (!std::filesystem::create_directory(path)) fail("test directory unavailable");
    return path;
}

void require_clean_shutdown() {
    const auto path = make_directory(L"-clean");
    auto probe = std::make_shared<namespace_monitor_lifecycle_probe>();
    namespace_monitor_test_options options;
    options.lifecycle = probe;
    {
        namespace_monitor monitor(open_directory(path), options);
        if (monitor.changed()) fail("unchanged directory reported a mutation");
    }
    if (probe->state_destroyed.load() != 1U ||
        probe->directory_closed.load() != 1U ||
        probe->event_closed.load() != 1U ||
        probe->detached.load() != 0U) {
        fail("normal cancellation did not release completed monitor state exactly once");
    }
    std::filesystem::remove_all(path);
}

void require_safe_detach(namespace_monitor_forced_outcome outcome, const wchar_t* suffix) {
    const auto path = make_directory(suffix);
    auto probe = std::make_shared<namespace_monitor_lifecycle_probe>();
    namespace_monitor_test_options options;
    options.shutdown_outcome = outcome;
    options.lifecycle = probe;
    {
        namespace_monitor monitor(open_directory(path), options);
    }
    if (probe->detached.load() != 1U ||
        probe->state_destroyed.load() != 0U ||
        probe->directory_closed.load() != 0U ||
        probe->event_closed.load() != 0U) {
        fail("unterminated overlapped request was destructed instead of safely detached");
    }
    // The intentionally detached production state owns the open handle until process exit.
    // Leave the test directory for the OS-owned temporary root cleanup.
}

void require_named_stream_policy() {
    {
        const auto path = make_directory(L"-streams-supported");
        namespace_monitor_test_options options;
        options.named_streams_supported = true;
        options.stream_arm_error = ERROR_INVALID_PARAMETER;
        bool rejected = false;
        try {
            namespace_monitor monitor(open_directory(path), options);
        } catch (const std::runtime_error&) {
            rejected = true;
        }
        if (!rejected) fail("named-stream-capable volume accepted stream-blind monitoring");
        std::filesystem::remove_all(path);
    }
    {
        const auto path = make_directory(L"-streams-unsupported");
        auto probe = std::make_shared<namespace_monitor_lifecycle_probe>();
        namespace_monitor_test_options options;
        options.named_streams_supported = false;
        options.stream_arm_error = ERROR_INVALID_PARAMETER;
        options.lifecycle = probe;
        { namespace_monitor monitor(open_directory(path), options); }
        if (probe->state_destroyed.load() != 1U) {
            fail("volume without named streams did not use the base-filter fallback");
        }
        std::filesystem::remove_all(path);
    }
}

void require_fail_closed_drain(namespace_monitor_forced_outcome outcome, const wchar_t* suffix) {
    const auto path = make_directory(suffix);
    namespace_monitor_test_options options;
    options.drain_outcome = outcome;
    namespace_monitor monitor(open_directory(path), options);
    if (!monitor.changed(true)) fail("forced monitor completion fault did not fail closed");
    std::error_code ignored;
    std::filesystem::remove_all(path, ignored);
}

void require_delayed_terminal_completion_fails_closed() {
    const auto path = make_directory(L"-terminal-delay");
    namespace_monitor_test_options options;
    options.terminal_outcome = namespace_monitor_forced_outcome::timeout;
    namespace_monitor monitor(open_directory(path), options);
    if (!monitor.terminal_barrier()) {
        fail("terminal publication barrier escaped delayed kernel completion");
    }
    std::error_code ignored;
    std::filesystem::remove_all(path, ignored);
}

void require_replacement_covers_handoff_mutation() {
    const auto path = make_directory(L"-handoff");
    namespace_monitor monitor(open_directory(path));
    if (monitor.terminal_barrier()) fail("clean initial handoff failed");
    const auto transient = path / L"during-rescan.tmp";
    HANDLE file = CreateFileW(
        transient.c_str(), GENERIC_WRITE, 0U, nullptr, CREATE_NEW,
        FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE) fail("handoff mutation fixture unavailable");
    CloseHandle(file);
    std::filesystem::remove(transient);
    if (!monitor.terminal_barrier()) {
        fail("replacement watcher missed mutation during topology rescan");
    }
    std::error_code ignored;
    std::filesystem::remove_all(path, ignored);
}

}  // namespace

int main() {
    try {
        require_clean_shutdown();
        require_safe_detach(namespace_monitor_forced_outcome::timeout, L"-timeout");
        require_safe_detach(namespace_monitor_forced_outcome::wait_error, L"-wait-error");
        require_named_stream_policy();
        require_fail_closed_drain(namespace_monitor_forced_outcome::overflow, L"-overflow");
        require_fail_closed_drain(namespace_monitor_forced_outcome::io_error, L"-io-error");
        require_delayed_terminal_completion_fails_closed();
        require_replacement_covers_handoff_mutation();
        std::cout << "namespace_monitor_tests_passed\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
