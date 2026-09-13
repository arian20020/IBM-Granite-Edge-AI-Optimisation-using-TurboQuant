#pragma once

#include <atomic>
#include <cstdint>
#include <memory>
#include <optional>

namespace granite::official_worker {

enum class namespace_monitor_forced_outcome {
    none,
    timeout,
    wait_error,
    overflow,
    io_error
};

struct namespace_monitor_lifecycle_probe final {
    std::atomic_uint state_destroyed{0U};
    std::atomic_uint directory_closed{0U};
    std::atomic_uint event_closed{0U};
    std::atomic_uint detached{0U};
};

// Instance-scoped fault injection for native ownership tests. Production uses
// default values and never consults mutable global state.
struct namespace_monitor_test_options final {
    std::optional<bool> named_streams_supported;
    std::optional<std::uint32_t> stream_arm_error;
    namespace_monitor_forced_outcome drain_outcome{
        namespace_monitor_forced_outcome::none};
    namespace_monitor_forced_outcome terminal_outcome{
        namespace_monitor_forced_outcome::none};
    namespace_monitor_forced_outcome shutdown_outcome{
        namespace_monitor_forced_outcome::none};
    std::shared_ptr<namespace_monitor_lifecycle_probe> lifecycle;
};

class namespace_monitor final {
public:
    explicit namespace_monitor(void* owned_directory_handle);
    namespace_monitor(
        void* owned_directory_handle,
        namespace_monitor_test_options options);
    ~namespace_monitor();
    namespace_monitor(namespace_monitor&&) noexcept;
    namespace_monitor& operator=(namespace_monitor&&) noexcept;
    namespace_monitor(const namespace_monitor&) = delete;
    namespace_monitor& operator=(const namespace_monitor&) = delete;

    [[nodiscard]] bool changed(bool drain_notifications = false) const noexcept;
    [[nodiscard]] bool terminal_barrier() const noexcept;

private:
    struct state;
    void release_state() noexcept;
    state* state_{};
};

}  // namespace granite::official_worker
