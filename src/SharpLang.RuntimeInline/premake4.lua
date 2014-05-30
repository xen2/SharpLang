project "SharpLLVM.RuntimeInline"

  kind "SharedLib"
  language "C++"

  SetupNativeProject()

  files { "*.h", "*_wrap.cpp", "*.i", "*.inc" }
  
  SetupLLVMIncludes()
  SetupLLVMLibs()