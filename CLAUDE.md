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
  (`OpenGeneratedCommand`) and Build and Run Cpp2 File (`BuildAndRunCommand`),
  all in `Cpp2Commands.vsct`, and the options page (`Cpp2Options`, whose
  `Cppfront()` and `CppfrontOptions()` are the settings for building without
  CMake). `Cpp2Documents` holds what the commands ask of the active document.
  `GeneratedCode` maps a source line to a generated one through the `#line`
  directives, preferring the last match, since cppfront declares functions
  before defining them. The generated file is made read-only in the editor
  (`BSF_USER_READONLY`), not on disk, since the build rewrites it.
  `SourcesFile` reads the CMake module's `cpp2_sources.json`, `Cppfront` runs
  cppfront (through `ToolRunner`) and knows the bundled one, `Diagnostic` parses
  cppfront's and MSVC's errors, `SingleFileBuild` writes Build and Run's cmd
  script, `MSBuildRules` keeps the stable copy of the MSBuild rules, and
  `Cpp2Output` writes the Output pane and the Error List.
  `tests/Test-ExtensionLogic.ps1` loads the built DLL with `Assembly.LoadFrom`
  (not `Add-Type`, which would load the package types and need Visual Studio)
  and tests everything but the commands' Visual Studio parts.
- `msbuild/`: MSBuild rules for `.cpp2` and `.h2` files in a `.vcxproj`
  (`Cpp2.props` defaults, `Cpp2.targets` steps, `Cpp2.xml` item type and
  property page), the three files Build Customizations wants. The VSIX ships
  them in `MSBuild\` with the bundled cppfront in `MSBuild\cppfront\`, and the
  package copies that folder to `%LOCALAPPDATA%\cpp2-tools\msbuild`, a path
  that survives updates. `tests/msbuild` holds test projects and
  `Test-Cpp2MSBuild.ps1`, which builds, runs and rebuilds them.
- `external/cppfront`: the helixsolara cppfront fork (branch `ena/modules`) as a
  submodule. The VSIX build compiles it (`BuildCppfront` in the csproj, with
  `vcvars64.bat` from the Visual Studio running MSBuild, `/O2 /MT`) into
  `visualstudio/obj/cppfront`, rebuilding only when its sources change.

## Visual Studio notes

- cppfront reports errors as `path(line,col): error: ...`, the format the Error
  List reads. Upstream cppfront prints only the file name, which the Error List
  can't open from a CMake build (cppfront runs in the output folder); the
  helixsolara fork (`ena/modules`, d148963) prints the path as given, and the
  CMake module passes full paths. Compiler errors in generated code already
  point at `.cpp2` lines through cppfront's `#line` directives.
- MSBuild rules: generated `ClCompile` items are added in the build's source
  generation step (`BuildGenerateSourcesTargets`), before anything compiles.
  They are made by the classifying task as new items, not by transforming
  `Cpp2Compile` items, since a transform would carry `AdditionalOptions` (the
  cppfront options) into the compiler's. `Output` is a reserved metadata name,
  hence `Cpp2Output`. The cppfront step is batched per source with
  `Inputs`/`Outputs`, so unchanged files are skipped, and skipped in design-time
  builds.
- Build and Run: `cpp2util.h` imports `std.compat` in pure mode, so both `std`
  and `std.compat` are built. In a cmd script, a quoted folder for `cl`'s
  `/Fo` or `/Fd` must end in a doubled backslash, or the backslash escapes the
  closing quote.
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
reach `main` through pull requests, per the shared Conventions rule.
`.github/workflows/ci.yml` runs on every pull request and push to `main`
(windows-latest): the grammar check, the CMake tests with modules, the VSIX
build, `Test-ExtensionLogic.ps1` and `Test-Cpp2MSBuild.ps1`, keeping the VSIX
as an artifact. Its `windows` check is required on `main` (branch protection,
with no review required), so a pull request merges only once CI passes. The
repository's auto-merge setting is off, so `gh pr merge --auto` doesn't wait:
it merges at once when it can, and is refused while the check is pending. It started as
a bare repo on OneDrive (`O:\OneDrive\Git\Helix\cpp2-tools`), no longer used.
Ena's submodule points at the GitHub URL.
