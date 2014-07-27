%module LLVM
%{

#include <llvm-c/Core.h>
#include <llvm-c/BitReader.h>
#include <llvm-c/BitWriter.h>
#include <llvm-c/Transforms/IPO.h>
#include <llvm-c/Transforms/PassManagerBuilder.h>
#include <llvm-c/Transforms/Scalar.h>
#include <llvm-c/Transforms/Vectorize.h>
#include <llvm-c/Target.h>
#include <llvm-c/TargetMachine.h>
#include <llvm-c/Analysis.h>

#include "Additional.h"
#include "DebugInfo.h"

%}

%csmethodmodifiers "public unsafe"

%rename("%(strip:[LLVM])s") "";

%include "LLVMCommon.i"

%ignore LLVMContextSetDiagnosticHandler;
%ignore LLVMContextSetYieldCallback;

typedef bool LLVMBool;
typedef unsigned char uint8_t;
typedef unsigned long long uint64_t;
typedef long long int64_t;


REF_CLASS(LLVMTargetRef, TargetRef)
REF_CLASS(LLVMMemoryBufferRef, MemoryBufferRef)
REF_CLASS(LLVMMemoryTargetRef, MemoryTargetRef)
REF_CLASS(LLVMBasicBlockRef, BasicBlockRef)
REF_CLASS(LLVMBuilderRef, BuilderRef)
REF_CLASS(LLVMContextRef, ContextRef)
REF_CLASS(LLVMModuleRef, ModuleRef)
REF_CLASS(LLVMModuleProviderRef, ModuleProviderRef)
REF_CLASS(LLVMTargetMachineRef, TargetMachineRef)
REF_CLASS(LLVMTypeRef, TypeRef)
REF_CLASS(LLVMPassManagerRef, PassManagerRef)
REF_CLASS(LLVMPassManagerBuilderRef, PassManagerBuilderRef)
REF_CLASS(LLVMPassRegistryRef, PassRegistryRef)
REF_CLASS(LLVMTargetDataRef, TargetDataRef)
REF_CLASS(LLVMTargetLibraryInfoRef, TargetLibraryInfoRef)
REF_CLASS(LLVMValueRef, ValueRef)
REF_CLASS(LLVMUseRef, UseRef)
REF_CLASS(LLVMDiagnosticInfoRef, DiagnosticInfoRef)
REF_CLASS(LLVMDIBuilderRef, DIBuilderRef)

%apply (LLVMTypeRef *ARRAY) {(LLVMTypeRef *Dest)};
%apply (LLVMTypeRef *ARRAY, unsigned ARRAYSIZE) {(LLVMTypeRef *ElementTypes, unsigned ElementCount)};
%apply (LLVMTypeRef *ARRAY, unsigned ARRAYSIZE) {(LLVMTypeRef *ParamTypes, unsigned ParamCount)};
%apply (LLVMValueRef *ARRAY, unsigned ARRAYSIZE) {(LLVMValueRef *Vals, unsigned Count)};
%apply (LLVMValueRef *ARRAY, unsigned ARRAYSIZE) {(LLVMValueRef *ConstantVals, unsigned Count)};
%apply (LLVMValueRef *ARRAY, unsigned ARRAYSIZE) {(LLVMValueRef *ConstantVals, unsigned Length)};
%apply (LLVMValueRef *ARRAY, unsigned ARRAYSIZE) {(LLVMValueRef *ScalarConstantVals, unsigned Size)};
%apply (LLVMValueRef *ARRAY, unsigned ARRAYSIZE) {(LLVMValueRef *ConstantIndices, unsigned NumIndices)};
%apply (LLVMValueRef *ARRAY, unsigned ARRAYSIZE) {(LLVMValueRef *Args, unsigned NumArgs)};
%apply (LLVMValueRef *ARRAY, unsigned ARRAYSIZE) {(LLVMValueRef *RetVals, unsigned N)};
%apply (LLVMValueRef *ARRAY, unsigned ARRAYSIZE) {(LLVMValueRef *Indices, unsigned NumIndices)};
%apply (LLVMValueRef *ARRAY, unsigned ARRAYSIZE) {(LLVMValueRef *Ptr, unsigned Count)};
%apply (LLVMValueRef *ARRAY, unsigned ARRAYSIZE) {(LLVMValueRef *AddrOps, unsigned AddrOpsCount)};
%apply (LLVMValueRef *ARRAY) {(LLVMValueRef *IncomingValues)};
%apply (LLVMBasicBlockRef *ARRAY, unsigned ARRAYSIZE) {(LLVMBasicBlockRef *IncomingBlocks, unsigned Count)};

%include "llvm-c/Core.h"
%include "llvm-c/BitReader.h"
%include "llvm-c/BitWriter.h"
%include "llvm-c/Transforms/IPO.h"
%include "llvm-c/Transforms/PassManagerBuilder.h"
%include "llvm-c/Transforms/Scalar.h"
%include "llvm-c/Transforms/Vectorize.h"
%include "llvm-c/Target.h"
%include "llvm-c/TargetMachine.h"
%include "llvm-c/Analysis.h"
%include "Additional.h"
%include "DebugInfo.h"
