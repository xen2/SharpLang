#ifndef SHARPLLVM_ADDITIONAL
#define SHARPLLVM_ADDITIONAL

#include <llvm-c/Core.h>

extern "C" LLVMValueRef LLVMBuildUnsignedIntCast(LLVMBuilderRef, LLVMValueRef Val, LLVMTypeRef DestTy, const char *Name);

extern "C" LLVMValueRef LLVMIntrinsicGetDeclaration(LLVMModuleRef M, unsigned int ID, LLVMTypeRef *ParamTypes, unsigned ParamCount);

#endif
