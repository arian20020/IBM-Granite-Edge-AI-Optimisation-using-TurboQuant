#include "session.hpp"

#include "protocol.hpp"

#include <algorithm>
#include <chrono>
#include <exception>
#include <functional>
#include <stdexcept>
#include <utility>

namespace granite::official_worker {
namespace {

ov::AnyMap pipeline_properties(const std::string& kv_cache_precision) {
    ov::AnyMap properties{{"ATTENTION_BACKEND", std::string("SDPA")}};
#if defined(GRANITE_TURBOQUANT_WORKER)
    if (kv_cache_precision == "tbq4") {
        properties.emplace("KEY_CACHE_PRECISION", std::string("u4"));
        properties.emplace("VALUE_CACHE_PRECISION", std::string("u4"));
        properties.emplace("KEY_CACHE_QUANT_ALG", std::string("TURBO"));
        properties.emplace("VALUE_CACHE_QUANT_ALG", std::string("TURBO"));
    } else {
        throw std::invalid_argument("TurboQuant worker requires TBQ4 cache mode");
    }
#else
    if (kv_cache_precision == "u8") {
        properties.emplace(ov::hint::kv_cache_precision.name(), ov::element::u8);
    } else if (kv_cache_precision != "released-default") {
        throw std::invalid_argument("unsupported KV-cache precision");
    }
#endif
    return properties;
}

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
    const runtime_context& runtime,
    std::string device,
    std::size_t model_context,
    std::size_t c1_context,
    native_load_observer observer,
    native_module_verifier module_verifier,
    std::string kv_cache_precision)
    : package_(std::move(package)),
      runtime_(runtime),
      observer_(std::move(observer)),
      module_verifier_(std::move(module_verifier)),
      device_(std::move(device)),
      kv_cache_precision_(std::move(kv_cache_precision)),
      model_context_(model_context),
      c1_context_(c1_context) {
    pipeline_ = create_pipeline();
}

official_session::~official_session() = default;

std::unique_ptr<ov::genai::LLMPipeline> official_session::create_pipeline() {
    try {
        if (observer_) observer_(native_load_stage::pipeline_construction);
        verify_integrity(false);
        auto pipeline = std::make_unique<ov::genai::LLMPipeline>(
            package_.root(), device_, pipeline_properties(kv_cache_precision_));
        pipeline->get_tokenizer().set_chat_template(
            std::string(route_chat_template));
        verify_integrity(true);
        return pipeline;
    } catch (...) {
        verify_integrity(true);
        throw;
    }
}

void official_session::verify_integrity(bool drain_notifications) const {
    std::exception_ptr first_failure;
    const auto capture = [&](const auto& check) {
        try {
            check();
        } catch (...) {
            if (first_failure == nullptr) first_failure = std::current_exception();
        }
    };
    capture([&] { package_.verify_topology(drain_notifications); });
    capture([&] { runtime_.verify_topology(drain_notifications); });
    if (module_verifier_) capture(module_verifier_);
    if (first_failure != nullptr) std::rethrow_exception(first_failure);
}

void official_session::verify_terminal_integrity() const {
    std::exception_ptr first_failure;
    const auto capture = [&](const auto& check) {
        try {
            check();
        } catch (...) {
            if (first_failure == nullptr) first_failure = std::current_exception();
        }
    };
    capture([&] { package_.verify_terminal_topology(); });
    capture([&] { runtime_.verify_terminal_topology(); });
    if (module_verifier_) capture(module_verifier_);
    if (first_failure != nullptr) std::rethrow_exception(first_failure);
}

turn_result official_session::generate(
    const std::string& session_id,
    const std::string& turn_id,
    const std::string& prompt,
    std::size_t requested_tokens,
    turn_control& control) {
    history_.push_back({{"role", std::string("user")}, {"content", prompt}});
    try {
        verify_integrity(false);
        ov::genai::Tokenizer tokenizer = pipeline_->get_tokenizer();
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

        ov::genai::GenerationConfig config = pipeline_->get_generation_config();
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
                std::unique_lock publication_lock(control.mutex, std::defer_lock);
                if (result.streamed_fragments == 0U) {
                    publication_lock.lock();
                    control.first_fragment_buffered.store(true, std::memory_order_release);
                    control.notify();
                    control.changed.wait(
                        publication_lock,
                        [&] {
                            return control.cancel.load(std::memory_order_acquire) ||
                                control.first_fragment_release.load(
                                    std::memory_order_acquire);
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
                if (publication_lock.owns_lock()) publication_lock.unlock();
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
        std::unique_ptr<ov::genai::LLMPipeline> active_pipeline =
            std::move(pipeline_);
        ov::genai::DecodedResults generated;
        try {
            generated = active_pipeline->generate(
                generation_history, config, streamer);
        } catch (...) {
            if (!control.cancel.load(std::memory_order_acquire)) {
                pipeline_ = create_pipeline();
            }
            throw;
        }
        verify_integrity(true);
        if (!control.cancel.load(std::memory_order_acquire) &&
            !result.cancelled) {
            pipeline_ = create_pipeline();
        }
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
        verify_integrity(true);
        throw;
    }
}

}  // namespace granite::official_worker
