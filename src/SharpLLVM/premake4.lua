project "SharpLLVM"

  kind "SharedLib"
  language "C#"
  flags { "Unsafe"}

  SetupManagedProject()

  files { "*.cs", "**.i" }
  links
  {
    "System",
    "System.Core",
  }