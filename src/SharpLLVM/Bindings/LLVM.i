%module LLVM
%{

#include <stdbool.h>
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

%}

%csmethodmodifiers "public unsafe"

%rename("%(strip:[LLVM])s") "";

%define REF_CLASS(TYPE, CSTYPE...)
    typedef struct TYPE { } TYPE;
    %typemap(cstype) TYPE* "out $csclassname"
    %typemap(csin) TYPE* "out $csinput.Value"
	%typemap(csin) TYPE "$csinput.Value"
	%typemap(imtype) TYPE "System.IntPtr"
    %typemap(imtype) TYPE* "out System.IntPtr"

    %typemap(in) (TYPE *ARRAY, unsigned ARRAYSIZE) "$1 = $1_data; $2 = $input;"
    %typemap(ctype) (TYPE *ARRAY, unsigned ARRAYSIZE) "void* $1_data, unsigned int"
    %typemap(csin, pre="    fixed (CSTYPE* swig_ptrTo_$csinput = $csinput)")
                     (TYPE *ARRAY, unsigned ARRAYSIZE) "(System.IntPtr)swig_ptrTo_$csinput, (uint)$csinput.Length"
    %typemap(imtype) (TYPE *ARRAY, unsigned ARRAYSIZE) "System.IntPtr $1_data, uint"
    %typemap(cstype) (TYPE *ARRAY, unsigned ARRAYSIZE) "CSTYPE[]"
%enddef

%nodefault;
%typemap(out) SWIGTYPE %{ $result = $1; %}
%typemap(in) SWIGTYPE %{ $1 = ($1_ltype)$input; %}
%typemap(csinterfaces) SWIGTYPE ""
%typemap(csclassmodifiers) SWIGTYPE "public struct"
%typemap(csbody) SWIGTYPE %{
    public $csclassname(global::System.IntPtr cPtr) {
        Value = cPtr;
    }

    public System.IntPtr Value; %}
%typemap(csout, excode=SWIGEXCODE) SWIGTYPE {
    $&csclassname ret = new $&csclassname($imcall);$excode
    return ret;
  }
%typemap(csdestruct) SWIGTYPE;
%typemap(csfinalize) SWIGTYPE;


%typemap(cstype) char** "out string"
%typemap(csin) char** "out $csinput"
%typemap(imtype) char** "out string"

typedef bool LLVMBool;
typedef unsigned char uint8_t;
typedef unsigned long long uint64_t;


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

%apply (LLVMTypeRef *ARRAY, unsigned ARRAYSIZE) {(LLVMTypeRef *ElementTypes, unsigned ElementCount)};
%apply (LLVMTypeRef *ARRAY, unsigned ARRAYSIZE) {(LLVMTypeRef *ParamTypes, unsigned ParamCount)};
%apply (LLVMValueRef *ARRAY, unsigned ARRAYSIZE) {(LLVMValueRef *ConstantVals, unsigned Count)};
%apply (LLVMValueRef *ARRAY, unsigned ARRAYSIZE) {(LLVMValueRef *ConstantIndices, unsigned NumIndices)};
%apply (LLVMValueRef *ARRAY, unsigned ARRAYSIZE) {(LLVMValueRef *Args, unsigned NumArgs)};

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