#include "session.hpp"

#include <iostream>
#include <stdexcept>

int main() {
    using namespace granite::official_worker;
    try {
        if (!fits_context(4, 2, 64, 64)) {
            throw std::runtime_error("valid context was rejected");
        }
        if (fits_context(63, 2, 64, 128)) {
            throw std::runtime_error("model context overflow was accepted");
        }
        if (fits_context(63, 2, 128, 64)) {
            throw std::runtime_error("C1 context overflow was accepted");
        }
        session_state state;
        state.accept_prompt();
        state.begin_generation();
        state.request_stop();
        state.complete_stopped_turn();
        state.accept_prompt();
        state.begin_generation();
        state.complete_turn();
        state.close();
        if (!state.is_terminal()) {
            throw std::runtime_error("graceful close was not terminal");
        }
        std::cout << "session_tests_passed\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
