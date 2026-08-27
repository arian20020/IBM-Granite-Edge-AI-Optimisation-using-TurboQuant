#include "session.hpp"
#include "runtime_evidence.hpp"
#include "protocol.hpp"

#include <iostream>
#include <stdexcept>

int main() {
    using namespace granite::official_worker;
    try {
        for (std::string_view device : {"CPU", "GPU", "GPU.0", "GPU.12"}) {
            if (!is_explicit_execution_device(device)) {
                throw std::runtime_error("explicit device was rejected");
            }
        }
        for (std::string_view device : {
                 "AUTO", "HETERO", "MULTI", "NPU", "gpu", "GPU.",
                 "GPU.-1", "GPU.01", " GPU", "GPU "}) {
            if (is_explicit_execution_device(device)) {
                throw std::runtime_error("implicit or malformed device was accepted");
            }
        }
        bool mismatch_rejected = false;
        try {
            require_execution_device_match("GPU.0", {"CPU"});
        } catch (const worker_failure& failure) {
            mismatch_rejected = failure.support_code() == "runtime_device_mismatch";
        }
        if (!mismatch_rejected) {
            throw std::runtime_error("CPU resolution mismatch was not typed");
        }
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
