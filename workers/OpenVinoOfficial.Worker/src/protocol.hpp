#pragma once

#include <nlohmann/json.hpp>

#include <cstddef>
#include <stdexcept>
#include <string>
#include <string_view>
#include <utility>
#include <vector>

namespace granite::official_worker {

inline constexpr std::size_t maximum_line_bytes = 1024U * 1024U;
inline constexpr std::size_t maximum_json_depth = 32U;
inline constexpr std::size_t maximum_operation_text_bytes = 4U * 1024U * 1024U;
inline constexpr std::string_view official_protocol = "openvino.official/1";

class protocol_error final : public std::runtime_error {
public:
    using std::runtime_error::runtime_error;
};

class worker_failure final : public std::runtime_error {
public:
    worker_failure(std::string support_code, bool fatal, std::string message)
        : std::runtime_error(std::move(message)),
          support_code_(std::move(support_code)),
          fatal_(fatal) {}

    [[nodiscard]] const std::string& support_code() const noexcept {
        return support_code_;
    }
    [[nodiscard]] bool fatal() const noexcept { return fatal_; }

private:
    std::string support_code_;
    bool fatal_;
};

enum class input_pipe_state { empty, available, closed };

std::string parse_arguments(const std::vector<std::string>& arguments);
nlohmann::json parse_json_line(const std::string& line);
bool validate_absolute_package_path(const std::wstring& path) noexcept;
std::wstring utf8_to_wide(const std::string& value);
std::string wide_to_utf8(const std::wstring& value);
bool is_canonical_uuid(const std::string& value) noexcept;
bool is_lower_sha256(const std::string& value) noexcept;
input_pipe_state probe_input_pipe(void* pipe);
void write_event(const nlohmann::ordered_json& event);

}  // namespace granite::official_worker
