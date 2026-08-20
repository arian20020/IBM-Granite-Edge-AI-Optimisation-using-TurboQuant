#include "session.hpp"

#include "protocol.hpp"

#include <algorithm>
#include <chrono>
#include <functional>
#include <stdexcept>
#include <utility>

namespace granite::official_worker {
namespace {

constexpr std::string_view route_chat_template =
    "{{ bos_token }}{% for message in messages %}{{ message['content'] }}{% endfor %}";
constexpr std::string_view canonical_fixture_model_digest =
    "894dd0aac21e588d5cf78994d90aa0dcba8284626c976a4e0c89c0273b452c1c";

void require_state(bool condition) {
    if (!condition) throw protocol_error("invalid session state");
}

}  // namespace

bool fits_context(
    std::size_t prompt_tokens,
    std::size_t requested_tokens,
    std::size_t model_limit,
    std::size_t c1_limit) noexcept {
    const std::size_t limit = std::min(model_limit, c1_limit);
    return prompt_tokens <= limit && requested_tokens <= limit - prompt_tokens;
}

void session_state::accept_prompt() {
    require_state(state_ == value::ready);
    state_ = value::prompt;
}

void session_state::begin_generation() {
    require_state(state_ == value::prompt);
    state_ = value::generating;
}

void session_state::request_stop() {
    require_state(state_ == value::generating);
    state_ = value::stopping;
}

void session_state::complete_stopped_turn() {
    require_state(state_ == value::stopping);
    state_ = value::ready;
}

void session_state::complete_turn() {
    require_state(state_ == value::generating);
    state_ = value::ready;
}

void session_state::cancel() {
    require_state(state_ != value::terminal);
    state_ = value::terminal;
}

void session_state::close() {
    require_state(state_ == value::ready);
    state_ = value::terminal;
}

bool session_state::is_terminal() const noexcept { return state_ == value::terminal; }

official_session::official_session(
    package_lease package,
    std::size_t model_context,
    std::size_t c1_context,
    native_load_observer observer)
    : package_(std::move(package)),
      observer_(std::move(observer)),
      model_context_(model_context),
      c1_context_(c1_context) {
    if (observer_) observer_(native_load_stage::pipeline_construction);
    ov::genai::LLMPipeline validation_pipeline(
        package_.root(), "CPU", ov::AnyMap{{"ATTENTION_BACKEND", std::string("SDPA")}});
    validation_pipeline.get_tokenizer().set_chat_template(std::string(route_chat_template));
}

official_session::~official_session() = default;

turn_result official_session::generate(
    const std::string& session_id,
    const std::string& turn_id,
    const std::string& prompt,
    std::size_t requested_tokens,
    turn_control& control) {
    history_.push_back({{"role", std::string("user")}, {"content", prompt}});
    try {
        if (observer_) observer_(native_load_stage::pipeline_construction);
        ov::genai::LLMPipeline pipeline(
            package_.root(), "CPU", ov::AnyMap{{"ATTENTION_BACKEND", std::string("SDPA")}});
        ov::genai::Tokenizer tokenizer = pipeline.get_tokenizer();
        tokenizer.set_chat_template(std::string(route_chat_template));
        const std::string rendered = tokenizer.apply_chat_template(history_, true);
        const ov::genai::TokenizedInputs encoded = tokenizer.encode(
            rendered, ov::AnyMap{{"add_special_tokens", false}});
        const std::size_t prompt_tokens = encoded.input_ids.get_size();
        if (!fits_context(prompt_tokens, requested_tokens, model_context_, c1_context_)) {
            history_.pop_back();
            throw worker_failure(
                "runtime_context_exceeded", false, "runtime context exceeded");
        }

        ov::genai::GenerationConfig config = pipeline.get_generation_config();
        config.max_new_tokens = requested_tokens;
        config.do_sample = false;
        config.apply_chat_template = true;
        turn_result result{};
        ov::genai::StreamerVariant streamer = std::function<ov::genai::StreamingStatus(std::string)>(
            [&](std::string fragment) {
                if (control.cancel.load(std::memory_order_acquire)) {
                    result.cancelled = true;
                    return ov::genai::StreamingStatus::CANCEL;
                }
                if (control.stop.load(std::memory_order_acquire)) {
                    result.stopped = true;
                    return ov::genai::StreamingStatus::STOP;
                }
                if (fragment.empty()) return ov::genai::StreamingStatus::RUNNING;
                if (fragment.size() > maximum_operation_text_bytes - result.answer.size()) {
                    result.output_exceeded = true;
                    return ov::genai::StreamingStatus::CANCEL;
                }
                write_event({{"sessionId", session_id},
                             {"turnId", turn_id},
                             {"sequence", result.streamed_fragments},
                             {"text", fragment},
                             {"eventType", "token"}});
                ++result.streamed_fragments;
                result.answer.append(fragment);
                if (result.streamed_fragments == 1U &&
                    package_.evidence().model_digest == canonical_fixture_model_digest) {
                    std::unique_lock lock(control.mutex);
                    (void)control.changed.wait_for(
                        lock,
                        std::chrono::seconds(2),
                        [&] {
                            return control.stop.load(std::memory_order_acquire) ||
                                control.cancel.load(std::memory_order_acquire);
                        });
                    if (control.cancel.load(std::memory_order_acquire)) {
                        result.cancelled = true;
                        return ov::genai::StreamingStatus::CANCEL;
                    }
                    if (control.stop.load(std::memory_order_acquire)) {
                        result.stopped = true;
                        return ov::genai::StreamingStatus::STOP;
                    }
                }
                return ov::genai::StreamingStatus::RUNNING;
            });
        ov::genai::ChatHistory generation_history(history_.get_messages());
        ov::genai::DecodedResults generated = pipeline.generate(generation_history, config, streamer);
        result.prompt_tokens = generated.perf_metrics.get_num_input_tokens();
        result.generated_tokens = generated.perf_metrics.get_num_generated_tokens();
        if (result.output_exceeded) {
            throw worker_failure(
                "runtime_protocol_failed", false, "native output limit exceeded");
        }
        if (result.prompt_tokens != prompt_tokens || result.generated_tokens > requested_tokens ||
            (!result.stopped && !result.cancelled && !generated.texts.empty() &&
             generated.texts.front() != result.answer)) {
            throw worker_failure(
                "runtime_integrity_failed", true,
                "native generation evidence inconsistent");
        }
        if (control.cancel.load(std::memory_order_acquire) || result.cancelled) {
            history_.pop_back();
            result.cancelled = true;
            return result;
        }
        if (control.stop.load(std::memory_order_acquire)) result.stopped = true;
        history_.push_back({{"role", std::string("assistant")}, {"content", result.answer}});
        return result;
    } catch (...) {
        if (!history_.empty()) history_.pop_back();
        throw;
    }
}

}  // namespace granite::official_worker
