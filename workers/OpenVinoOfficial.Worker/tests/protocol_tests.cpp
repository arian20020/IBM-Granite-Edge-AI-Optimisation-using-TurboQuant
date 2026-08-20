#include "protocol.hpp"

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include <iostream>
#include <stdexcept>
#include <string>

namespace {

void require(bool condition, const char* message) {
    if (!condition) {
        throw std::runtime_error(message);
    }
}

template <typename Action>
void require_protocol_error(Action action, const char* message) {
    try {
        action();
    } catch (const granite::official_worker::protocol_error&) {
        return;
    }
    throw std::runtime_error(message);
}

}  // namespace

int main() {
    using namespace granite::official_worker;
    try {
        require(parse_arguments({"worker", "--protocol", "openvino.official/1"}) ==
                    "openvino.official/1",
                "fixed official argument grammar was rejected");
        require_protocol_error(
            [] { (void)parse_arguments({"worker", "--protocol", "openvino.official/1", "C:\\secret"}); },
            "package paths must never be accepted as arguments");
        require_protocol_error(
            [] { (void)parse_json_line(std::string(maximum_line_bytes + 1, 'x')); },
            "oversized JSONL was accepted");
        require_protocol_error(
            [] { (void)parse_json_line(R"({"commandType":"closeSession","sessionId":"00000000-0000-0000-0000-000000000001","extra":true})"); },
            "unknown fields were accepted");
        require_protocol_error(
            [] { (void)parse_json_line(R"({"commandType":"closeSession","sessionId":"00000000-0000-0000-0000-000000000001","sessionId":"00000000-0000-0000-0000-000000000002"})"); },
            "duplicate fields were accepted");
        require_protocol_error(
            [] { (void)parse_json_line(std::string(33, '[') + std::string(33, ']')); },
            "excessive JSON depth was accepted");
        require(validate_absolute_package_path(L"C:\\operation\\package"),
                "absolute package path was rejected");
        require(!validate_absolute_package_path(L"package"),
                "relative package path was accepted");
        HANDLE read_pipe = nullptr;
        HANDLE write_pipe = nullptr;
        require(CreatePipe(&read_pipe, &write_pipe, nullptr, 0) != FALSE,
                "test pipe creation failed");
        require(probe_input_pipe(read_pipe) == input_pipe_state::empty,
                "empty stdin pipe was treated as readable");
        const char frame[] = "{}\n";
        DWORD written = 0;
        require(WriteFile(write_pipe, frame, 3U, &written, nullptr) != FALSE && written == 3U,
                "test pipe write failed");
        require(probe_input_pipe(read_pipe) == input_pipe_state::available,
                "available stdin frame was not detected");
        CloseHandle(write_pipe);
        write_pipe = nullptr;
        char consumed[3]{};
        DWORD read = 0;
        require(ReadFile(read_pipe, consumed, 3U, &read, nullptr) != FALSE && read == 3U,
                "test pipe read failed");
        require(probe_input_pipe(read_pipe) == input_pipe_state::closed,
                "closed stdin pipe was not detected");
        CloseHandle(read_pipe);
        std::cout << "protocol_tests_passed\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
