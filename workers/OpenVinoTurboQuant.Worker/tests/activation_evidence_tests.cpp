#include "activation_evidence.hpp"

#include <iostream>
#include <stdexcept>

int main() {
    try {
        const auto evidence = granite::official_worker::measure_turboquant_activation();
        if (evidence.runtime_dispatch_count == 0U ||
            evidence.encoded_record_count == 0U ||
            !evidence.forced_scalar_negative) {
            throw std::runtime_error("activation evidence was incomplete");
        }
        std::cout << "turboquant_activation_probe_passed\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
