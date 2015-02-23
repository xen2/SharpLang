-- This is the starting point of the build scripts for the project.
-- It defines the common build settings that all the projects share
-- and calls the build scripts of all the sub-projects.

config = {}

dofile "Helpers.lua"
--dofile "Tests.lua"
dofile "LLVM.lua"

if not os.is("windows") then
  target_triplet = os.capture("gcc -dumpmachine"):gsub("[\r\n]+", "")
end

solution "SharpLang"

  configurations { "Debug", "Release" }
  platforms { "x32", "x64" }
  flags { common_flags }

  location (builddir)
  objdir (path.join(builddir, "obj"))
  targetdir (libdir)
  debugdir (bindir)

  framework "4.5"
  
  configuration "windows"
    defines { "WINDOWS" }

  configuration {}
    
  group "Libraries"
    include (srcdir .. "/SharpLang.Compiler")
    include (srcdir .. "/SharpLang.Compiler.Tests")

    if os.is("windows") then
      -- Windows: Fake .vcxproj that will execute make on runtime
      configuration "windows"
        include (srcdir .. "/SharpLang.Runtime")
    else
      -- Linux: call make recursively on runtime
      external "SharpLang.Runtime"
        location ("gmake/runtime/" .. target_triplet)
        kind "SharedLib"
        removeplatforms "*"
        configmap {
          ["Release"] = "Release",
          ["Debug"] = "Debug",
          ["x32"] = "",
          ["x64"] = "",
        }
    end

    include (srcdir .. "/SharpLLVM")
    include (srcdir .. "/SharpLLVM.Native")
    include (srcdir .. "/SharpLLVM.Tests")

  group "Cecil"
    external "Mono.Cecil"
      location (depsdir .. "/Mono.Cecil")
      uuid "D68133BD-1E63-496E-9EDE-4FBDBF77B486"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["Release"] = "net_4_0_Release",
          ["Debug"] = "net_4_0_Debug",
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "Mono.Cecil.Pdb"
      location (depsdir .. "/Mono.Cecil/symbols/pdb")
      uuid "63E6915C-7EA4-4D76-AB28-0D7191EEA626"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["Release"] = "net_4_0_Release",
          ["Debug"] = "net_4_0_Debug",
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "Mono.Cecil.Rocks"
      location (depsdir .. "/Mono.Cecil/rocks")
      uuid "FBC6DD59-D09D-499C-B03C-99C1C78FF2AC"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["Release"] = "net_4_0_Release",
          ["Debug"] = "net_4_0_Debug",
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }

  group "Tools"
    external "Mono.Linker"
      location (srcdir .. "/mcs/tools/linker")
      uuid "DD28E2B1-057B-4B4D-A04D-B2EBD9E76E46"
      kind "ConsoleApp"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
