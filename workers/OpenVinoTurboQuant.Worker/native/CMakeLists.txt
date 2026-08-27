cmake_minimum_required(VERSION 3.25)
project(OpenVinoTurboQuantNativeTests LANGUAGES CXX)

if(NOT WIN32 OR NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
    message(FATAL_ERROR "TurboQuant conformance supports Windows x64 only.")
endif()
if(NOT DEFINED OPENVINO_SOURCE_ROOT)
    message(FATAL_ERROR "OPENVINO_SOURCE_ROOT must identify the verified pinned source.")
endif()

find_package(OpenVINO CONFIG REQUIRED COMPONENTS Runtime)

add_executable(tbq4_codec_tests tbq4_codec_tests.cpp)
target_compile_features(tbq4_codec_tests PRIVATE cxx_std_20)
target_include_directories(tbq4_codec_tests PRIVATE
    "${OPENVINO_SOURCE_ROOT}/src/plugins/intel_cpu/src/nodes/kernels/scaled_attn/codecs")

add_executable(tbq4_dispatch_tests tbq4_dispatch_tests.cpp)
target_compile_features(tbq4_dispatch_tests PRIVATE cxx_std_20)
target_link_libraries(tbq4_dispatch_tests PRIVATE openvino::runtime)

foreach(target tbq4_codec_tests tbq4_dispatch_tests)
    if(MSVC)
        target_compile_options(${target} PRIVATE /W4 /WX /utf-8 /guard:cf)
        target_link_options(${target} PRIVATE /DYNAMICBASE /NXCOMPAT /HIGHENTROPYVA /GUARD:CF)
    endif()
endforeach()

include(CTest)
add_test(NAME tbq4_codec_tests COMMAND tbq4_codec_tests)
add_test(NAME tbq4_dispatch_tests COMMAND tbq4_dispatch_tests)
