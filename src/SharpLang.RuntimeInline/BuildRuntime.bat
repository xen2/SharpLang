..\..\deps\llvm\build\RelWithDebInfo\bin\clang RuntimeType.cpp -emit-llvm -c -o RuntimeType.bc -O3 -I../../deps/llvm/build/include -I../../deps/llvm/include
..\..\deps\llvm\build\RelWithDebInfo\bin\clang Exception.cpp -emit-llvm -c -o Exception.bc -O3 -I../../deps/llvm/build/include -I../../deps/llvm/include
..\..\deps\llvm\build\RelWithDebInfo\bin\llvm-link RuntimeType.bc Exception.bc -o Runtime.bc
..\..\deps\llvm\build\RelWithDebInfo\bin\llc -march=cpp Runtime.bc -cppgen=contents -o RuntimeInline.inc