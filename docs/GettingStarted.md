# Getting started

## Compiling on Windows/Visual Studio

1. Clone SharpLang to `<SharpLang>` with submodules.
2. Run `<SharpLang>\deps\build_llvm_clang.bat`. This will build LLVM and Clang both in RelWithDebInfo and Debug mode with VS2013.
3. Run `<SharpLang>\build\GenerateProjects.bat`.
4. Open `<SharpLang>\build\vs2013\SharpLang.sln`.
5. Switch Active solution platform to *x86* in Build > Configuration Manager.
6. Build and play with tests.
