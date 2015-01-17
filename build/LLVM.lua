-- Setup the LLVM dependency directories

LLVMRootDir = "../../deps/llvm/"

-- TODO: Search for available system dependencies

newoption
{
  trigger = "llvm",
  value = "path",
  description = "Path to the LLVM directory"
}

if _OPTIONS['llvm'] ~= nil then
  LLVMRootDir = _OPTIONS['llvm']
end

LLVMBuild32Dir = path.join(LLVMRootDir, "build_x32")
LLVMBuild64Dir = path.join(LLVMRootDir, "build_x64")

function SetupLLVMIncludes()
  local c = configuration()

  includedirs
  {
    path.join(LLVMRootDir, "include"),
    path.join(LLVMRootDir, "tools/clang/include"),    
    path.join(LLVMRootDir, "tools/clang/lib"),    
  }
  
  configuration { "x32" }
    includedirs
    {
      path.join(LLVMBuild32Dir, "include"),
      path.join(LLVMBuild32Dir, "tools/clang/include"),
	}

  configuration { "x64" }
    includedirs
    {
      path.join(LLVMBuild64Dir, "include"),
      path.join(LLVMBuild64Dir, "tools/clang/include"),
	}
	
  configuration(c)
end

function SetupLLVMLibDirs()
  local c = configuration()

  configuration { "x32" }
    libdirs { path.join(LLVMBuild32Dir, "lib") }
  configuration { "x64" }
    libdirs { path.join(LLVMBuild64Dir, "lib") }
	
  configuration { "x32", "Debug", "vs*" }
    libdirs { path.join(LLVMBuild32Dir, "Debug/lib") }
  configuration { "x64", "Debug", "vs*" }
    libdirs { path.join(LLVMBuild64Dir, "Debug/lib") }

  configuration { "x32", "Release", "vs*" }
    libdirs { path.join(LLVMBuild32Dir, "RelWithDebInfo/lib") }
  configuration { "x64", "Release", "vs*" }
    libdirs { path.join(LLVMBuild64Dir, "RelWithDebInfo/lib") }
	  
  configuration "not vs*"
    defines { "__STDC_CONSTANT_MACROS", "__STDC_LIMIT_MACROS" }

  configuration "macosx"
    links { "c++", "curses", "pthread", "z" }

  configuration(c)
end

function SetupLLVMLibs()
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
      "LLVMMCDisassembler",
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
      "LLVMCppBackendCodeGen",
      "LLVMCppBackendInfo",
    }
    
  configuration(c)
end


