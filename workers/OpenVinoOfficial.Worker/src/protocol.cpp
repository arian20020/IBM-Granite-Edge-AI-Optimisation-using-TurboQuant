#include "protocol.hpp"

#include <windows.h>

#include <algorithm>
#include <array>
#include <iostream>
#include <mutex>
#include <set>

namespace granite::official_worker {
namespace {

using json = nlohmann::json;

void require_exact_keys(const json& value, std::initializer_list<std::string_view> expected) {
    if (!value.is_object() || value.size() != expected.size()) {
        throw protocol_error("closed object shape mismatch");
    }
    for (std::string_view key : expected) {
        if (!value.contains(std::string(key))) {
            throw protocol_error("closed object field missing");
        }
    }
}

void validate_command_shape(const json& value) {
    if (!value.is_object() || !value.contains("commandType") || !value["commandType"].is_string()) {
        throw protocol_error("command discriminator missing");
    }
    const std::string type = value["commandType"].get<std::string>();
    if (type == "startInspection") {
        require_exact_keys(value, {"inspectionRunId", "packagePath", "packageManifestDigest", "modelSha256", "modelLengthBytes", "commandType"});
    } else if (type == "startSession") {
        if (value.contains("initialHistory")) {
            require_exact_keys(value, {"sessionId", "inspectionRunId", "packagePath", "packageManifestDigest", "modelSha256", "modelLengthBytes", "device", "limits", "runtime", "commandType", "initialHistory"});
            const auto& turns = value.at("initialHistory");
            if (!turns.is_array() || turns.size() > 512U) throw protocol_error("initial history count exceeded");
            std::size_t bytes = 0;
            for (const auto& turn : turns) {
                require_exact_keys(turn, {"role", "content"});
                if (!turn.at("role").is_string() || !turn.at("content").is_string()) throw protocol_error("initial history types invalid");
                const auto role = turn.at("role").get<std::string>();
                const auto content = turn.at("content").get<std::string>();
                if ((role != "user" && role != "assistant") || content.empty() || content.size() > 64U * 1024U || content.find('\0') != std::string::npos) throw protocol_error("initial history turn invalid");
                bytes += content.size();
                if (bytes > 512U * 1024U) throw protocol_error("initial history bytes exceeded");
            }
        } else {
            require_exact_keys(value, {"sessionId", "inspectionRunId", "packagePath", "packageManifestDigest", "modelSha256", "modelLengthBytes", "device", "limits", "runtime", "commandType"});
        }
        require_exact_keys(value.at("device"), {"deviceId"});
        require_exact_keys(value.at("limits"), {"maximumContextTokens", "maximumNewTokens"});
        require_exact_keys(value.at("runtime"), {"kvCachePrecision"});
    } else if (type == "prompt") {
        if (value.contains("isTransientTitle")) {
            require_exact_keys(value, {"sessionId", "turnId", "prompt", "requestedNewTokens", "commandType", "isTransientTitle"});
            if (!value.at("isTransientTitle").is_boolean()) throw protocol_error("title flag invalid");
        } else {
            require_exact_keys(value, {"sessionId", "turnId", "prompt", "requestedNewTokens", "commandType"});
        }
    } else if (type == "stopTurn") {
        require_exact_keys(value, {"sessionId", "turnId", "commandType"});
    } else if (type == "cancelSession" || type == "closeSession") {
        require_exact_keys(value, {"sessionId", "commandType"});
    } else {
        throw protocol_error("command discriminator unsupported");
    }
}

}  // namespace

std::string parse_arguments(const std::vector<std::string>& arguments) {
    if (arguments.size() != 3U || arguments[1] != "--protocol" ||
        arguments[2] != official_protocol) {
        throw protocol_error("invalid fixed argument grammar");
    }
    return arguments[2];
}

nlohmann::json parse_json_line(const std::string& line) {
    if (line.empty() || line.size() > maximum_line_bytes ||
        line.find('\0') != std::string::npos) {
        throw protocol_error("invalid line bound");
    }

    std::vector<std::set<std::string, std::less<>>> object_keys(maximum_json_depth + 1U);
    json::parser_callback_t callback = [&](int depth, json::parse_event_t event, json& parsed) {
        if (depth < 0 || static_cast<std::size_t>(depth) > maximum_json_depth) {
            throw protocol_error("JSON depth exceeded");
        }
        const auto index = static_cast<std::size_t>(depth);
        if (event == json::parse_event_t::object_start) {
            // Keys are reported one level deeper than their object's start.
            if (index + 1U > maximum_json_depth) throw protocol_error("JSON depth exceeded");
            object_keys[index + 1U].clear();
        } else if (event == json::parse_event_t::key) {
            const std::string key = parsed.get<std::string>();
            if (!object_keys[index].insert(key).second) {
                throw protocol_error("duplicate JSON field");
            }
        }
        return true;
    };

    json result;
    try {
        result = json::parse(line, callback, true, false);
    } catch (const protocol_error&) {
        throw;
    } catch (const json::exception&) {
        throw protocol_error("invalid JSON");
    }
    if (result.is_discarded()) {
        throw protocol_error("invalid JSON");
    }
    validate_command_shape(result);
    return result;
}

bool validate_absolute_package_path(const std::wstring& path) noexcept {
    if (path.empty() || path.size() > 32768U ||
        std::any_of(path.begin(), path.end(), [](wchar_t value) {
            return value == L'\0' || value < L' ';
        })) {
        return false;
    }
    return (path.size() >= 3U &&
            ((path[0] >= L'A' && path[0] <= L'Z') ||
             (path[0] >= L'a' && path[0] <= L'z')) &&
            path[1] == L':' && (path[2] == L'\\' || path[2] == L'/')) ||
           (path.size() >= 3U && path[0] == L'\\' && path[1] == L'\\');
}

std::wstring utf8_to_wide(const std::string& value) {
    if (value.empty()) {
        return {};
    }
    const int length = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, value.data(),
                                           static_cast<int>(value.size()), nullptr, 0);
    if (length <= 0) {
        throw protocol_error("invalid UTF-8");
    }
    std::wstring result(static_cast<std::size_t>(length), L'\0');
    if (MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, value.data(),
                            static_cast<int>(value.size()), result.data(), length) != length) {
        throw protocol_error("invalid UTF-8");
    }
    return result;
}

