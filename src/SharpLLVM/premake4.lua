project "SharpLLVM"

  kind "SharedLib"
  language "C#"
  flags { "Unsafe"}

  SetupManagedProject()

  files { "*.cs", "**.i" }

  dependson { "SharpLLVM.Native" }

  links
  {
    "System",
    "System.Core",
  }
