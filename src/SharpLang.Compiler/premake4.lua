project "SharpLang.Compiler"

  kind "ConsoleApp"
  language "C#"
  flags { "Unsafe"}

  SetupManagedProject()

  files { "**.cs" }
  excludes { "obj/**" }

  libdirs 
  {
    depsdir .. "/Mono.Options",
  }

  dependson { "SharpLang.Runtime" }

  links
  {
    "System",
    "System.Core",
    "SharpLLVM",
    "Mono.Cecil",
    "Mono.Cecil.Pdb",
    "Mono.Cecil.Rocks",
    "Mono.Options",
    "Mono.Linker",
    "System.Reflection.Metadata",
  }