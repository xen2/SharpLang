call "%VS110COMNTOOLS%..\..\VC\vcvarsall.bat" x86

mkdir llvm\build
pushd llvm\build
cmake .. -DLLVM_TARGETS_TO_BUILD=X86;CppBackend -DLLVM_INCLUDE_TESTS=OFF -DLLVM_INCLUDE_EXAMPLES=OFF -DLLVM_EXTERNAL_CLANG_SOURCE_DIR=..\..\clang -DCLANG_INCLUDE_TESTS=OFF
msbuild LLVM.sln /p:Configuration=RelWithDebInfo
msbuild LLVM.sln /p:Configuration=Debug
popd