std::string wide_to_utf8(const std::wstring& value) {
    if (value.empty()) {
        return {};
    }
    const int length = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, value.data(),
                                           static_cast<int>(value.size()), nullptr, 0, nullptr, nullptr);
    if (length <= 0) {
        throw protocol_error("invalid path encoding");
    }
    std::string result(static_cast<std::size_t>(length), '\0');
    if (WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, value.data(),
                            static_cast<int>(value.size()), result.data(), length, nullptr, nullptr) != length) {
        throw protocol_error("invalid path encoding");
    }
    return result;
}

bool is_canonical_uuid(const std::string& value) noexcept {
    if (value.size() != 36U || value == "00000000-0000-0000-0000-000000000000") {
        return false;
    }
    for (std::size_t index = 0; index < value.size(); ++index) {
        if (index == 8U || index == 13U || index == 18U || index == 23U) {
            if (value[index] != '-') return false;
        } else if (!((value[index] >= '0' && value[index] <= '9') ||
                     (value[index] >= 'a' && value[index] <= 'f'))) {
            return false;
        }
    }
    return true;
}

bool is_lower_sha256(const std::string& value) noexcept {
    return value.size() == 64U && std::all_of(value.begin(), value.end(), [](char item) {
        return (item >= '0' && item <= '9') || (item >= 'a' && item <= 'f');
    });
}

input_pipe_state probe_input_pipe(void* pipe) {
    DWORD available = 0;
    if (PeekNamedPipe(static_cast<HANDLE>(pipe), nullptr, 0, nullptr, &available, nullptr) != FALSE) {
        return available == 0 ? input_pipe_state::empty : input_pipe_state::available;
    }
    if (GetLastError() == ERROR_BROKEN_PIPE) return input_pipe_state::closed;
    throw protocol_error("stdin pipe probe failed");
}

void write_event(const nlohmann::ordered_json& event) {
    static std::mutex output_mutex;
    const std::string line = event.dump(-1, ' ', false, nlohmann::ordered_json::error_handler_t::strict);
    if (line.size() > maximum_line_bytes) {
        throw protocol_error("output line exceeded");
    }
    std::lock_guard lock(output_mutex);
    std::cout << line << '\n' << std::flush;
}

}  // namespace granite::official_worker
