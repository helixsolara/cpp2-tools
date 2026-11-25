# Cpp2: a CMake module for building Cpp2 (cppfront) code.
#
#     list (APPEND CMAKE_MODULE_PATH "<cpp2-tools>/cmake")
#     set (CPP2_CPPFRONT_SOURCE_DIR "<a cppfront checkout>")   # optional
#     include (Cpp2)
#
#     add_executable (app)
#     cpp2_target_sources (app PURE_CPP2)
#
# Including it defines two targets:
#
#   cpp2::cppfront  the cppfront executable
#   cpp2::cpp2util  an interface library with cppfront's include folder, which
#                   generated code needs for cpp2util.h
#
# cppfront comes from the first of these that is set:
#
#   CPP2_CPPFRONT_EXECUTABLE  a cppfront already built; CPP2_CPPFRONT_INCLUDE_DIR
#                             must then name its include folder
#   CPP2_CPPFRONT_SOURCE_DIR  a cppfront checkout (a submodule, say), built here
#   otherwise                 cppfront fetched from CPP2_CPPFRONT_GIT_REPOSITORY at
#                             CPP2_CPPFRONT_GIT_TAG and built here

include_guard (GLOBAL)

cmake_minimum_required (VERSION 3.28)

set (CPP2_CPPFRONT_EXECUTABLE "" CACHE FILEPATH "A prebuilt cppfront to use instead of building one")
set (CPP2_CPPFRONT_INCLUDE_DIR "" CACHE PATH "The include folder of CPP2_CPPFRONT_EXECUTABLE's cppfront")
set (CPP2_CPPFRONT_SOURCE_DIR "" CACHE PATH "A cppfront checkout to build cppfront from")
set (CPP2_CPPFRONT_GIT_REPOSITORY "https://github.com/hsutter/cppfront.git" CACHE STRING "Where to fetch cppfront from")
set (CPP2_CPPFRONT_GIT_TAG "v0.8.1" CACHE STRING "The cppfront tag, branch or commit to fetch")

# Defines cpp2::cppfront and cpp2::cpp2util from the settings above
function (_cpp2_define_targets)
    if (CPP2_CPPFRONT_EXECUTABLE)
        if (NOT CPP2_CPPFRONT_INCLUDE_DIR)
            message (FATAL_ERROR "CPP2_CPPFRONT_EXECUTABLE needs CPP2_CPPFRONT_INCLUDE_DIR, the folder holding cpp2util.h")
        endif ()
        add_executable (cpp2::cppfront IMPORTED GLOBAL)
        set_target_properties (cpp2::cppfront PROPERTIES IMPORTED_LOCATION "${CPP2_CPPFRONT_EXECUTABLE}")
        set (INCLUDE_DIR "${CPP2_CPPFRONT_INCLUDE_DIR}")
    else ()
        set (SOURCE_DIR "${CPP2_CPPFRONT_SOURCE_DIR}")
        if (NOT SOURCE_DIR)
            include (FetchContent)
            # cppfront has no CMakeLists.txt; naming a folder without one makes
            # FetchContent only download it
            FetchContent_Declare (cpp2_cppfront
                GIT_REPOSITORY "${CPP2_CPPFRONT_GIT_REPOSITORY}"
                GIT_TAG "${CPP2_CPPFRONT_GIT_TAG}"
                GIT_SHALLOW ON
                SOURCE_SUBDIR "no-cmake")
            FetchContent_MakeAvailable (cpp2_cppfront)
            set (SOURCE_DIR "${cpp2_cppfront_SOURCE_DIR}")
        endif ()
        if (NOT EXISTS "${SOURCE_DIR}/source/cppfront.cpp")
            message (FATAL_ERROR "No cppfront sources in ${SOURCE_DIR} (expected source/cppfront.cpp)")
        endif ()

        add_executable (cpp2_cppfront "${SOURCE_DIR}/source/cppfront.cpp")
        set_target_properties (cpp2_cppfront PROPERTIES OUTPUT_NAME cppfront)
        target_compile_features (cpp2_cppfront PRIVATE cxx_std_20)
        # cppfront recurses through a few very large functions. Unoptimized,
        # and above all with Edit and Continue (/ZI), which keeps every local
        # in a slot of its own, those frames overflow the default 1 MB stack
        # (0xC00000FD) on ordinary code, so give it 16 MB
        if (MSVC)
            target_compile_options (cpp2_cppfront PRIVATE /bigobj /EHsc)
            target_link_options (cpp2_cppfront PRIVATE /STACK:16777216)
        elseif (MINGW)
            target_compile_options (cpp2_cppfront PRIVATE -Wa,-mbig-obj)
            target_link_options (cpp2_cppfront PRIVATE -Wl,--stack,16777216)
        endif ()
        add_executable (cpp2::cppfront ALIAS cpp2_cppfront)
        set (INCLUDE_DIR "${SOURCE_DIR}/include")
    endif ()

    add_library (cpp2_cpp2util INTERFACE)
    target_include_directories (cpp2_cpp2util INTERFACE "${INCLUDE_DIR}")
    target_compile_features (cpp2_cpp2util INTERFACE cxx_std_20)
    add_library (cpp2::cpp2util ALIAS cpp2_cpp2util)
