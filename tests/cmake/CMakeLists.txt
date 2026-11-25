# Tests of the Cpp2 module: each builds a small program and checks its output.
# Run them with ctest from the build folder.

# A .cpp2 program that includes a .h2 header
add_executable (cpp2_plain)
cpp2_target_sources (cpp2_plain DIRECTORY plain)
add_test (NAME Plain COMMAND cpp2_plain)
set_property (TEST Plain PROPERTY PASS_REGULAR_EXPRESSION "Hello from Cpp2, 42")

# A module written in Cpp2, and a program that imports it. Upstream cppfront
# can't write modules, so this needs a cppfront that can, such as
# https://github.com/helixsolara/cppfront (branch ena/modules)
option (CPP2_TEST_MODULES "Test Cpp2 modules (needs a cppfront with module support)" OFF)
if (CPP2_TEST_MODULES)
    add_library (cpp2_greeting STATIC)
    cpp2_target_sources (cpp2_greeting DIRECTORY modules/greeting)
    add_executable (cpp2_modules)
    cpp2_target_sources (cpp2_modules DIRECTORY modules/app)
    target_link_libraries (cpp2_modules PRIVATE cpp2_greeting)
    add_test (NAME Modules COMMAND cpp2_modules)
    set_property (TEST Modules PROPERTY PASS_REGULAR_EXPRESSION "Hello from a Cpp2 module")
endif ()

# A debug cppfront built with Edit and Continue (/ZI), as Visual Studio's
# CMake projects do, overflows the default 1 MB stack on this file; Cpp2.cmake
# gives it a larger one. CI builds with /ZI so this test guards that
add_test (NAME DeepNesting
    COMMAND "$<TARGET_FILE:cpp2::cppfront>" -pure-cpp2
        "${CMAKE_CURRENT_SOURCE_DIR}/deep/nested_lambda.cpp2"
        -output "${CMAKE_CURRENT_BINARY_DIR}/nested_lambda.cpp")

# The sources file lists every transpiled file above (2 plain, 2 more with modules)
if (CPP2_TEST_MODULES)
    set (CPP2_EXPECTED_SOURCES 4)
else ()
    set (CPP2_EXPECTED_SOURCES 2)
endif ()
add_test (NAME SourcesFile
    COMMAND "${CMAKE_COMMAND}"
        "-DSOURCES_FILE=${CMAKE_BINARY_DIR}/cpp2_sources$<$<BOOL:$<CONFIG>>:-$<CONFIG>>.json"
        -DEXPECTED_COUNT=${CPP2_EXPECTED_SOURCES}
        -P "${CMAKE_CURRENT_SOURCE_DIR}/check_sources_file.cmake")
set_property (TEST SourcesFile PROPERTY PASS_REGULAR_EXPRESSION "Sources file checked")
