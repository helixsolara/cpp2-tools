# Checks the sources file the Cpp2 module writes for editor tools: it parses,
# names an existing cppfront, and lists each transpiled file with its output.
#
#   cmake -DSOURCES_FILE=<path> -DEXPECTED_COUNT=<n> -P check_sources_file.cmake

file (READ "${SOURCES_FILE}" CONTENT)

string (JSON VERSION GET "${CONTENT}" version)
if (NOT VERSION EQUAL 1)
    message (FATAL_ERROR "version is ${VERSION}, not 1")
endif ()

string (JSON CPPFRONT GET "${CONTENT}" cppfront)
if (NOT EXISTS "${CPPFRONT}")
    message (FATAL_ERROR "cppfront ${CPPFRONT} doesn't exist")
endif ()

string (JSON COUNT LENGTH "${CONTENT}" sources)
if (NOT COUNT EQUAL EXPECTED_COUNT)
    message (FATAL_ERROR "${COUNT} sources listed, expected ${EXPECTED_COUNT}")
endif ()

math (EXPR LAST "${COUNT} - 1")
foreach (INDEX RANGE ${LAST})
    string (JSON SOURCE GET "${CONTENT}" sources ${INDEX} source)
    string (JSON OUTPUT GET "${CONTENT}" sources ${INDEX} output)
    if (NOT EXISTS "${SOURCE}" OR NOT EXISTS "${OUTPUT}")
        message (FATAL_ERROR "Entry ${INDEX}: ${SOURCE} or its output ${OUTPUT} is missing")
    endif ()
endforeach ()

message ("Sources file checked: ${COUNT} sources")
