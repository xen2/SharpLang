#include "Additional.h"

#include <llvm/IR/Intrinsics.h>
#include <llvm/IR/Module.h>
#include <llvm/ADT/ArrayRef.h>
#include <llvm-c/Core.h>

using namespace llvm;

extern "C" LLVMValueRef LLVMIntrinsicGetDeclaration(LLVMModuleRef M, unsigned int ID, LLVMTypeRef *ParamTypes, unsigned ParamCount)
{
	return wrap(Intrinsic::getDeclaration(unwrap(M), (Intrinsic::ID)ID, makeArrayRef(unwrap(ParamTypes), ParamCount)));
}