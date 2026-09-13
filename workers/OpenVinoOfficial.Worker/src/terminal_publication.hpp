#pragma once

#include <functional>
#include <vector>

#include <nlohmann/json.hpp>

namespace granite::official_worker {

using terminal_integrity_check = std::function<void()>;
using terminal_publication_observer = std::function<void()>;

// Runs the instance observer first, then every integrity check independently.
// The event is written only after all checks succeed.
void publish_terminal_event(
    const nlohmann::json& event,
    const std::vector<terminal_integrity_check>& integrity_checks,
    const terminal_publication_observer& before_integrity = {});

}  // namespace granite::official_worker
