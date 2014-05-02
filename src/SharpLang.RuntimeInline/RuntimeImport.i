%module Runtime
%{
#include "llvm/IR/Constants.h"
#include "llvm/IR/Module.h"
#include "llvm/IR/Instructions.h"

using namespace llvm;
#include "RuntimeInline.inc"
%}

//typedef Module* LLVMModule;
//typedef Function* LLVMFunction;

%define REF_CLASS_CPP(TYPE, CSTYPE)
    typedef struct TYPE { } TYPE;
    %typemap(cstype) TYPE* "CSTYPE"
    %typemap(csin) TYPE* "$csinput.Value"
    %typemap(imtype) TYPE* "System.IntPtr"
    %typemap(csout, excode=SWIGEXCODE) TYPE* {
      CSTYPE ret = new CSTYPE($imcall);$excode
      return ret;
    }
%enddef

REF_CLASS_CPP(Module, ModuleRef)
REF_CLASS_CPP(Function, ValueRef)
