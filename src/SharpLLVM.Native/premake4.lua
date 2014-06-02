project "SharpLLVM.Native"

  kind "SharedLib"
  language "C++"

  SetupNativeProject()

  includedirs { "." }
  files { "**.c" }

  SetupLLVMIncludes()
  SetupLLVMLibDirs()
  SetupLLVMLibs()