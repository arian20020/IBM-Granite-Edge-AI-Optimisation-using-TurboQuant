#include "namespace_monitor.hpp"

#include <windows.h>

#include <array>
#include <atomic>
#include <cstddef>
#include <memory>
#include <stdexcept>
#include <utility>
#include <vector>

namespace granite::official_worker {
namespace {

constexpr DWORD base_change_filter =
    FILE_NOTIFY_CHANGE_FILE_NAME |
    FILE_NOTIFY_CHANGE_DIR_NAME |
    FILE_NOTIFY_CHANGE_SIZE |
    FILE_NOTIFY_CHANGE_LAST_WRITE |
    FILE_NOTIFY_CHANGE_CREATION |
    FILE_NOTIFY_CHANGE_SECURITY;

constexpr DWORD stream_change_filter =
    0x00000200U |  // FILE_NOTIFY_CHANGE_STREAM_NAME
    0x00000400U |  // FILE_NOTIFY_CHANGE_STREAM_SIZE
    0x00000800U;   // FILE_NOTIFY_CHANGE_STREAM_WRITE

constexpr DWORD named_streams_capability = 0x00040000U;  // FILE_NAMED_STREAMS
constexpr DWORD shutdown_timeout_ms = 5000U;

}  // namespace

struct namespace_monitor::state final {
    struct request final {
        HANDLE event{nullptr};
        OVERLAPPED overlapped{};
        alignas(DWORD) std::array<std::byte, 64U * 1024U> buffer{};
        bool pending{};
        std::shared_ptr<namespace_monitor_lifecycle_probe> lifecycle;

        explicit request(std::shared_ptr<namespace_monitor_lifecycle_probe> probe)
            : lifecycle(std::move(probe)) {
            event = CreateEventW(nullptr, TRUE, FALSE, nullptr);
            if (event == nullptr) {
                throw std::runtime_error("namespace monitor event unavailable");
            }
            overlapped.hEvent = event;
        }

        ~request() {
            if (event != nullptr) {
                CloseHandle(event);
                event = nullptr;
                if (lifecycle) lifecycle->event_closed.fetch_add(1U);
            }
        }
    };

    HANDLE directory{INVALID_HANDLE_VALUE};
    DWORD filter{};
    mutable std::vector<std::unique_ptr<request>> requests;
    mutable std::atomic_bool sticky{false};
    namespace_monitor_test_options options;

    state(HANDLE owned_directory, namespace_monitor_test_options injected)
        : directory(owned_directory), options(std::move(injected)) {
        if (directory == INVALID_HANDLE_VALUE || directory == nullptr) {
            throw std::runtime_error("namespace monitor handle unavailable");
        }
        try {
            // Reserve before any asynchronous request is armed so allocation
            // failure cannot destroy storage still owned by the kernel.
            requests.reserve(2U);
            DWORD volume_flags = 0U;
            if (!GetVolumeInformationByHandleW(
                    directory, nullptr, 0U, nullptr, nullptr, &volume_flags,
                    nullptr, 0U)) {
                throw std::runtime_error("namespace monitor volume unavailable");
            }
            const bool named_streams_supported = options.named_streams_supported
                .value_or((volume_flags & named_streams_capability) != 0U);

            auto initial = std::make_unique<request>(options.lifecycle);
            filter = base_change_filter | stream_change_filter;
            if (!arm(*initial, filter, true)) {
                const DWORD error = GetLastError();
                if (error != ERROR_INVALID_PARAMETER || named_streams_supported) {
                    throw std::runtime_error("namespace monitor unavailable");
                }
                filter = base_change_filter;
                if (!arm(*initial, filter, false)) {
                    throw std::runtime_error("namespace monitor unavailable");
                }
            }
            requests.push_back(std::move(initial));
        } catch (...) {
            CloseHandle(directory);
            directory = INVALID_HANDLE_VALUE;
            if (options.lifecycle) options.lifecycle->directory_closed.fetch_add(1U);
            throw;
        }
    }

    ~state() {
        if (directory != INVALID_HANDLE_VALUE && directory != nullptr) {
            CloseHandle(directory);
            directory = INVALID_HANDLE_VALUE;
            if (options.lifecycle) options.lifecycle->directory_closed.fetch_add(1U);
        }
        if (options.lifecycle) options.lifecycle->state_destroyed.fetch_add(1U);
    }

    bool arm(request& target, DWORD requested_filter, bool includes_streams) const noexcept {
        ResetEvent(target.event);
        target.overlapped.Internal = 0U;
        target.overlapped.InternalHigh = 0U;
        target.overlapped.Offset = 0U;
        target.overlapped.OffsetHigh = 0U;
        if (includes_streams && options.stream_arm_error.has_value()) {
            SetLastError(static_cast<DWORD>(*options.stream_arm_error));
            return false;
        }
        const BOOL started = ReadDirectoryChangesW(
            directory, target.buffer.data(), static_cast<DWORD>(target.buffer.size()),
            TRUE, requested_filter, nullptr, &target.overlapped, nullptr);
        target.pending = started != FALSE;
        return target.pending;
    }

