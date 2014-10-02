project "SharpLLVM"

  kind "SharedLib"
  language "C#"
  flags { "Unsafe"}

  SetupManagedProject()

  files { "**.cs", "**.i", "**.tt" }
  excludes { "obj/**" }

  dependson { "SharpLLVM.Native" }

  links
  {
    "System",
    "System.Core",
  }
