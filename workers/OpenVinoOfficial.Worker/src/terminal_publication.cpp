#include "terminal_publication.hpp"

#include <exception>

#include "protocol.hpp"

namespace granite::official_worker {

void publish_terminal_event(
    const nlohmann::json& event,
    const std::vector<terminal_integrity_check>& integrity_checks,
    const terminal_publication_observer& before_integrity) {
    if (before_integrity) before_integrity();
    std::exception_ptr first_failure;
    for (const auto& check : integrity_checks) {
        try {
            check();
        } catch (...) {
            if (first_failure == nullptr) first_failure = std::current_exception();
        }
    }
    if (first_failure != nullptr) std::rethrow_exception(first_failure);
    write_event(event);
}

}  // namespace granite::official_worker
