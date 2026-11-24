# Cpp2 for Visual Studio

A VSIX for Visual Studio 2022 17.14 and later (up to 19.0):

- **Highlighting.** The shared TextMate grammar and language configuration from
  `../grammar` give `.cpp2` and `.h2` files highlighting, bracket matching and
  comment toggling.
- **Build > Transpile Cpp2 File** (also on the editor's context menu, shown only
  for `.cpp2` and `.h2` files). It saves the file and runs cppfront on it the way
  its CMake build does, using the `cpp2_sources.json` the Cpp2 CMake module
  writes into the build folder (looked for in `out/build/*`, `.out/build/*`,
  `build`, `build/*` and `cmake-build-*` beside the file and in each folder
  above it). For a file no build lists, it uses **Tools > Options > Cpp2**
  (the cppfront path, or cppfront on PATH, and its options). The result goes to
  `%TEMP%\cpp2-tools\transpiled\` and opens read-only; cppfront's output goes to
  the **Cpp2** pane of the Output window, and its errors to the Error List.

Build it from a Developer prompt (restoring the VS SDK packages from NuGet):

    msbuild visualstudio\Cpp2.VisualStudio.csproj /restore /p:Configuration=Release

and install it, with Visual Studio closed:

    "%VSINSTALLDIR%Common7\IDE\VSIXInstaller.exe" visualstudio\bin\Release\Cpp2.VisualStudio.vsix

Opening the project in Visual Studio and pressing F5 runs it in the
experimental instance instead.

`tests\Test-ExtensionLogic.ps1` tests the logic that needs no Visual Studio
(finding the file in a sources file, parsing diagnostics, running cppfront)
against the cpp2-tools build:

    powershell -NoProfile -ExecutionPolicy Bypass -File visualstudio\tests\Test-ExtensionLogic.ps1
