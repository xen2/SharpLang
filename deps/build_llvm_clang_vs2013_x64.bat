call "%VS120COMNTOOLS%..\..\VC\vcvarsall.bat" x64

mkdir llvm\build_x64
pushd llvm\build_x64
cmake .. -G "Visual Studio 12 Win64" -DLLVM_TARGETS_TO_BUILD=X86;CppBackend -DLLVM_INCLUDE_TESTS=OFF -DLLVM_INCLUDE_EXAMPLES=OFF -DLLVM_EXTERNAL_CLANG_SOURCE_DIR=..\..\clang -DCLANG_INCLUDE_TESTS=OFF -DCLANG_ENABLE_ARCMT=OFF -DCLANG_ENABLE_STATIC_ANALYZER=OFF
msbuild LLVM.sln /p:Configuration=RelWithDebInfo
msbuild LLVM.sln /p:Configuration=Debug
popd