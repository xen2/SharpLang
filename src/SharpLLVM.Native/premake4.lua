function SetupSWIGBindings()
  local c = configuration '**/LLVM.i'
  
    -- A message to display while this build step is running (optional)
    buildmessage 'Generating bindings with SWIG for: %{file.name}'

    -- One or more commands to run (required)
    local prj = premake.api.scope.project.location
    local gen = path.getrelative(prj, path.getabsolute(gendir))
    local current = path.getrelative(prj, path.getabsolute("."))
    local out = path.join(current, '%{file.basename}_wrap.cpp')
    local outdir = path.getrelative(prj, path.getabsolute("../SharpLLVM"))
    local llvm = path.getrelative(prj, path.getabsolute(path.join(LLVMRootDir, "include")))
    local swig = path.getrelative(prj, path.getabsolute(path.join(toolsdir, 'swig/swig')))

    local cmd = '"' .. swig .. '" -w302 -csharp -I' .. llvm .. " -I" .. current
      .. " -namespace SharpLLVM -dllimport SharpLLVM.Native.dll"
      .. " -outdir " .. outdir .. " -o " .. out .. " %{file.relpath}"

    buildcommands { cmd }

    -- One or more outputs resulting from the build (required)
    buildoutputs { '%{file.basename}_wrap.cpp' }

  configuration(c)
end

project "SharpLLVM.Native"

  kind "SharedLib"
  language "C++"

  SetupNativeProject()

  includedirs { "." }
  files
  {
    "*.h",
    "*.cpp",
    "../SharpLLVM/Bindings/**.i"
  }

  SetupSWIGBindings()

  SetupLLVMIncludes()
  SetupLLVMLibDirs()
  SetupLLVMLibs()

