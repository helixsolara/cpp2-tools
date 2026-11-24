# Cpp2 for Visual Studio

A VSIX for Visual Studio 2022 and later (17.0 up to 19.0) that registers the
shared TextMate grammar and language configuration from `../grammar`, giving
`.cpp2` and `.h2` files syntax highlighting, bracket matching and comment
toggling. It has no code yet; build support comes next.

Build it from a Developer prompt (restoring Microsoft.VSSDK.BuildTools from NuGet):

    msbuild visualstudio\Cpp2.VisualStudio.csproj /restore /p:Configuration=Release

and install it, with Visual Studio closed:

    "%VSINSTALLDIR%Common7\IDE\VSIXInstaller.exe" visualstudio\bin\Release\Cpp2.VisualStudio.vsix

Opening the project in Visual Studio and pressing F5 runs it in the
experimental instance instead.
