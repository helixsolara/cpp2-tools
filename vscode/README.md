# Cpp2 for VS Code

Syntax highlighting for Cpp2 (cppfront) `.cpp2` and `.h2` files. Building goes
through CMake Tools with the Cpp2 CMake module (`cmake/Cpp2.cmake`).

The grammar and language configuration live in `../grammar`, shared with the
Visual Studio extension; `install.ps1` copies them in. Install for the current
user with:

    powershell -NoProfile -ExecutionPolicy Bypass -File vscode\install.ps1

then reload VS Code (Developer: Reload Window).
