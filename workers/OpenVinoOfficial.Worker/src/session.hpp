#pragma once

#include <atomic>
#include <cstddef>
#include <condition_variable>
#include <filesystem>
#include <memory>
#include <mutex>
#include <string>

#include <nlohmann/json.hpp>
#include <openvino/genai/llm_pipeline.hpp>

#include "package_inspector.hpp"

namespace granite::official_worker {

bool fits_context(
    std::size_t prompt_tokens,
    std::size_t requested_tokens,
    std::size_t model_limit,
    std::size_t c1_limit) noexcept;

class session_state final {
public:
    void accept_prompt();
    void begin_generation();
    void request_stop();
    void complete_stopped_turn();
    void complete_turn();
    void cancel();
    void close();
    [[nodiscard]] bool is_terminal() const noexcept;

private:
    enum class value { ready, prompt, generating, stopping, terminal } state_ = value::ready;
};

struct turn_control final {
    std::atomic_bool stop{false};
    std::atomic_bool cancel{false};
    std::atomic_bool first_fragment_buffered{false};
    std::atomic_bool first_fragment_release{false};
    std::mutex mutex;
    std::condition_variable changed;

    void notify() noexcept { changed.notify_all(); }
};

struct turn_result final {
    std::size_t prompt_tokens{};
    std::size_t generated_tokens{};
    std::size_t streamed_fragments{};
    bool stopped{};
    bool cancelled{};
    bool output_exceeded{};
    std::string answer;
};

class official_session final {
public:
    official_session(
        package_lease package,
        const runtime_context& runtime,
        std::string device,
        std::size_t model_context,
        std::size_t c1_context,
        native_load_observer observer = {},
        native_module_verifier module_verifier = {});
    ~official_session();
    official_session(const official_session&) = delete;
    official_session& operator=(const official_session&) = delete;

    turn_result generate(
        const std::string& session_id,
        const std::string& turn_id,
        const std::string& prompt,
        std::size_t requested_tokens,
        turn_control& control);
    void verify_integrity(bool drain_notifications = false) const;
    void verify_terminal_integrity() const;

private:
    package_lease package_;
    const runtime_context& runtime_;
    native_load_observer observer_;
    native_module_verifier module_verifier_;
    std::string device_;
    ov::genai::ChatHistory history_;
    std::size_t model_context_;
    std::size_t c1_context_;
};

}  // namespace granite::official_worker
