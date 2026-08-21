#include "namespace_monitor.hpp"

#include <windows.h>

#include <array>
#include <atomic>
#include <cstddef>
#include <stdexcept>

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
    0x00000200U |  // FILE_NOTIFY_CHANGE_STREAM_NAME (NTFS, when supported)
    0x00000400U |  // FILE_NOTIFY_CHANGE_STREAM_SIZE
    0x00000800U;   // FILE_NOTIFY_CHANGE_STREAM_WRITE

}  // namespace

struct namespace_monitor::state final {
    HANDLE directory{INVALID_HANDLE_VALUE};
    HANDLE event{nullptr};
    mutable OVERLAPPED overlapped{};
    alignas(DWORD) std::array<std::byte, 64U * 1024U> buffer{};
    mutable std::atomic_bool sticky{false};

    explicit state(HANDLE owned_directory) : directory(owned_directory) {
        if (directory == INVALID_HANDLE_VALUE || directory == nullptr) {
            throw std::runtime_error("namespace monitor handle unavailable");
        }
        event = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        if (event == nullptr) {
            CloseHandle(directory);
            directory = INVALID_HANDLE_VALUE;
            throw std::runtime_error("namespace monitor event unavailable");
        }
        overlapped.hEvent = event;
        if (!arm(base_change_filter | stream_change_filter)) {
            const DWORD error = GetLastError();
            if (error != ERROR_INVALID_PARAMETER || !arm(base_change_filter)) {
                CloseHandle(event);
                CloseHandle(directory);
                event = nullptr;
                directory = INVALID_HANDLE_VALUE;
                throw std::runtime_error("namespace monitor unavailable");
            }
        }
    }

    ~state() {
        if (directory != INVALID_HANDLE_VALUE && directory != nullptr) {
            if (!CancelIoEx(directory, &overlapped) &&
                GetLastError() != ERROR_NOT_FOUND) {
                sticky.store(true, std::memory_order_release);
            }
            if (WaitForSingleObject(event, 5000U) != WAIT_OBJECT_0) {
                sticky.store(true, std::memory_order_release);
            }
            CloseHandle(directory);
            directory = INVALID_HANDLE_VALUE;
        }
        if (event != nullptr) {
            CloseHandle(event);
            event = nullptr;
        }
    }

    bool arm(DWORD filter) noexcept {
        ResetEvent(event);
        overlapped.Internal = 0U;
        overlapped.InternalHigh = 0U;
        overlapped.Offset = 0U;
        overlapped.OffsetHigh = 0U;
        return ReadDirectoryChangesW(
                   directory,
                   buffer.data(),
                   static_cast<DWORD>(buffer.size()),
                   TRUE,
                   filter,
                   nullptr,
                   &overlapped,
                   nullptr) != FALSE;
    }

    [[nodiscard]] bool changed(bool drain_notifications) const noexcept {
        if (sticky.load(std::memory_order_acquire)) return true;
        // Drain completion of the request that was armed before enumeration.
        // This does not open a new observation window; it only gives the kernel
        // a bounded opportunity to publish an already-recorded change.
        const DWORD wait = WaitForSingleObject(
            event, drain_notifications ? 50U : 0U);
        if (wait == WAIT_TIMEOUT) return false;
        if (wait != WAIT_OBJECT_0) {
            sticky.store(true, std::memory_order_release);
            return true;
        }
        DWORD transferred = 0U;
        if (!GetOverlappedResult(directory, &overlapped, &transferred, FALSE) ||
            transferred == 0U) {
            sticky.store(true, std::memory_order_release);
            return true;
        }
        sticky.store(true, std::memory_order_release);
        return true;
    }
};

namespace_monitor::namespace_monitor(void* owned_directory_handle)
    : state_(std::make_unique<state>(static_cast<HANDLE>(owned_directory_handle))) {}

namespace_monitor::~namespace_monitor() = default;
namespace_monitor::namespace_monitor(namespace_monitor&&) noexcept = default;
namespace_monitor& namespace_monitor::operator=(namespace_monitor&&) noexcept = default;

bool namespace_monitor::changed(bool drain_notifications) const noexcept {
    return state_ == nullptr || state_->changed(drain_notifications);
}

}  // namespace granite::official_worker
