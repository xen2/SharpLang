function SetupSWIGBindings()
  local c = configuration 'Runtime.i'
  
    -- A message to display while this build step is running (optional)
    buildmessage 'Generating bindings with SWIG for: %{file.name}'

    -- One or more commands to run (required)
    local prj = premake.api.scope.project.location
    local gen = path.getrelative(prj, path.getabsolute(gendir))
    local out = path.join(gen, '%{file.basename}_wrap.c')
    local llvm = path.getrelative(prj, path.getabsolute(path.join(LLVMRootDir, "include")))
    local swig = path.getrelative(prj, path.getabsolute(path.join(toolsdir, 'swig/swig')))

    local cmd = '"' .. swig .. '" -w302 -csharp -I' .. llvm
      .. " -namespace SharpLang.RuntimeInline -dllimport SharpLang.RuntimeInline.dll"
      .. " -outdir " .. gen .. " -o " .. out .. " %{file.relpath}"

    buildcommands { cmd }

    -- One or more outputs resulting from the build (required)
    buildoutputs { path.join(gendir, '%{file.basename}_wrap.c') }

  configuration(c)
end

project "SharpLang.RuntimeInline"

  kind "SharedLib"
  language "C++"

  SetupNativeProject()

  files { "*.h", "Runtime_wrap.cpp", "*.i", "*.inc" }
  
  SetupSWIGBindings()

  SetupLLVMIncludes()
  SetupLLVMLibDirs()

  links 
  {
    "LLVMCore",
    "LLVMSupport",
  }