    [[nodiscard]] bool consume_terminal(
        request& target,
        namespace_monitor_forced_outcome forced,
        bool notification_is_failure) const noexcept {
        if (forced == namespace_monitor_forced_outcome::timeout ||
            forced == namespace_monitor_forced_outcome::wait_error) {
            sticky.store(true, std::memory_order_release);
            return false;
        }
        const DWORD wait = WaitForSingleObject(target.event, shutdown_timeout_ms);
        if (wait != WAIT_OBJECT_0) {
            sticky.store(true, std::memory_order_release);
            return false;
        }
        DWORD transferred = 0U;
        const BOOL completed = GetOverlappedResult(
            directory, &target.overlapped, &transferred, FALSE);
        const DWORD error = completed ? ERROR_SUCCESS : GetLastError();
        if (!completed && error == ERROR_IO_INCOMPLETE) {
            sticky.store(true, std::memory_order_release);
            return false;
        }
        target.pending = false;
        if (forced == namespace_monitor_forced_outcome::overflow ||
            forced == namespace_monitor_forced_outcome::io_error) {
            sticky.store(true, std::memory_order_release);
        } else if (completed) {
            if (notification_is_failure || transferred == 0U) {
                sticky.store(true, std::memory_order_release);
            }
        } else if (error != ERROR_OPERATION_ABORTED) {
            sticky.store(true, std::memory_order_release);
        }
        return true;
    }

    [[nodiscard]] bool handoff(namespace_monitor_forced_outcome forced) const noexcept {
        if (sticky.load(std::memory_order_acquire)) return true;
        try {
            // Capacity must be secured before arming the replacement request.
            requests.reserve(requests.size() + 1U);
            auto replacement = std::make_unique<request>(options.lifecycle);
            if (!arm(*replacement, filter, false)) {
                sticky.store(true, std::memory_order_release);
                return true;
            }
            request& previous = *requests.back();
            requests.push_back(std::move(replacement));
            if (!CancelIoEx(directory, &previous.overlapped) &&
                GetLastError() != ERROR_NOT_FOUND) {
                sticky.store(true, std::memory_order_release);
            }
            if (!consume_terminal(previous, forced, true)) return true;
            requests.erase(requests.end() - 2);
            return sticky.load(std::memory_order_acquire);
        } catch (...) {
            sticky.store(true, std::memory_order_release);
            return true;
        }
    }

    [[nodiscard]] bool changed(bool drain_notifications) const noexcept {
        if (sticky.load(std::memory_order_acquire)) return true;
        if (drain_notifications) return handoff(options.drain_outcome);
        request& active = *requests.back();
        const DWORD wait = WaitForSingleObject(active.event, 0U);
        if (wait == WAIT_TIMEOUT) return false;
        if (wait != WAIT_OBJECT_0) {
            sticky.store(true, std::memory_order_release);
            return true;
        }
        DWORD transferred = 0U;
        if (!GetOverlappedResult(
                directory, &active.overlapped, &transferred, FALSE) ||
            transferred == 0U) {
            sticky.store(true, std::memory_order_release);
            return true;
        }
        active.pending = false;
        sticky.store(true, std::memory_order_release);
        return true;
    }

    [[nodiscard]] bool shutdown() noexcept {
        bool terminal = true;
        for (auto& owned : requests) {
            request& current = *owned;
            if (!current.pending) continue;
            if (!CancelIoEx(directory, &current.overlapped) &&
                GetLastError() != ERROR_NOT_FOUND) {
                sticky.store(true, std::memory_order_release);
            }
            if (!consume_terminal(current, options.shutdown_outcome, false)) {
                terminal = false;
            }
        }
        return terminal;
    }
};

namespace_monitor::namespace_monitor(void* owned_directory_handle)
    : namespace_monitor(owned_directory_handle, namespace_monitor_test_options{}) {}

namespace_monitor::namespace_monitor(
    void* owned_directory_handle,
    namespace_monitor_test_options options)
    : state_(new state(static_cast<HANDLE>(owned_directory_handle), std::move(options))) {}

namespace_monitor::~namespace_monitor() { release_state(); }

namespace_monitor::namespace_monitor(namespace_monitor&& other) noexcept
    : state_(std::exchange(other.state_, nullptr)) {}

namespace_monitor& namespace_monitor::operator=(namespace_monitor&& other) noexcept {
    if (this != &other) {
        release_state();
        state_ = std::exchange(other.state_, nullptr);
    }
    return *this;
}

void namespace_monitor::release_state() noexcept {
    if (state_ == nullptr) return;
    if (state_->shutdown()) {
        delete state_;
    } else {
        if (state_->options.lifecycle) {
            state_->options.lifecycle->detached.fetch_add(1U);
        }
        // The complete state intentionally remains live until process exit: a
        // pending OVERLAPPED and its buffer may not be closed or freed safely.
    }
    state_ = nullptr;
}

bool namespace_monitor::changed(bool drain_notifications) const noexcept {
    return state_ == nullptr || state_->changed(drain_notifications);
}

bool namespace_monitor::terminal_barrier() const noexcept {
    return state_ == nullptr || state_->handoff(state_->options.terminal_outcome);
}

}  // namespace granite::official_worker