endfunction ()

if (NOT TARGET cpp2::cppfront)
    _cpp2_define_targets ()
endif ()

# The build's sources file -----------------------------------------------------
#
# Configuring writes <build dir>/cpp2_sources.json (cpp2_sources-<config>.json for
# a configuration's build), which tells editor tools how this build transpiles
# each Cpp2 file:
#
#   { "version": 1, "cppfront": "<path>",
#     "sources": [ { "target": "...", "source": "...", "output": "...",
#                    "options": ["-pure-cpp2", ...] }, ... ] }

# Sets OUT_VAR to TEXT quoted as a JSON string
function (_cpp2_json_string TEXT OUT_VAR)
    string (REPLACE "\\" "\\\\" TEXT "${TEXT}")
    string (REPLACE "\"" "\\\"" TEXT "${TEXT}")
    set (${OUT_VAR} "\"${TEXT}\"" PARENT_SCOPE)
endfunction ()

# Adds one transpiled file to the sources file, which is written once the top
# folder has been configured
function (_cpp2_record_source TARGET_NAME INPUT OUTPUT FLAGS)
    _cpp2_json_string ("${TARGET_NAME}" TARGET_JSON)
    _cpp2_json_string ("${INPUT}" INPUT_JSON)
    _cpp2_json_string ("${OUTPUT}" OUTPUT_JSON)
    set (OPTIONS_JSON "")
    foreach (FLAG IN LISTS FLAGS)
        _cpp2_json_string ("${FLAG}" FLAG_JSON)
        if (OPTIONS_JSON)
            string (APPEND OPTIONS_JSON ", ")
        endif ()
        string (APPEND OPTIONS_JSON "${FLAG_JSON}")
    endforeach ()
    set (ENTRY "    { \"target\": ${TARGET_JSON}, \"source\": ${INPUT_JSON}, \"output\": ${OUTPUT_JSON}, \"options\": [${OPTIONS_JSON}] }")

    get_property (ENTRIES GLOBAL PROPERTY _CPP2_SOURCES_JSON)
    if (ENTRIES)
        set_property (GLOBAL APPEND_STRING PROPERTY _CPP2_SOURCES_JSON ",\n${ENTRY}")
    else ()
        set_property (GLOBAL PROPERTY _CPP2_SOURCES_JSON "${ENTRY}")
        cmake_language (DEFER DIRECTORY "${CMAKE_SOURCE_DIR}" CALL _cpp2_write_sources_file)
    endif ()
endfunction ()

function (_cpp2_write_sources_file)
    get_property (ENTRIES GLOBAL PROPERTY _CPP2_SOURCES_JSON)
    file (GENERATE
        OUTPUT "${CMAKE_BINARY_DIR}/cpp2_sources$<$<BOOL:$<CONFIG>>:-$<CONFIG>>.json"
        CONTENT "{\n  \"version\": 1,\n  \"cppfront\": \"$<TARGET_FILE:cpp2::cppfront>\",\n  \"sources\": [\n${ENTRIES}\n  ]\n}\n")
endfunction ()

