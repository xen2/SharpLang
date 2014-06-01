-- Setup the LLVM dependency directories

LLVMRootDir = "../../deps/llvm/"

-- TODO: Search for available system dependencies

newoption
{
  trigger = "llvm",
  value = "path",
  description = "Path to the LLVM directory"
}

if _OPTIONS['llvm'] ~= "path" then
  LLVMRootDir = _OPTIONS['llvm']
end

LLVMBuildDir = path.join(LLVMRootDir, "build")

function SetupLLVMIncludes()
  local c = configuration()

  includedirs
  {
    path.join(LLVMRootDir, "include"),
    path.join(LLVMRootDir, "tools/clang/include"),    
    path.join(LLVMRootDir, "tools/clang/lib"),    
    path.join(LLVMBuildDir, "include"),
    path.join(LLVMBuildDir, "tools/clang/include"),
  }

  configuration(c)
end

function SetupLLVMLibs()
  local c = configuration()

  libdirs { path.join(LLVMBuildDir, "lib") }

  configuration { "Debug", "vs*" }
    libdirs { path.join(LLVMBuildDir, "Debug/lib") }

  configuration { "Release", "vs*" }
    libdirs { path.join(LLVMBuildDir, "RelWithDebInfo/lib") }

  configuration "not vs*"
    defines { "__STDC_CONSTANT_MACROS", "__STDC_LIMIT_MACROS" }

  configuration "macosx"
    links { "c++", "curses", "pthread", "z" }

  configuration "*"
    links
    {
      "LLVMAnalysis",
      "LLVMAsmParser",
      "LLVMAsmPrinter",
      "LLVMBitReader",
      "LLVMBitWriter",
      "LLVMCodeGen",
      "LLVMCore",
      "LLVMDebugInfo",
      "LLVMExecutionEngine",
      "LLVMipa",
      "LLVMipo",
      "LLVMInstCombine",
      "LLVMInstrumentation",
      "LLVMIRReader",
      "LLVMLinker",
      "LLVMMC",
      "LLVMMCParser",
      "LLVMObjCARCOpts",
      "LLVMObject",
      "LLVMOption",
      "LLVMScalarOpts",
      "LLVMSelectionDAG",
      "LLVMSupport",
      "LLVMTarget",
      "LLVMTransformUtils",
      "LLVMVectorize",
      "LLVMX86AsmParser",
      "LLVMX86AsmPrinter",
      "LLVMX86CodeGen",
      "LLVMX86Desc",
      "LLVMX86Disassembler",
      "LLVMX86Info",
      "LLVMX86Utils",
    }
    
  configuration(c)
end


