#pragma once

#include <memory>

namespace granite::official_worker {

class namespace_monitor final {
public:
    explicit namespace_monitor(void* owned_directory_handle);
    ~namespace_monitor();
    namespace_monitor(namespace_monitor&&) noexcept;
    namespace_monitor& operator=(namespace_monitor&&) noexcept;
    namespace_monitor(const namespace_monitor&) = delete;
    namespace_monitor& operator=(const namespace_monitor&) = delete;

    [[nodiscard]] bool changed(bool drain_notifications = false) const noexcept;

private:
    struct state;
    std::unique_ptr<state> state_;
};

}  // namespace granite::official_worker
