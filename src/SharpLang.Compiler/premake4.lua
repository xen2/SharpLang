project "SharpLang.Compiler"

  kind "ConsoleApp"
  language "C#"
  flags { "Unsafe"}

  SetupManagedProject()

  files { "**.cs" }
  excludes { "obj/**" }

  libdirs 
  {
    depsdir .. "/Mono.Cecil",
    depsdir .. "/Mono.Options",
  }

  links
  {
    "System",
    "System.Core",
    "SharpLLVM",
    "Mono.Cecil",
    "Mono.Cecil.Pdb",
    "Mono.Cecil.Rocks",
    "Mono.Options",
  }