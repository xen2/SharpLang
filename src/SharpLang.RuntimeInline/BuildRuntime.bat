call "%VS120COMNTOOLS%..\..\VC\vcvarsall.bat" x86
..\..\deps\llvm\build\RelWithDebInfo\bin\clang Runtime.cpp -emit-llvm -c -o Runtime.bc -O3
..\..\deps\llvm\build\RelWithDebInfo\bin\llc -march=cpp Runtime.bc -cppgen=contents -o RuntimeInline.inc