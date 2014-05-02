%module Runtime
%{
#include "llvm/IR/Constants.h"
#include "llvm/IR/Module.h"
#include "llvm/IR/Instructions.h"

using namespace llvm;
#include "RuntimeInline.inc"
%}

%import "RuntimeImport.i"

%pragma(csharp) moduleimports=%{
using SharpLLVM;
%}

%include "RuntimeInline.inc"