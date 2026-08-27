#include "package_inspector.hpp"
#include "protocol.hpp"
#include "runtime_evidence.hpp"
#include "session.hpp"
#include "terminal_publication.hpp"
#if defined(GRANITE_TURBOQUANT_WORKER)
#include "activation_evidence.hpp"
#endif

#define NOMINMAX
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <fcntl.h>
#include <io.h>

#include <chrono>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <future>
#include <iostream>
#include <limits>
#include <string>
#include <thread>
#include <utility>
#include <vector>

namespace granite::official_worker {
namespace {

using json = nlohmann::json;

std::string read_bounded_line() {
    std::string line;
    line.reserve(4096U);
    char value = '\0';
    while (std::cin.get(value)) {
        if (value == '\n') {
            if (!line.empty() && line.back() == '\r') line.pop_back();
            return line;
        }
        if (line.size() == maximum_line_bytes) throw protocol_error("input line exceeded");
        line.push_back(value);
    }
    if (!line.empty()) return line;
    throw protocol_error("input ended");
}

std::string required_string(const json& value, const char* key, std::size_t maximum = 32768U) {
    if (!value.at(key).is_string()) throw protocol_error("string field invalid");
    const std::string result = value.at(key).get<std::string>();
    if (result.empty() || result.size() > maximum || result.find('\0') != std::string::npos) {
        throw protocol_error("string field invalid");
    }
    return result;
}

std::uintmax_t required_positive_u64(const json& value, const char* key) {
    if ((!value.at(key).is_number_unsigned() && !value.at(key).is_number_integer())) {
        throw protocol_error("integer field invalid");
    }
    const std::int64_t signed_value = value.at(key).get<std::int64_t>();
    if (signed_value <= 0) throw protocol_error("integer field invalid");
    return static_cast<std::uintmax_t>(signed_value);
}

std::filesystem::path required_package_path(const json& command) {
    const std::string encoded = required_string(command, "packagePath", 32U * 1024U);
    const std::wstring decoded = utf8_to_wide(encoded);
    if (!validate_absolute_package_path(decoded)) throw protocol_error("package path invalid");
    return std::filesystem::path(decoded);
}

void validate_id(const std::string& value) {
    if (!is_canonical_uuid(value)) throw protocol_error("identifier invalid");
}

void validate_digest(const std::string& value) {
    if (!is_lower_sha256(value)) throw protocol_error("digest invalid");
}

std::size_t model_context_limit(const std::filesystem::path& package) {
    std::ifstream input(package / L"config.json", std::ios::binary);
    json config;
    try {
        input >> config;
    } catch (const json::exception&) {
        throw protocol_error("model configuration invalid");
    }
    if (!config.is_object() || !config.contains("max_position_embeddings")) {
        throw protocol_error("model context unknown");
    }
    return static_cast<std::size_t>(required_positive_u64(config, "max_position_embeddings"));
}

void emit_session_failure(const std::string& session_id, std::string_view code) {
    write_event({{"sessionId", session_id}, {"supportCode", code}, {"eventType", "sessionFailed"}});
}

void run_inspection(const json& command, const runtime_context& runtime) {
    const std::string run_id = required_string(command, "inspectionRunId", 36U);
    validate_id(run_id);
    const std::filesystem::path package = required_package_path(command);
    const std::string package_digest = required_string(command, "packageManifestDigest", 64U);
    const std::string model_digest = required_string(command, "modelSha256", 64U);
    validate_digest(package_digest);
    validate_digest(model_digest);
    const std::uintmax_t model_length = required_positive_u64(command, "modelLengthBytes");

    write_event({{"inspectionRunId", run_id}, {"eventType", "inspectionStarted"}});
    try {
        package_lease lease = acquire_package(
            package, package_digest, model_digest, model_length);
        const package_evidence evidence = inspect_package(
            lease, runtime, {}, [&] { verify_loaded_module_closure(runtime); });
        write_event({{"inspectionRunId", run_id}, {"stage", "manifestVerified"}, {"eventType", "inspectionProgress"}});
        write_event({{"inspectionRunId", run_id}, {"stage", "mainModelParsed"}, {"eventType", "inspectionProgress"}});
        write_event({{"inspectionRunId", run_id}, {"stage", "tokenizerParsed"}, {"eventType", "inspectionProgress"}});
        write_event({{"inspectionRunId", run_id}, {"stage", "detokenizerParsed"}, {"eventType", "inspectionProgress"}});
        publish_terminal_event(
            {{"inspectionRunId", run_id},
             {"packageManifestDigest", evidence.package_digest},
             {"modelSha256", evidence.model_digest},
             {"modelLengthBytes", evidence.model_length},
             {"mainModelParsed", true},
             {"tokenizerParsed", true},
             {"detokenizerParsed", true},
             {"buildEvidence", runtime.evidence().to_json()},
             {"eventType", "inspectionCompleted"}},
            {
                [&] { lease.verify_terminal_topology(); },
                [&] { runtime.verify_terminal_topology(); },
                [&] { verify_loaded_module_closure(runtime); },
            });
    } catch (const worker_failure& failure) {
        write_event({{"inspectionRunId", run_id},
                     {"supportCode", failure.support_code()},
                     {"eventType", "inspectionFailed"}});
        throw;
    } catch (...) {
        write_event({{"inspectionRunId", run_id},
                     {"supportCode", "runtime_load_failed"},
                     {"eventType", "inspectionFailed"}});
        throw;
    }
}

void run_session_body(const json& command, const runtime_context& runtime) {
    const std::string session_id = required_string(command, "sessionId", 36U);
    const std::string inspection_id = required_string(command, "inspectionRunId", 36U);
    validate_id(session_id);
    validate_id(inspection_id);
    const std::filesystem::path package = required_package_path(command);
    const std::string package_digest = required_string(command, "packageManifestDigest", 64U);
    const std::string model_digest = required_string(command, "modelSha256", 64U);
    validate_digest(package_digest);
    validate_digest(model_digest);
    const std::uintmax_t model_length = required_positive_u64(command, "modelLengthBytes");
    const std::string device = required_string(command.at("device"), "deviceId", 128U);
    if (!is_explicit_execution_device(device)) throw protocol_error("device rejected");
    const std::size_t c1_context = static_cast<std::size_t>(
        required_positive_u64(command.at("limits"), "maximumContextTokens"));
    const std::size_t session_new_tokens = static_cast<std::size_t>(
        required_positive_u64(command.at("limits"), "maximumNewTokens"));
    if (session_new_tokens > 512U) throw protocol_error("generation limit rejected");
    const std::string kv_cache_precision = required_string(
        command.at("runtime"), "kvCachePrecision", 32U);
#if defined(GRANITE_TURBOQUANT_WORKER)
    if (kv_cache_precision != "tbq4") {
#else
    if (kv_cache_precision != "released-default" && kv_cache_precision != "u8") {
#endif
        throw protocol_error("KV-cache precision rejected");
    }

    package_lease lease = acquire_package(
        package, package_digest, model_digest, model_length);
    (void)inspect_package(
        lease, runtime, {}, [&] { verify_loaded_module_closure(runtime); });
    lease.verify_topology();
    const std::size_t model_context = model_context_limit(package);
    lease.verify_topology(true);
    const verified_execution_device execution = verify_execution_device(
        package / L"openvino_model.xml",
        device,
        [&] { verify_loaded_module_closure(runtime); });
    lease.verify_topology(true);
    official_session session(
        std::move(lease), runtime, device, model_context, c1_context, {},
        [&] { verify_loaded_module_closure(runtime); }, kv_cache_precision);
#if defined(GRANITE_TURBOQUANT_WORKER)
    const turboquant_activation_evidence activation = measure_turboquant_activation(
        package / L"openvino_model.xml");
#endif
    verify_loaded_module_closure(runtime);
    publish_terminal_event(
        {{"sessionId", session_id},
         {"requestedDevice", execution.requested},
         {"actualExecutionDevices", execution.actual},
         {"requestedKvCachePrecision", kv_cache_precision},
         {"actualKvCachePrecision", kv_cache_precision},
         {"protocolId", official_protocol},
         {"buildEvidence", runtime.evidence().to_json()},
         {"eventType", "sessionStarted"}},
        {[&] { session.verify_terminal_integrity(); }});

    std::size_t turn_count = 0;
    while (true) {
        const json next = parse_json_line(read_bounded_line());
        const std::string type = required_string(next, "commandType", 32U);
        const std::string command_session = required_string(next, "sessionId", 36U);
        validate_id(command_session);
        if (command_session != session_id) throw protocol_error("session identifier mismatch");
        if (type == "closeSession") {
            publish_terminal_event(
                {{"sessionId", session_id}, {"eventType", "sessionCompleted"}},
                {[&] { session.verify_terminal_integrity(); }});
            return;
        }
        if (type == "cancelSession") {
            publish_terminal_event(
                {{"sessionId", session_id}, {"eventType", "sessionCancelled"}},
                {[&] { session.verify_terminal_integrity(); }});
            return;
        }
        if (type != "prompt" || ++turn_count > 32U) throw protocol_error("session command rejected");

        const std::string turn_id = required_string(next, "turnId", 36U);
        validate_id(turn_id);
        const std::string prompt = required_string(next, "prompt", 64U * 1024U);
        const std::size_t requested = static_cast<std::size_t>(
            required_positive_u64(next, "requestedNewTokens"));
        if (requested > session_new_tokens || requested > 512U) {
            write_event({{"sessionId", session_id}, {"turnId", turn_id},
                         {"supportCode", "runtime_protocol_failed"}, {"eventType", "turnFailed"}});
            continue;
        }

        write_event({{"sessionId", session_id}, {"turnId", turn_id}, {"eventType", "generationStarted"}});
        turn_control control;
        std::future<turn_result> generation = std::async(std::launch::async, [&] {
            turn_result result = session.generate(session_id, turn_id, prompt, requested, control);
            verify_loaded_module_closure(runtime);
            return result;
        });
        bool cancelled = false;
        while (generation.wait_for(std::chrono::milliseconds(2)) != std::future_status::ready) {
            const input_pipe_state input = probe_input_pipe(GetStdHandle(STD_INPUT_HANDLE));
            if (input == input_pipe_state::empty) {
                if (control.first_fragment_buffered.load(std::memory_order_acquire) &&
                    !control.first_fragment_release.load(std::memory_order_acquire)) {
                    {
                        std::lock_guard lock(control.mutex);
                        control.first_fragment_release.store(true, std::memory_order_release);
                    }
                    control.notify();
                }
                continue;
            }
            if (input == input_pipe_state::closed) {
                {
                    std::lock_guard lock(control.mutex);
                    control.cancel.store(true, std::memory_order_release);
                }
                control.notify();
                cancelled = true;
                continue;
            }
            const json control_command = parse_json_line(read_bounded_line());
            const std::string control_type = required_string(control_command, "commandType", 32U);
            const std::string control_session = required_string(control_command, "sessionId", 36U);
            validate_id(control_session);
            if (control_session != session_id) throw protocol_error("session identifier mismatch");
            if (control_type == "stopTurn") {
                const std::string control_turn = required_string(control_command, "turnId", 36U);
                validate_id(control_turn);
                if (control_turn != turn_id) throw protocol_error("turn identifier mismatch");
                {
                    std::lock_guard lock(control.mutex);
                    control.stop.store(true, std::memory_order_release);
                }
                control.notify();
            } else if (control_type == "cancelSession") {
                {
                    std::lock_guard lock(control.mutex);
                    control.cancel.store(true, std::memory_order_release);
                }
                control.notify();
                cancelled = true;
            } else {
                throw protocol_error("active turn command rejected");
            }
        }

        turn_result result;
        try {
            result = generation.get();
        } catch (const worker_failure& failure) {
            if (failure.fatal()) throw;
            write_event({{"sessionId", session_id}, {"turnId", turn_id},
                         {"supportCode", failure.support_code()}, {"eventType", "turnFailed"}});
            continue;
        } catch (const protocol_error&) {
            write_event({{"sessionId", session_id}, {"turnId", turn_id},
                         {"supportCode", "runtime_protocol_failed"}, {"eventType", "turnFailed"}});
            continue;
        } catch (...) {
            throw worker_failure(
                "runtime_load_failed", true, "native generation failed");
        }
        if (cancelled || result.cancelled) {
            publish_terminal_event(
                {{"sessionId", session_id}, {"eventType", "sessionCancelled"}},
                {[&] { session.verify_terminal_integrity(); }});
            std::_Exit(EXIT_SUCCESS);
        }
#if defined(GRANITE_TURBOQUANT_WORKER)
        write_event(activation.to_json(session_id, turn_id));
#endif
        publish_terminal_event(
            {{"sessionId", session_id},
             {"turnId", turn_id},
             {"promptTokenCount", result.prompt_tokens},
             {"generatedTokenCount", result.generated_tokens},
             {"disposition", result.stopped ? "stopped" : "completed"},
             {"eventType", "turnCompleted"}},
            {[&] { session.verify_terminal_integrity(); }});
    }
}

void run_session(const json& command, const runtime_context& runtime) {
    const std::string session_id = required_string(command, "sessionId", 36U);
    validate_id(session_id);
    try {
        run_session_body(command, runtime);
    } catch (const worker_failure& failure) {
        emit_session_failure(session_id, failure.support_code());
        throw;
    } catch (const protocol_error&) {
        emit_session_failure(session_id, "runtime_protocol_failed");
        throw;
    } catch (...) {
        emit_session_failure(session_id, "runtime_load_failed");
        throw;
    }
}

}  // namespace
}  // namespace granite::official_worker

int main(int argc, char** argv) {
    using namespace granite::official_worker;
    try {
        if (_setmode(_fileno(stdin), _O_BINARY) == -1 ||
            _setmode(_fileno(stdout), _O_BINARY) == -1) {
            throw protocol_error("binary protocol framing unavailable");
        }
        std::vector<std::string> arguments;
        arguments.reserve(static_cast<std::size_t>(argc));
        for (int index = 0; index < argc; ++index) arguments.emplace_back(argv[index]);
        (void)parse_arguments(arguments);
        const runtime_context runtime = initialize_verified_runtime();
        publish_terminal_event(
            {{"protocolId", official_protocol},
             {"buildEvidence", runtime.evidence().to_json()},
             {"eventType", "hello"}},
            {
                [&] { runtime.verify_terminal_topology(); },
                [&] { verify_loaded_module_closure(runtime); },
            });
        const nlohmann::json command = parse_json_line(read_bounded_line());
        const std::string type = command.at("commandType").get<std::string>();
        if (type == "startInspection") {
            run_inspection(command, runtime);
        } else if (type == "startSession") {
            run_session(command, runtime);
        } else {
            throw protocol_error("initial command rejected");
        }
        return 0;
    } catch (...) {
        std::cerr << "worker_failed\n";
        return 2;
    }
}
