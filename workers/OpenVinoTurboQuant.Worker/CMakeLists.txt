cmake_minimum_required(VERSION 3.25)
project(OpenVinoTurboQuantWorker LANGUAGES CXX)

if(NOT WIN32 OR NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
    message(FATAL_ERROR "The TurboQuant worker supports Windows x64 only.")
endif()
if(NOT DEFINED NLOHMANN_JSON_INCLUDE_DIR)
    message(FATAL_ERROR "NLOHMANN_JSON_INCLUDE_DIR must identify the verified header closure.")
endif()
if(NOT DEFINED OPENVINO_TURBOQUANT_SOURCE_ROOT)
    message(FATAL_ERROR "OPENVINO_TURBOQUANT_SOURCE_ROOT must identify the pinned upstream source tree.")
endif()
if(NOT DEFINED OPENVINO_TURBOQUANT_GENAI_SOURCE_ROOT)
    message(FATAL_ERROR "OPENVINO_TURBOQUANT_GENAI_SOURCE_ROOT must identify the pinned GenAI source tree.")
endif()
if(NOT DEFINED OPENVINO_TURBOQUANT_IMPORT_LIBRARY_DIR)
    message(FATAL_ERROR "OPENVINO_TURBOQUANT_IMPORT_LIBRARY_DIR must contain exact-runtime import libraries.")
endif()

set(TURBOQUANT_DEV_API_INCLUDE
    "${OPENVINO_TURBOQUANT_SOURCE_ROOT}/src/inference/dev_api")
set(TURBOQUANT_CORE_DEV_API_INCLUDE
    "${OPENVINO_TURBOQUANT_SOURCE_ROOT}/src/core/dev_api")
set(TURBOQUANT_PUBLIC_INCLUDE
    "${OPENVINO_TURBOQUANT_SOURCE_ROOT}/src/inference/include")
set(TURBOQUANT_CORE_INCLUDE
    "${OPENVINO_TURBOQUANT_SOURCE_ROOT}/src/core/include")
if(NOT EXISTS "${TURBOQUANT_DEV_API_INCLUDE}/openvino/runtime/internal_properties.hpp")
    message(FATAL_ERROR "The pinned TurboQuant internal property header is missing.")
endif()

find_package(OpenVINO CONFIG REQUIRED COMPONENTS Runtime)
find_package(OpenVINOGenAI CONFIG REQUIRED)
set_target_properties(openvino::runtime PROPERTIES
    IMPORTED_IMPLIB_RELEASE "${OPENVINO_TURBOQUANT_IMPORT_LIBRARY_DIR}/openvino.lib")
set_target_properties(openvino::genai PROPERTIES
    IMPORTED_IMPLIB_RELEASE "${OPENVINO_TURBOQUANT_IMPORT_LIBRARY_DIR}/openvino_genai.lib")

set(TURBOQUANT_GENAI_INCLUDE
    "${OPENVINO_TURBOQUANT_GENAI_SOURCE_ROOT}/src/cpp/include")

set(OFFICIAL_SOURCE "${CMAKE_CURRENT_SOURCE_DIR}/../OpenVinoOfficial.Worker/src")
add_library(openvino_turboquant_core STATIC
    "${OFFICIAL_SOURCE}/namespace_monitor.cpp"
    "${OFFICIAL_SOURCE}/protocol.cpp"
    "${OFFICIAL_SOURCE}/package_inspector.cpp"
    src/session.cpp
    "${OFFICIAL_SOURCE}/runtime_evidence.cpp"
    "${OFFICIAL_SOURCE}/terminal_publication.cpp")
target_compile_features(openvino_turboquant_core PUBLIC cxx_std_20)
target_compile_definitions(openvino_turboquant_core PRIVATE
    NOMINMAX WIN32_LEAN_AND_MEAN GRANITE_TURBOQUANT_WORKER)
target_include_directories(openvino_turboquant_core PUBLIC
    src "${OFFICIAL_SOURCE}" PRIVATE "${NLOHMANN_JSON_INCLUDE_DIR}")
target_include_directories(openvino_turboquant_core BEFORE PRIVATE
    "${TURBOQUANT_DEV_API_INCLUDE}" "${TURBOQUANT_CORE_DEV_API_INCLUDE}"
    "${TURBOQUANT_PUBLIC_INCLUDE}" "${TURBOQUANT_CORE_INCLUDE}"
    "${TURBOQUANT_GENAI_INCLUDE}")
target_link_libraries(openvino_turboquant_core PUBLIC
    openvino::runtime openvino::genai bcrypt version psapi)

add_library(OpenVinoTurboQuant.Probe SHARED src/activation_evidence.cpp)
target_compile_features(OpenVinoTurboQuant.Probe PRIVATE cxx_std_20)
target_compile_definitions(OpenVinoTurboQuant.Probe PRIVATE GRANITE_TURBOQUANT_PROBE_BUILD)
target_include_directories(OpenVinoTurboQuant.Probe PRIVATE src "${NLOHMANN_JSON_INCLUDE_DIR}")
target_include_directories(OpenVinoTurboQuant.Probe BEFORE PRIVATE
    "${TURBOQUANT_DEV_API_INCLUDE}" "${TURBOQUANT_CORE_DEV_API_INCLUDE}"
    "${TURBOQUANT_PUBLIC_INCLUDE}" "${TURBOQUANT_CORE_INCLUDE}")
target_link_libraries(OpenVinoTurboQuant.Probe PRIVATE openvino::runtime)

add_executable(OpenVinoTurboQuant.Worker src/main.cpp)
target_compile_features(OpenVinoTurboQuant.Worker PRIVATE cxx_std_20)
target_compile_definitions(OpenVinoTurboQuant.Worker PRIVATE GRANITE_TURBOQUANT_WORKER)
target_include_directories(OpenVinoTurboQuant.Worker PRIVATE
    src "${OFFICIAL_SOURCE}" "${NLOHMANN_JSON_INCLUDE_DIR}")
target_include_directories(OpenVinoTurboQuant.Worker BEFORE PRIVATE
    "${TURBOQUANT_DEV_API_INCLUDE}" "${TURBOQUANT_CORE_DEV_API_INCLUDE}"
    "${TURBOQUANT_PUBLIC_INCLUDE}" "${TURBOQUANT_CORE_INCLUDE}")
target_link_libraries(OpenVinoTurboQuant.Worker PRIVATE
    openvino_turboquant_core OpenVinoTurboQuant.Probe delayimp)
if(MSVC)
    foreach(target openvino_turboquant_core OpenVinoTurboQuant.Probe OpenVinoTurboQuant.Worker)
        target_compile_options(${target} PRIVATE /W4 /WX /utf-8 /guard:cf /wd4100 /wd4305)
    endforeach()
    target_compile_options(OpenVinoTurboQuant.Probe PRIVATE /wd4251)
    target_link_options(OpenVinoTurboQuant.Worker PRIVATE
        /DYNAMICBASE /NXCOMPAT /HIGHENTROPYVA /GUARD:CF
        /DELAYLOAD:openvino.dll /DELAYLOAD:openvino_genai.dll
        /DELAYLOAD:OpenVinoTurboQuant.Probe.dll)
endif()

install(TARGETS OpenVinoTurboQuant.Worker RUNTIME DESTINATION .)

include(CTest)
if(BUILD_TESTING)
    add_executable(turboquant_activation_probe_tests tests/activation_evidence_tests.cpp)
    target_compile_features(turboquant_activation_probe_tests PRIVATE cxx_std_20)
    target_include_directories(turboquant_activation_probe_tests PRIVATE src "${NLOHMANN_JSON_INCLUDE_DIR}")
    target_include_directories(turboquant_activation_probe_tests BEFORE PRIVATE
        "${TURBOQUANT_DEV_API_INCLUDE}" "${TURBOQUANT_CORE_DEV_API_INCLUDE}"
        "${TURBOQUANT_PUBLIC_INCLUDE}" "${TURBOQUANT_CORE_INCLUDE}")
    target_link_libraries(turboquant_activation_probe_tests PRIVATE OpenVinoTurboQuant.Probe)
    if(MSVC)
        target_compile_options(turboquant_activation_probe_tests PRIVATE /W4 /WX /utf-8 /wd4251 /wd4100)
    endif()
    add_test(NAME turboquant_activation_probe_tests COMMAND turboquant_activation_probe_tests)
endif()
