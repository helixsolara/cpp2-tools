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
