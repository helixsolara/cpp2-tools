# cpp2-tools

Tools for writing [Cpp2](https://github.com/hsutter/cppfront) (cppfront) code:

- `cmake/Cpp2.cmake`: a CMake module that builds cppfront and transpiles a
  target's `.cpp2` and `.h2` files, including C++20 module units.
- Syntax highlighting for VS Code and Visual Studio, and build support in Visual
  Studio (in progress).

## The CMake module

```cmake
list (APPEND CMAKE_MODULE_PATH "${CMAKE_SOURCE_DIR}/external/cpp2-tools/cmake")
set (CPP2_CPPFRONT_SOURCE_DIR "${CMAKE_SOURCE_DIR}/external/cppfront")  # optional
include (Cpp2)

add_executable (app)
cpp2_target_sources (app PURE_CPP2)
```

`add_subdirectory` or `FetchContent_MakeAvailable` on this repo works too and puts
`cmake/` on the caller's `CMAKE_MODULE_PATH`.

cppfront comes from `CPP2_CPPFRONT_EXECUTABLE` (prebuilt, with
`CPP2_CPPFRONT_INCLUDE_DIR`), else `CPP2_CPPFRONT_SOURCE_DIR` (a checkout, built
in the project), else it is fetched from `CPP2_CPPFRONT_GIT_REPOSITORY` at
`CPP2_CPPFRONT_GIT_TAG` (upstream, `v0.8.1`).

`cpp2_target_sources (<target> [PURE_CPP2] [DIRECTORY <dir>] [SOURCES <files>...] [OPTIONS <cppfront options>...])`
transpiles the given files, or every `.cpp2` and `.h2` under `DIRECTORY` (the
calling folder by default). A `.cpp2` that starts a module interface or partition
(`export module m;`, `module m:part;`) becomes a `.ixx` in the target's
`CXX_MODULES` file set; other `.cpp2` files become `.cpp` sources; `.h2` files
become `.h` files beside them. CMake 3.28 or later.

## Tests

```powershell
cmake --preset x64-debug -DCPP2_CPPFRONT_SOURCE_DIR=<cppfront checkout> -DCPP2_TEST_MODULES=ON
cmake --build .out/build/x64-debug
ctest --test-dir .out/build/x64-debug
```

`CPP2_TEST_MODULES` needs a cppfront that can write modules, such as
https://github.com/helixsolara/cppfront (branch `ena/modules`).
