#include <openvino/genai/llm_pipeline.hpp>

#include "session.hpp"

#include <filesystem>
#include <future>
#include <iostream>
#include <memory>
#include <string>
#include <vector>

namespace {

constexpr std::string_view route_chat_template =
    "{{ bos_token }}{% for message in messages %}{{ message['content'] }}{% endfor %}";

class token_collector final : public ov::genai::StreamerBase {
public:
    ov::genai::StreamingStatus write(std::int64_t token) override {
        tokens.push_back(token);
        return ov::genai::StreamingStatus::RUNNING;
    }

    void end() override {
    }

    std::vector<std::int64_t> tokens;
};

}  // namespace

int main(int argc, char** argv) {
    if (argc != 3) return 2;
    try {
        const std::filesystem::path package = std::filesystem::absolute(argv[1]);
        const std::string mode = argv[2];
        if (mode == "official" || mode == "official-async" || mode == "official-two") {
            granite::official_worker::official_session session(package, 64U, 64U);
            granite::official_worker::turn_control control;
            const auto generate = [&] {
                return session.generate(
                    "e39d252d-2144-4624-a055-0350c93f6728",
                    "f77fb13c-263d-49a1-8d93-d908968c5832",
                    "hello",
                    2U,
                    control);
            };
            const granite::official_worker::turn_result result = mode == "official-async"
                ? std::async(std::launch::async, generate).get()
                : generate();
            std::cout << "official:" << result.answer << ":input:" << result.prompt_tokens
                      << ":generated:" << result.generated_tokens
                      << ":fragments:" << result.streamed_fragments << '\n';
            if (mode == "official-two") {
                const granite::official_worker::turn_result second = session.generate(
                    "e39d252d-2144-4624-a055-0350c93f6728",
                    "4b29875f-7880-4ee9-a2e0-131dbb3779d2",
                    "hello",
                    2U,
                    control);
                std::cout << "official-second:" << second.answer
                          << ":input:" << second.prompt_tokens
                          << ":generated:" << second.generated_tokens
                          << ":fragments:" << second.streamed_fragments << '\n';
            }
            return 0;
        }
        if (mode == "rebuilt-two") {
            ov::genai::ChatHistory retained;
            for (int turn = 0; turn < 2; ++turn) {
                retained.push_back(
                    {{"role", std::string("user")}, {"content", std::string("hello")}});
                ov::genai::LLMPipeline fresh(
                    package, "CPU", ov::AnyMap{{"ATTENTION_BACKEND", std::string("SDPA")}});
                fresh.get_tokenizer().set_chat_template(std::string(route_chat_template));
                ov::genai::GenerationConfig turn_config = fresh.get_generation_config();
                turn_config.max_new_tokens = 2U;
                turn_config.do_sample = false;
                turn_config.apply_chat_template = true;
                ov::genai::ChatHistory request(retained.get_messages());
                ov::genai::DecodedResults turn_result = fresh.generate(request, turn_config);
                if (turn_result.texts.empty()) return 1;
                std::cout << "rebuilt:" << turn << ':' << retained.size() << ':'
                          << turn_result.texts.front() << '\n';
                retained.push_back({{"role", std::string("assistant")},
                                    {"content", turn_result.texts.front()}});
            }
            return 0;
        }
        ov::genai::LLMPipeline pipeline(
            package, "CPU", ov::AnyMap{{"ATTENTION_BACKEND", std::string("SDPA")}});
        ov::genai::GenerationConfig config = pipeline.get_generation_config();
        config.max_new_tokens = 2U;
        config.do_sample = false;

        ov::genai::DecodedResults result;
        if (mode == "plain") {
            config.apply_chat_template = false;
            result = pipeline.generate(std::string("hello"), config);
        } else if (mode == "chat" || mode == "chat-stream" || mode == "chat-text-stream" ||
                   mode == "chat-text-stop") {
            pipeline.get_tokenizer().set_chat_template(std::string(route_chat_template));
            config.apply_chat_template = true;
            ov::genai::ChatHistory history;
            history.push_back(
                {{"role", std::string("user")}, {"content", std::string("hello")}});
            if (mode == "chat-stream") {
                const auto stream = std::make_shared<token_collector>();
                result = pipeline.generate(history, config, stream);
                std::cout << "tokens:";
                for (const auto token : stream->tokens) std::cout << token << ',';
                std::cout << " decoded:" << pipeline.get_tokenizer().decode(stream->tokens) << '\n';
            } else if (mode == "chat-text-stream" || mode == "chat-text-stop") {
                std::size_t callback_count = 0U;
                const bool stop_immediately = mode == "chat-text-stop";
                ov::genai::StreamerVariant stream = [&callback_count, stop_immediately](std::string fragment) {
                    if (stop_immediately) return ov::genai::StreamingStatus::STOP;
                    std::cout << "fragment:" << fragment << '\n';
                    ++callback_count;
                    return ov::genai::StreamingStatus::RUNNING;
                };
                result = pipeline.generate(history, config, stream);
                std::cout << "callbacks:" << callback_count << '\n';
            } else {
                result = pipeline.generate(history, config);
            }
        } else {
            return 2;
        }

        std::cout << mode << ':' << result.texts.size() << ':';
        if (!result.texts.empty()) std::cout << result.texts.front();
        std::cout << " input:" << result.perf_metrics.get_num_input_tokens()
                  << " generated:" << result.perf_metrics.get_num_generated_tokens() << '\n';
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "probe_failed:" << error.what() << '\n';
        return 1;
    }
}