# cpp2_target_sources (<target> [PURE_CPP2] [DIRECTORY <dir>] [SOURCES <file>...]
#                      [OPTIONS <cppfront option>...])
#
# Transpiles Cpp2 sources with cppfront and adds the generated code to <target>.
#
#   SOURCES    the .cpp2 and .h2 files; without it, every one under DIRECTORY
#   DIRECTORY  the folder to search, recursively (the calling folder by default);
#              generated files keep their path relative to it, under
#              <binary dir>/cpp2/<target>, so same-named files don't collide
#   PURE_CPP2  passes -pure-cpp2: Cpp2 only, with import std
#   OPTIONS    further cppfront options, such as -fno-exceptions
#
# A .cpp2 whose module declaration makes it a module interface unit or partition
# ("export module m;" or "module m:part;") becomes a .ixx in the target's public
# CXX_MODULES file set; any other .cpp2 becomes a .cpp source. A .h2 becomes a .h
# beside the generated code, where a .cpp2's #include "x.h2" finds it.
#
# cppfront writes #line directives, so compiler errors in generated code point
# at the .cpp2 line.
function (cpp2_target_sources TARGET_NAME)
    cmake_parse_arguments (PARSE_ARGV 1 ARG "PURE_CPP2" "DIRECTORY" "SOURCES;OPTIONS")
    if (ARG_UNPARSED_ARGUMENTS)
        message (FATAL_ERROR "cpp2_target_sources: unknown arguments ${ARG_UNPARSED_ARGUMENTS}")
    endif ()

    set (BASE_DIR "${CMAKE_CURRENT_SOURCE_DIR}")
    if (ARG_DIRECTORY)
        cmake_path (ABSOLUTE_PATH ARG_DIRECTORY BASE_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}" OUTPUT_VARIABLE BASE_DIR)
    endif ()
    set (GEN_DIR "${CMAKE_CURRENT_BINARY_DIR}/cpp2/${TARGET_NAME}")

    set (INPUTS "")
    if (ARG_SOURCES)
        foreach (SOURCE IN LISTS ARG_SOURCES)
            cmake_path (ABSOLUTE_PATH SOURCE BASE_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}" OUTPUT_VARIABLE SOURCE)
            list (APPEND INPUTS "${SOURCE}")
        endforeach ()
    else ()
        file (GLOB_RECURSE INPUTS CONFIGURE_DEPENDS "${BASE_DIR}/*.cpp2" "${BASE_DIR}/*.h2")
    endif ()

    set (FLAGS ${ARG_OPTIONS})
    if (ARG_PURE_CPP2)
        list (APPEND FLAGS -pure-cpp2)
    endif ()

    set (MODULE_UNITS "")
    set (OTHER_UNITS "")
    foreach (INPUT IN LISTS INPUTS)
        cmake_path (RELATIVE_PATH INPUT BASE_DIRECTORY "${BASE_DIR}" OUTPUT_VARIABLE RELATIVE_INPUT)
        if (RELATIVE_INPUT MATCHES "^\\.\\./")
            message (FATAL_ERROR "cpp2_target_sources: ${INPUT} is outside ${BASE_DIR}; pass DIRECTORY")
        endif ()
        cmake_path (REMOVE_EXTENSION RELATIVE_INPUT LAST_ONLY OUTPUT_VARIABLE STEM)

        if (INPUT MATCHES "\\.h2$")
            set (OUTPUT "${GEN_DIR}/${STEM}.h")
            list (APPEND OTHER_UNITS "${OUTPUT}")
        else ()
            # The module declaration decides how the output is built, so the
            # configure step re-runs whenever the file changes
            set_property (DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS "${INPUT}")
            file (STRINGS "${INPUT}" MODULE_INTERFACE_DECLARATION LIMIT_COUNT 1
                REGEX "^[ \t]*(export[ \t]+module[ \t]|module[ \t]+[^;:]+:)")
            if (MODULE_INTERFACE_DECLARATION)
                set (OUTPUT "${GEN_DIR}/${STEM}.ixx")
                list (APPEND MODULE_UNITS "${OUTPUT}")
            else ()
                set (OUTPUT "${GEN_DIR}/${STEM}.cpp")
                list (APPEND OTHER_UNITS "${OUTPUT}")
            endif ()
        endif ()

        # cppfront writes an empty cpp2_post_build.sh into its working
        # folder, so it runs in the build tree
        cmake_path (GET OUTPUT PARENT_PATH OUTPUT_DIR)
        file (MAKE_DIRECTORY "${OUTPUT_DIR}")
        add_custom_command (
            OUTPUT "${OUTPUT}"
            COMMAND "$<TARGET_FILE:cpp2::cppfront>" ${FLAGS} "${INPUT}" -output "${OUTPUT}"
            WORKING_DIRECTORY "${OUTPUT_DIR}"
            DEPENDS "${INPUT}" "$<TARGET_FILE:cpp2::cppfront>"
            COMMENT "Transpiling ${RELATIVE_INPUT}"
            VERBATIM)
        _cpp2_record_source ("${TARGET_NAME}" "${INPUT}" "${OUTPUT}" "${FLAGS}")
    endforeach ()

    if (OTHER_UNITS)
        target_sources (${TARGET_NAME} PRIVATE ${OTHER_UNITS})
    endif ()
    if (MODULE_UNITS)
        target_sources (${TARGET_NAME}
            PUBLIC FILE_SET cpp2_modules TYPE CXX_MODULES BASE_DIRS "${GEN_DIR}" FILES ${MODULE_UNITS})
    endif ()
    if (INPUTS)
        target_link_libraries (${TARGET_NAME} PRIVATE cpp2::cpp2util)
        # Builds cppfront first; Visual Studio's generator needs this as well
        # as the file dependency above
        if (TARGET cpp2_cppfront)
            add_dependencies (${TARGET_NAME} cpp2_cppfront)
        endif ()
    endif ()
endfunction ()
