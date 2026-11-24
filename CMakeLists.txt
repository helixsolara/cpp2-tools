# cpp2-tools: the Cpp2 CMake module, plus its tests when built on its own.
#
# Another project can take the module from here with add_subdirectory or
# FetchContent_MakeAvailable, which also puts cmake/ on its CMAKE_MODULE_PATH,
# or by adding cmake/ to CMAKE_MODULE_PATH itself and calling include (Cpp2).

cmake_minimum_required (VERSION 3.28)

project (cpp2_tools LANGUAGES CXX)

list (APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/cmake")
if (NOT PROJECT_IS_TOP_LEVEL)
    set (CMAKE_MODULE_PATH "${CMAKE_MODULE_PATH}" PARENT_SCOPE)
endif ()

include (Cpp2)

if (PROJECT_IS_TOP_LEVEL)
    enable_testing ()
    add_subdirectory (tests/cmake)
endif ()
