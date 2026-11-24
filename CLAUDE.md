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
- `visualstudio/`: the Visual Studio extension, a VSIX built with MSBuild,
  Microsoft.VSSDK.BuildTools and Microsoft.VisualStudio.SDK 17.14 (an old-style
  csproj, net472, C# latest with nullable on and warnings as errors).
  `Cpp2.pkgdef` registers the shared grammar under `TextMate\Repositories` and
  the language configuration under `TextMate\LanguageConfiguration\GrammarMapping`;
  the csproj links both from `grammar/` into the VSIX's `Grammars` folder.
  `Cpp2Package` (an `AsyncPackage` that loads in the background) adds
  Transpile Cpp2 File (`TranspileCommand`), Go To Generated Code
  (`OpenGeneratedCommand`), both in `Cpp2Commands.vsct`, and the options page
  (`Cpp2Options`); `Cpp2Documents` holds what both commands ask of the active
  document. `GeneratedCode` maps a source line to a generated one through the
  `#line` directives, preferring the last match, since cppfront declares
  functions before defining them. The generated file is made read-only in the
  editor (`BSF_USER_READONLY`), not on disk, since the build rewrites it. `SourcesFile` reads the CMake module's
  `cpp2_sources.json`, `Cppfront` runs cppfront and parses its diagnostics, and
  `Cpp2Output` writes the Output pane and the Error List.
  `tests/Test-ExtensionLogic.ps1` loads the built DLL with `Assembly.LoadFrom`
  (not `Add-Type`, which would load the package types and need Visual Studio)
  and tests `SourcesFile`, `Cppfront` and `GeneratedCode`. Next: building without
  CMake.

## Visual Studio notes

- cppfront reports errors as `path(line,col): error: ...`, the format the Error
  List reads. Upstream cppfront prints only the file name, which the Error List
  can't open from a CMake build (cppfront runs in the output folder); the
  helixsolara fork (`ena/modules`, d148963) prints the path as given, and the
  CMake module passes full paths. Compiler errors in generated code already
  point at `.cpp2` lines through cppfront's `#line` directives.
- A debug cppfront's CRT assertions open a dialog. When testing broken input,
  build it with a forced-include header that calls `_CrtSetReportMode(_CRT_ASSERT,
  _CRTDBG_MODE_FILE)` so they go to stderr instead.

## Grammar notes

- A declaration's kind comes from what follows `name:` (a lookahead): `namespace`,
  `type` (after template parameters, metafunctions and `final`), `concept`, `(`
  for functions, anything else a variable. Names after `.` or `::` aren't
  declarations, so `handlers.paint = :() = ...` stays a member access.
- String interpolation `(expr)$` is recognized only when a lookahead finds the
  closing `)$`, with up to two levels of nested parentheses.

## CMake module notes

- Generated files go to `<binary dir>/cpp2/<target>/<path relative to DIRECTORY>`.
- Configuring writes `<build dir>/cpp2_sources.json` (`cpp2_sources-<config>.json`
  when a configuration is set) for editor tools: the cppfront path, and each
  source's target, output and options. Entries collect in a global property;
  the first one schedules `_cpp2_write_sources_file` with
  `cmake_language (DEFER DIRECTORY ${CMAKE_SOURCE_DIR})`, which writes the file
  with `file (GENERATE)` so the cppfront path can be `$<TARGET_FILE:...>`.
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
