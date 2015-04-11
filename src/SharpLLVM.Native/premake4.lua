function SetupSWIGBindings()
  local c = configuration '**/LLVM.i'
  
    -- One or more commands to run (required)
    local prj = premake.api.scope.project.location
	
	-- Check swig existence before actually adding this command (otherwise we generate a project file which doesn't generate wrapper
    if os.isdir(path.getabsolute(path.join(toolsdir, 'swig'))) then
      -- A message to display while this build step is running (optional)
      buildmessage 'Generating bindings with SWIG for: %{file.name}'

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
    end


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

