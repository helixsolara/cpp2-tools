# TODO

## Only the user can do
- Decide whether to publish the extensions (VS Code Marketplace, Visual Studio
  Marketplace); both need publisher accounts.
- Install Node.js if the VS Code extension should be packaged as a `.vsix`
  (`vsce` needs it). Until then it is installed by copying its folder.

## Not verified
- Fetching cppfront with FetchContent (no `CPP2_CPPFRONT_SOURCE_DIR`): not run.
- A prebuilt cppfront through `CPP2_CPPFRONT_EXECUTABLE`: not run.
- The Visual Studio generator (only Ninja has been built).
