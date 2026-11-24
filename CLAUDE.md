# cpp2-tools

Tooling for Cpp2 (cppfront), split out of Ena (`D:\Src\Helix\Ena`), which uses it
as the submodule `lang/cpp2/tools`. The general rules live in the shared
claude-rules repo.

## Layout

- `cmake/Cpp2.cmake`: the CMake module (`include (Cpp2)`), defining
  `cpp2::cppfront`, `cpp2::cpp2util` and `cpp2_target_sources`.
- `tests/cmake`: programs built with the module; ctest checks their output.
- `grammar/`: the TextMate grammar both editors use, `cpp2.tmLanguage.json`,
  written by `generate.py` (edit the script, not the JSON, then run
  `python grammar/generate.py`), and `language-configuration.json`.
- `vscode/`: the VS Code extension, highlighting only (VS Code builds through
  CMake Tools). It has no code, so it needs no Node; `install.ps1` assembles it
  with the shared grammar in `%USERPROFILE%\.vscode\extensions`, where VS Code
  picks it up after a reload.
- Planned: `visualstudio/`, a VSIX: highlighting first, then build support
  (cppfront errors in the Error List, transpiling the current file, opening the
  generated code, and building without CMake).

## Grammar notes

- A declaration's kind comes from what follows `name:` (a lookahead): `namespace`,
  `type` (after template parameters, metafunctions and `final`), `concept`, `(`
  for functions, anything else a variable. Names after `.` or `::` aren't
  declarations, so `handlers.paint = :() = ...` stays a member access.
- String interpolation `(expr)$` is recognized only when a lookahead finds the
  closing `)$`, with up to two levels of nested parentheses.

## CMake module notes

- Generated files go to `<binary dir>/cpp2/<target>/<path relative to DIRECTORY>`.
- Module interface units are found by a regex on the file's first module
  declaration at configure time, so each `.cpp2` is a `CMAKE_CONFIGURE_DEPENDS`.
- cppfront writes an empty `cpp2_post_build.sh` into its working folder, so the
  custom command runs in the output folder.
- cppfront has no CMakeLists.txt, so FetchContent gets `SOURCE_SUBDIR "no-cmake"`
  to only download it.
- The command and its dependency use `$<TARGET_FILE:cpp2::cppfront>`, which works
  for both the built and the imported cppfront; `add_dependencies` on
  `cpp2_cppfront` orders the build for the Visual Studio generator.
- cppfront writes `#line` directives, so compiler errors already point at `.cpp2`
  lines.

## Repository

Public on GitHub: https://github.com/helixsolara/cpp2-tools (`origin`). Changes
reach `main` through pull requests, per the shared Conventions rule. It started as
a bare repo on OneDrive (`O:\OneDrive\Git\Helix\cpp2-tools`), no longer used.
Ena's submodule points at the GitHub URL.
