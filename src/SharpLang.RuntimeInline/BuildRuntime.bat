call "C:\Program Files (x86)\Microsoft Visual Studio 12.0\VC\vcvarsall.bat" x86
..\..\deps\llvm\build\RelWithDebInfo\bin\clang Runtime.c -emit-llvm -c -o Runtime.bc -O3
..\..\deps\llvm\build\RelWithDebInfo\bin\llc -march=cpp Runtime.bc -cppgen=functions -o RuntimeInline.inc