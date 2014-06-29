# Getting started

## Compiling on Windows/Visual Studio

1. Clone SharpLang to `<SharpLang>` with submodules.
2. Download `http://sourceforge.net/projects/mingw-w64/files/Toolchains%20targetting%20Win32/Personal%20Builds/mingw-builds/4.9.0/threads-win32/dwarf/i686-4.9.0-release-win32-dwarf-rt_v3-rev2.7z/download` and extract it in `<SharpLang>\deps`.
3. Run `<SharpLang>\deps\build_llvm_clang.bat`. This will build LLVM and Clang both in RelWithDebInfo and Debug mode with VS2013.
4. Run `<SharpLang>\build\GenerateProjects.bat`.
5. Open `<SharpLang>\build\vs2013\SharpLang.sln`.
6. Switch Active solution platform to *x86* in Build > Configuration Manager.
7. Build and play with tests.
