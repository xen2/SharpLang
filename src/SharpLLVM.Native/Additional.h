#ifndef SHARPLLVM_ADDITIONAL
#define SHARPLLVM_ADDITIONAL

#include <llvm-c/Core.h>

#ifdef __cplusplus
extern "C"
#endif
LLVMValueRef LLVMIntrinsicGetDeclaration(LLVMModuleRef M, unsigned int ID, LLVMTypeRef *ParamTypes, unsigned ParamCount);

#endif
