# Cpp2 for Visual Studio

A VSIX for Visual Studio 2022 17.14 and later (up to 19.0). It ships a cppfront
built from https://github.com/helixsolara/cppfront (branch `ena/modules`, the
`external/cppfront` submodule), with its include folder and license.

- **Highlighting.** The shared TextMate grammar and language configuration from
  `../grammar` give `.cpp2` and `.h2` files highlighting, bracket matching and
  comment toggling.
- **Build > Transpile Cpp2 File** (also on the editor's context menu, shown only
  for `.cpp2` and `.h2` files). It saves the file and runs cppfront on it the way
  its CMake build does, using the `cpp2_sources.json` the Cpp2 CMake module
  writes into the build folder (looked for in `out/build/*`, `.out/build/*`,
  `build`, `build/*` and `cmake-build-*` beside the file and in each folder
  above it). For a file no build lists, it uses **Tools > Options > Cpp2**. The
  result goes to `%TEMP%\cpp2-tools\transpiled\` and opens read-only; cppfront's
  output goes to the **Cpp2** pane of the Output window, and its errors to the
  Error List.
- **Build > Go To Generated Code** (also on the context menu, for the same
  files). It opens the file the CMake build generated from the current file,
  as `cpp2_sources.json` records it, read-only in the editor, at the code
  generated from the caret's line (found through cppfront's `#line`
  directives, preferring a function's definition to its declaration). The
  status bar says when the generated code is older than the file; build to
  update it.
- **Build > Build and Run Cpp2 File** (also on the context menu, for `.cpp2`
  files). It builds the file on its own, with no project or CMake: cppfront and
  its options from **Tools > Options > Cpp2**, then a debug build with this
  Visual Studio's x64 compiler (the C++ workload is needed). With `-pure-cpp2`
  in the options, it first builds the `std` and `std.compat` modules, once per
  compiler version, into `%TEMP%\cpp2-tools\std\`. Errors from cppfront and the
  compiler go to the Error List at their `.cpp2` lines, and the program runs in
  a console window that stays open. Its files go to `%TEMP%\cpp2-tools\run\`.
- **Tools > Options > Cpp2**: the cppfront path (empty for the bundled one) and
  its options, used by Build and Run Cpp2 File, and by Transpile Cpp2 File for
  a file no CMake build lists.

## Cpp2 in C++ projects (.vcxproj)

The extension's MSBuild rules (`../msbuild`: `Cpp2.props`, `Cpp2.targets` and
`Cpp2.xml`) let an ordinary C++ project contain `.cpp2` and `.h2` files.
Visual Studio keeps a copy of them, with the bundled cppfront, in
`%LOCALAPPDATA%\cpp2-tools\msbuild`, updated whenever the extension is. To use
them in a project:

1. Right-click the project, **Build Dependencies > Build Customizations**,
   **Find Existing**, and choose `%LOCALAPPDATA%\cpp2-tools\msbuild\Cpp2.targets`;
   make sure it's ticked.
2. Add `.cpp2` and `.h2` files to the project. They get the **Cpp2 source**
   item type and a **Cpp2** property page (**Pure Cpp2**, **Additional
   Options**).
3. Set **C/C++ > Language > C++ Language Standard** to latest. For pure Cpp2,
   also turn on **Build ISO C++23 Standard Library Modules**.

Building runs cppfront on changed files only, into `$(IntDir)cpp2\`, then
compiles the result: module interfaces as module interface units, internal
partitions as such, other files as ordinary C++. cppfront's errors and the
compiler's both point at `.cpp2` lines, and debugging steps through the `.cpp2`
source. The project then refers to a path in your profile; for a project others
build too, copy the `msbuild` folder into the repository and point Build
Customizations there instead.

## Building and testing

Build it from a Developer prompt, after `git submodule update --init` (this
restores the VS SDK packages from NuGet and builds cppfront with this Visual
Studio's compiler):

    msbuild visualstudio\Cpp2.VisualStudio.csproj /restore /p:Configuration=Release

and install it, with Visual Studio closed:

    "%VSINSTALLDIR%Common7\IDE\VSIXInstaller.exe" visualstudio\bin\Release\Cpp2.VisualStudio.vsix

Opening the project in Visual Studio and pressing F5 runs it in the
experimental instance instead.

`tests\Test-ExtensionLogic.ps1` tests the logic that needs no Visual Studio
(finding the file in a sources file, parsing diagnostics, running cppfront,
mapping source lines to generated ones, building a single file, copying the
MSBuild rules) against the cpp2-tools build, and `..\tests\msbuild\Test-Cpp2MSBuild.ps1`
builds and runs test projects that use the MSBuild rules:

    powershell -NoProfile -ExecutionPolicy Bypass -File visualstudio\tests\Test-ExtensionLogic.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tests\msbuild\Test-Cpp2MSBuild.ps1
