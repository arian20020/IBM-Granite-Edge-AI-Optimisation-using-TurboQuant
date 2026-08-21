cmake_minimum_required(VERSION 3.25)
project(OpenVinoOfficialWorker LANGUAGES CXX)

if(NOT WIN32 OR NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
    message(FATAL_ERROR "The official worker supports Windows x64 only.")
endif()

if(NOT DEFINED NLOHMANN_JSON_INCLUDE_DIR)
    message(FATAL_ERROR "NLOHMANN_JSON_INCLUDE_DIR must identify the verified Task 1 header closure.")
endif()

find_package(OpenVINO CONFIG REQUIRED COMPONENTS Runtime)
find_package(OpenVINOGenAI CONFIG REQUIRED)

add_library(openvino_official_core STATIC
    src/namespace_monitor.cpp
    src/protocol.cpp
    src/package_inspector.cpp
    src/session.cpp
    src/runtime_evidence.cpp
    src/terminal_publication.cpp)
target_compile_features(openvino_official_core PUBLIC cxx_std_20)
target_compile_definitions(openvino_official_core PRIVATE NOMINMAX WIN32_LEAN_AND_MEAN)
target_include_directories(openvino_official_core PUBLIC src PRIVATE "${NLOHMANN_JSON_INCLUDE_DIR}")
target_link_libraries(openvino_official_core PUBLIC openvino::runtime openvino::genai bcrypt version psapi)

add_executable(OpenVinoOfficial.Worker src/main.cpp)
target_compile_features(OpenVinoOfficial.Worker PRIVATE cxx_std_20)
target_include_directories(OpenVinoOfficial.Worker PRIVATE "${NLOHMANN_JSON_INCLUDE_DIR}")
target_link_libraries(OpenVinoOfficial.Worker PRIVATE openvino_official_core delayimp)
if(MSVC)
    target_compile_options(openvino_official_core PRIVATE /W4 /WX /utf-8 /guard:cf)
    target_compile_options(OpenVinoOfficial.Worker PRIVATE /W4 /WX /utf-8 /guard:cf)
    target_link_options(OpenVinoOfficial.Worker PRIVATE
        /DYNAMICBASE /NXCOMPAT /HIGHENTROPYVA /GUARD:CF
        /DELAYLOAD:openvino.dll /DELAYLOAD:openvino_genai.dll)
endif()

include(CTest)
if(BUILD_TESTING)
    add_executable(openvino_official_namespace_monitor_tests
        tests/namespace_monitor_tests.cpp
        src/namespace_monitor.cpp)
    target_compile_features(openvino_official_namespace_monitor_tests PRIVATE cxx_std_20)
    target_compile_definitions(openvino_official_namespace_monitor_tests PRIVATE NOMINMAX WIN32_LEAN_AND_MEAN)
    target_include_directories(openvino_official_namespace_monitor_tests PRIVATE src)
    if(MSVC)
        target_compile_options(openvino_official_namespace_monitor_tests PRIVATE /W4 /WX /utf-8)
    endif()
    add_test(
        NAME openvino_official_namespace_monitor_tests
        COMMAND openvino_official_namespace_monitor_tests)

    add_executable(openvino_official_protocol_tests tests/protocol_tests.cpp)
    target_compile_features(openvino_official_protocol_tests PRIVATE cxx_std_20)
    target_include_directories(openvino_official_protocol_tests PRIVATE src "${NLOHMANN_JSON_INCLUDE_DIR}")
    target_link_libraries(openvino_official_protocol_tests PRIVATE openvino_official_core)
    add_test(NAME openvino_official_protocol_tests COMMAND openvino_official_protocol_tests)

    add_executable(openvino_official_session_tests tests/session_tests.cpp)
    target_compile_features(openvino_official_session_tests PRIVATE cxx_std_20)
    target_include_directories(openvino_official_session_tests PRIVATE src "${NLOHMANN_JSON_INCLUDE_DIR}")
    target_link_libraries(openvino_official_session_tests PRIVATE openvino_official_core)
    add_test(NAME openvino_official_session_tests COMMAND openvino_official_session_tests)

    add_executable(openvino_official_api_probe tests/api_probe.cpp)
    target_compile_features(openvino_official_api_probe PRIVATE cxx_std_20)
    target_include_directories(openvino_official_api_probe PRIVATE src "${NLOHMANN_JSON_INCLUDE_DIR}")
    target_link_libraries(openvino_official_api_probe PRIVATE openvino_official_core)
    if(MSVC)
        target_compile_options(openvino_official_api_probe PRIVATE /W4 /WX /utf-8)
    endif()
    if(DEFINED OFFICIAL_FIXTURE_PACKAGE)
        get_filename_component(official_runtime_root "${OpenVINO_DIR}/.." ABSOLUTE)
        add_test(
            NAME openvino_official_two_turn_fixture
            COMMAND openvino_official_api_probe "${OFFICIAL_FIXTURE_PACKAGE}" official-two "${OFFICIAL_WORKER_STAGE}")
        set_tests_properties(openvino_official_two_turn_fixture PROPERTIES
            ENVIRONMENT_MODIFICATION
                "PATH=path_list_prepend:${official_runtime_root}/bin/intel64/Release;PATH=path_list_prepend:${official_runtime_root}/3rdparty/tbb/bin")

        add_executable(openvino_official_lease_tests tests/lease_tests.cpp)
        target_compile_features(openvino_official_lease_tests PRIVATE cxx_std_20)
        target_include_directories(openvino_official_lease_tests PRIVATE src "${NLOHMANN_JSON_INCLUDE_DIR}")
        target_link_libraries(openvino_official_lease_tests PRIVATE openvino_official_core)
        if(MSVC)
            target_compile_options(openvino_official_lease_tests PRIVATE /W4 /WX /utf-8)
        endif()
        add_custom_command(TARGET openvino_official_lease_tests POST_BUILD
            COMMAND "${CMAKE_COMMAND}" -E copy_if_different
                "${official_runtime_root}/bin/intel64/Release/openvino_tokenizers.dll"
                "$<TARGET_FILE_DIR:openvino_official_lease_tests>/openvino_tokenizers.dll"
            VERBATIM)
        add_custom_command(TARGET openvino_official_api_probe POST_BUILD
            COMMAND "${CMAKE_COMMAND}" -E copy_if_different
                "${official_runtime_root}/bin/intel64/Release/openvino_tokenizers.dll"
                "$<TARGET_FILE_DIR:openvino_official_api_probe>/openvino_tokenizers.dll"
            VERBATIM)
        add_test(
            NAME openvino_official_lease_tests
            COMMAND openvino_official_lease_tests "${OFFICIAL_FIXTURE_PACKAGE}" "${OFFICIAL_WORKER_STAGE}")
        set_tests_properties(openvino_official_lease_tests PROPERTIES
            ENVIRONMENT_MODIFICATION
                "PATH=path_list_prepend:${official_runtime_root}/bin/intel64/Release;PATH=path_list_prepend:${official_runtime_root}/3rdparty/tbb/bin")

        add_executable(openvino_official_terminal_publication_tests
            tests/terminal_publication_tests.cpp)
        target_compile_features(openvino_official_terminal_publication_tests PRIVATE cxx_std_20)
        target_include_directories(openvino_official_terminal_publication_tests PRIVATE src "${NLOHMANN_JSON_INCLUDE_DIR}")
        target_link_libraries(openvino_official_terminal_publication_tests PRIVATE openvino_official_core)
        if(MSVC)
            target_compile_options(openvino_official_terminal_publication_tests PRIVATE /W4 /WX /utf-8)
        endif()
        add_custom_command(TARGET openvino_official_terminal_publication_tests POST_BUILD
            COMMAND "${CMAKE_COMMAND}" -E copy_if_different
                "${official_runtime_root}/bin/intel64/Release/openvino_tokenizers.dll"
                "$<TARGET_FILE_DIR:openvino_official_terminal_publication_tests>/openvino_tokenizers.dll"
            VERBATIM)
        add_test(
            NAME openvino_official_terminal_publication_tests
            COMMAND openvino_official_terminal_publication_tests "${OFFICIAL_FIXTURE_PACKAGE}" "${OFFICIAL_WORKER_STAGE}")
        set_tests_properties(openvino_official_terminal_publication_tests PROPERTIES
            ENVIRONMENT_MODIFICATION
                "PATH=path_list_prepend:${official_runtime_root}/bin/intel64/Release;PATH=path_list_prepend:${official_runtime_root}/3rdparty/tbb/bin")

        if(DEFINED OFFICIAL_WORKER_STAGE)
            add_executable(openvino_official_runtime_lease_tests tests/runtime_lease_tests.cpp)
            target_compile_features(openvino_official_runtime_lease_tests PRIVATE cxx_std_20)
            target_include_directories(openvino_official_runtime_lease_tests PRIVATE src "${NLOHMANN_JSON_INCLUDE_DIR}")
            target_link_libraries(openvino_official_runtime_lease_tests PRIVATE openvino_official_core)
            if(MSVC)
                target_compile_options(openvino_official_runtime_lease_tests PRIVATE /W4 /WX /utf-8)
            endif()
            add_test(
                NAME openvino_official_runtime_lease_tests
                COMMAND openvino_official_runtime_lease_tests "${OFFICIAL_WORKER_STAGE}")
            set_tests_properties(openvino_official_runtime_lease_tests PROPERTIES
                ENVIRONMENT_MODIFICATION
                    "PATH=path_list_prepend:${official_runtime_root}/bin/intel64/Release;PATH=path_list_prepend:${official_runtime_root}/3rdparty/tbb/bin")
        endif()
    endif()
endif()

install(TARGETS OpenVinoOfficial.Worker RUNTIME DESTINATION .)
