#include "protocol.hpp"
#include "runtime_evidence.hpp"

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
        const auto ordinary_prompt = parse_json_line(R"({"sessionId":"00000000-0000-0000-0000-000000000001","turnId":"00000000-0000-0000-0000-000000000002","prompt":"title","requestedNewTokens":32,"commandType":"prompt"})");
        auto title_prompt = ordinary_prompt;
        for (bool enabled : {true, false}) {
            title_prompt["isTransientTitle"] = enabled;
            require(parse_json_line(title_prompt.dump()).at("isTransientTitle") == enabled, "boolean title flag rejected");
        }
        for (const auto& invalid : {nlohmann::json(nullptr), nlohmann::json("true"), nlohmann::json(1)}) {
            title_prompt["isTransientTitle"] = invalid;
            require_protocol_error([&] { (void)parse_json_line(title_prompt.dump()); }, "non-boolean title flag accepted");
        }
        title_prompt["isTransientTitle"] = true;
        title_prompt["extra"] = true;
        require_protocol_error([&] { (void)parse_json_line(title_prompt.dump()); }, "unknown title field accepted");
        require_protocol_error([] { (void)parse_json_line(R"({"sessionId":"s","turnId":"t","prompt":"title","requestedNewTokens":32,"commandType":"prompt","isTransientTitle":true,"isTransientTitle":false})"); }, "duplicate title flag accepted");
        const std::string replay_command = R"({"sessionId":"00000000-0000-0000-0000-000000000001","inspectionRunId":"00000000-0000-0000-0000-000000000002","packagePath":"C:\\operation\\package","packageManifestDigest":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","modelSha256":"abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789","modelLengthBytes":1,"device":{"deviceId":"CPU"},"limits":{"maximumContextTokens":64,"maximumNewTokens":1},"runtime":{"kvCachePrecision":"u8"},"commandType":"startSession","initialHistory":[{"role":"user","content":"violet"},{"role":"assistant","content":"remembered"}]})";
        const auto replay = parse_json_line(replay_command);
        std::string duplicate_replay = replay_command;
        const auto role_position = duplicate_replay.find("\"role\":\"user\"");
        duplicate_replay.insert(role_position, "\"role\":\"assistant\",");
        require_protocol_error([&] { (void)parse_json_line(duplicate_replay); }, "duplicate role inside history turn accepted");
        require(replay.at("initialHistory").at(1).at("role") == "assistant", "assistant role lost during replay");
        auto invalid_replay = replay;
        invalid_replay["initialHistory"][0]["role"] = "tool";
        require_protocol_error([&] { (void)parse_json_line(invalid_replay.dump()); }, "unsupported history role accepted");
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
            [] { (void)parse_json_line(R"({"sessionId":"00000000-0000-0000-0000-000000000001","inspectionRunId":"00000000-0000-0000-0000-000000000002","packagePath":"C:\\operation\\package","packageManifestDigest":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","modelSha256":"abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789","modelLengthBytes":1,"device":{"deviceId":"CPU"},"limits":{"maximumContextTokens":64,"maximumNewTokens":1},"commandType":"startSession"})"); },
            "startSession without a runtime policy was accepted");
        (void)parse_json_line(R"({"sessionId":"00000000-0000-0000-0000-000000000001","inspectionRunId":"00000000-0000-0000-0000-000000000002","packagePath":"C:\\operation\\package","packageManifestDigest":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","modelSha256":"abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789","modelLengthBytes":1,"device":{"deviceId":"CPU"},"limits":{"maximumContextTokens":64,"maximumNewTokens":1},"runtime":{"kvCachePrecision":"u8"},"commandType":"startSession"})");
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
        bool failed_load_verified = false;
        bool integrity_failure_preserved = false;
        try {
            (void)verify_execution_device(
                L"missing-openvino-model.xml",
                "GPU",
                [&] {
                    failed_load_verified = true;
                    throw worker_failure(
                        "runtime_integrity_failed",
                        false,
                        "runtime integrity failed");
                });
        } catch (const worker_failure& failure) {
            integrity_failure_preserved =
                failure.support_code() == "runtime_integrity_failed";
        }
        require(failed_load_verified,
                "failed runtime load bypassed module verification");
        require(integrity_failure_preserved,
                "runtime integrity failure was masked as device unavailability");
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
