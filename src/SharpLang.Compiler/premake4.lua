project "SharpLang.Compiler"

  kind "ConsoleApp"
  language "C#"
  flags { "Unsafe"}

  SetupManagedProject()

  files { "**.cs" }
  excludes { "obj/**" }

  dependson { "SharpLang.Runtime" }

  links
  {
    "System",
    "System.Core",
    "SharpLLVM",
    "Mono.Cecil",
    "Mono.Cecil.Pdb",
    "Mono.Cecil.Rocks",
    "Mono.Linker",
    depsdir .. "/Mono.Options/Mono.Options",
    depsdir .. "/System.Reflection.Metadata/System.Reflection.Metadata",
  }