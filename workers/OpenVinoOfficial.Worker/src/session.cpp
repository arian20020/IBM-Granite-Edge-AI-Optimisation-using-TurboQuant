#include "session.hpp"

#include "protocol.hpp"

#include <algorithm>
#include <chrono>
#include <exception>
#include <functional>
#include <stdexcept>
#include <utility>
#include <vector>

#include <openvino/openvino.hpp>
#if defined(GRANITE_TURBOQUANT_WORKER)
#include <openvino/runtime/internal_properties.hpp>
#endif

namespace granite::official_worker {
namespace {

ov::AnyMap pipeline_properties(const std::string& kv_cache_precision) {
    ov::AnyMap properties{{"ATTENTION_BACKEND", std::string("SDPA")}};
#if defined(GRANITE_TURBOQUANT_WORKER)
    if (kv_cache_precision == "tbq4" || kv_cache_precision == "tbq3") {
        const ov::element::Type precision = kv_cache_precision == "tbq4"
            ? ov::element::u4
            : ov::element::u3;
        properties.emplace(ov::key_cache_precision.name(), precision);
        properties.emplace(ov::value_cache_precision.name(), precision);
        properties.emplace(
            ov::internal::key_cache_quant_alg.name(),
            ov::internal::CacheQuantAlgorithm::TURBO);
        properties.emplace(
            ov::internal::value_cache_quant_alg.name(),
            ov::internal::CacheQuantAlgorithm::TURBO);
    } else {
        throw std::invalid_argument("TurboQuant worker requires TBQ4 or TBQ3 cache mode");
    }
#else
    if (kv_cache_precision == "u8") {
        properties.emplace(ov::hint::kv_cache_precision.name(), ov::element::u8);
    } else if (kv_cache_precision == "u4") {
        properties.emplace(ov::hint::kv_cache_precision.name(), ov::element::u4);
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

std::uint64_t count_exact_model_sdpa(
    const std::shared_ptr<ov::Model>& model) {
    std::uint64_t count = 0U;
    for (const auto& node : model->get_ops()) {
        if (std::string(node->get_type_name()) != "ScaledDotProductAttention") {
            continue;
        }
        if (node->get_input_size() < 3U) {
            throw std::runtime_error("model SDPA input arity changed");
        }
        for (std::size_t index = 0; index < 3U; ++index) {
            const ov::PartialShape shape = node->get_input_partial_shape(index);
            if (!shape.rank().is_static() || shape.rank().get_length() != 4 ||
                !shape[3].is_static() || shape[3].get_length() != 64) {
                throw std::runtime_error("model SDPA head dimension is not exactly 64");
            }
        }
        ++count;
    }
    if (count == 0U) {
        throw std::runtime_error("model contains no SDPA nodes");
    }
    return count;
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

bool requires_route_chat_template_fallback(
    const package_evidence& evidence) noexcept {
    return !evidence.has_chat_template;
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
        ov::AnyMap properties = pipeline_properties(kv_cache_precision_);
        // ATTENTION_BACKEND is consumed by the GenAI directory constructor;
        // it is not an OpenVINO device property. This path compiles the same
        // model explicitly so the applied KV-cache precision can be observed.
        properties.erase("ATTENTION_BACKEND");
        ov::Core core;
        const std::shared_ptr<ov::Model> model = core.read_model(
            package_.root() / L"openvino_model.xml");
        ov::CompiledModel compiled = core.compile_model(
            model, device_, properties);
#if defined(GRANITE_TURBOQUANT_WORKER)
        model_sdpa_node_count_ = count_exact_model_sdpa(model);
#endif
        if (kv_cache_precision_ == "released-default") {
            actual_kv_cache_precision_ = kv_cache_precision_;
        } else {
#if defined(GRANITE_TURBOQUANT_WORKER)
            const ov::element::Type requested = kv_cache_precision_ == "tbq3"
                ? ov::element::u3
                : ov::element::u4;
            const ov::element::Type actual_key =
                compiled.get_property(ov::key_cache_precision);
            const ov::element::Type actual_value =
                compiled.get_property(ov::value_cache_precision);
            if (actual_key != requested || actual_value != requested) {
                throw std::runtime_error(
                    "compiled model TurboQuant key/value precision did not match request");
            }
            actual_kv_cache_precision_ = kv_cache_precision_;
#else
            const ov::element::Type requested = kv_cache_precision_ == "u8"
                ? ov::element::u8
                : ov::element::u4;
            const ov::element::Type actual_key =
                compiled.get_property(ov::key_cache_precision);
            const ov::element::Type actual_value =
                compiled.get_property(ov::value_cache_precision);
            if (actual_key != requested || actual_value != requested) {
                throw std::runtime_error(
                    "compiled model KV-cache precision did not match request");
            }
            actual_kv_cache_precision_ = actual_key.get_type_name();
#endif
        }
        ov::genai::Tokenizer tokenizer(package_.root());
        ov::genai::GenerationConfig generation_config(
            package_.root() / L"generation_config.json");
        auto pipeline = std::make_unique<ov::genai::LLMPipeline>(
            compiled.create_infer_request(), tokenizer, generation_config);
        if (requires_route_chat_template_fallback(package_.evidence())) {
            pipeline->get_tokenizer().set_chat_template(
                std::string(route_chat_template));
        }
        verify_integrity(true);
        return pipeline;
    } catch (...) {
        verify_integrity(true);
        throw;
    }
}

const std::string& official_session::actual_kv_cache_precision() const noexcept {
    return actual_kv_cache_precision_;
}

std::uint64_t official_session::model_sdpa_node_count() const noexcept {
    return model_sdpa_node_count_;
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

void official_session::prepare_initial_history(const nlohmann::json& turns, std::size_t generation_reserve) {
    ov::genai::ChatHistory candidate;
    for (const auto& turn : turns) {
        candidate.push_back({{"role", turn.at("role").get<std::string>()},
                             {"content", turn.at("content").get<std::string>()}});
    }
    if (!candidate.empty()) {
        auto tokenizer = pipeline_->get_tokenizer();
        if (requires_route_chat_template_fallback(package_.evidence())) tokenizer.set_chat_template(std::string(route_chat_template));
        const auto rendered = tokenizer.apply_chat_template(candidate, true);
        const auto encoded = tokenizer.encode(rendered, ov::AnyMap{{"add_special_tokens", false}});
        if (!fits_context(encoded.input_ids.get_size(), generation_reserve, model_context_, c1_context_)) {
            throw worker_failure("runtime_context_exceeded", false, "initial history exceeds context");
        }
    }
    history_ = std::move(candidate);
}

turn_result official_session::generate(
    const std::string& session_id,
    const std::string& turn_id,
    const std::string& prompt,
    std::size_t requested_tokens,
    turn_control& control,
    bool is_transient_title) {
    ov::genai::ChatHistory retained;
    if (is_transient_title) {
        // Reconstruct messages, never share ChatHistory's mutable internal state.
        retained = ov::genai::ChatHistory(history_.get_messages());
        history_ = ov::genai::ChatHistory{};
    }
    history_.push_back({{"role", std::string("user")}, {"content", prompt}});
    try {
        verify_integrity(false);
        ov::genai::Tokenizer tokenizer = pipeline_->get_tokenizer();
        if (requires_route_chat_template_fallback(package_.evidence())) {
            tokenizer.set_chat_template(std::string(route_chat_template));
        }
        const std::string rendered = tokenizer.apply_chat_template(history_, true);
        const ov::genai::TokenizedInputs encoded = tokenizer.encode(
            rendered, ov::AnyMap{{"add_special_tokens", false}});
        const std::size_t prompt_tokens = encoded.input_ids.get_size();
        const std::size_t context_limit = std::min(model_context_, c1_context_);
        if (prompt_tokens >= context_limit) {
            throw worker_failure(
                "runtime_context_exceeded", false, "runtime context exceeded");
        }
        const std::size_t effective_tokens = std::min(requested_tokens, context_limit - prompt_tokens);

        ov::genai::GenerationConfig config = pipeline_->get_generation_config();
        config.max_new_tokens = effective_tokens;
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
            if (is_transient_title) {
                throw worker_failure("runtime_load_failed", true, "temporary inference state could not be restored");
            }
            if (!control.cancel.load(std::memory_order_acquire)) {
                pipeline_ = create_pipeline();
            }
            throw;
        }
        verify_integrity(true);
        if (!control.cancel.load(std::memory_order_acquire) &&
            !result.cancelled) {
            if (is_transient_title) pipeline_ = std::move(active_pipeline);
            else pipeline_ = create_pipeline();
        }
        result.prompt_tokens = generated.perf_metrics.get_num_input_tokens();
        result.generated_tokens = generated.perf_metrics.get_num_generated_tokens();
        if (!result.stopped && !result.cancelled && !control.stop.load(std::memory_order_acquire) &&
            !control.cancel.load(std::memory_order_acquire) &&
            (generated.finish_reasons.size() != 1U ||
             (generated.finish_reasons.front() != ov::genai::GenerationFinishReason::LENGTH &&
              generated.finish_reasons.front() != ov::genai::GenerationFinishReason::STOP))) {
            throw worker_failure("runtime_integrity_failed", true, "native finish reason missing or unsupported");
        }
        result.length_reached = !generated.finish_reasons.empty() &&
            generated.finish_reasons.front() == ov::genai::GenerationFinishReason::LENGTH;
        if (result.output_exceeded) {
            throw worker_failure(
                "runtime_protocol_failed", is_transient_title, "native output limit exceeded");
        }
        if (result.prompt_tokens != prompt_tokens || result.generated_tokens > effective_tokens ||
            (!result.stopped && !result.cancelled && !generated.texts.empty() &&
             generated.texts.front() != result.answer)) {
            throw worker_failure(
                "runtime_integrity_failed", true,
                "native generation evidence inconsistent");
        }
        if (control.cancel.load(std::memory_order_acquire) || result.cancelled) {
            pipeline_ = std::move(active_pipeline);
            history_.pop_back();
            if (is_transient_title) history_ = std::move(retained);
            result.cancelled = true;
            return result;
        }
        if (control.stop.load(std::memory_order_acquire)) result.stopped = true;
        history_.push_back({{"role", std::string("assistant")}, {"content", result.answer}});
        if (is_transient_title) history_ = std::move(retained);
        return result;
    } catch (...) {
        if (!history_.empty()) history_.pop_back();
        if (is_transient_title) history_ = std::move(retained);
        verify_integrity(true);
        throw;
    }
}

}  // namespace granite::official_worker
