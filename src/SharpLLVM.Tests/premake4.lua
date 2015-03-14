project "SharpLLVM.Tests"

  kind "SharedLib"
  language "C#"
  flags { "Unsafe"}

  SetupManagedProject()

  files { "**.cs" }
  excludes { "obj/**" }

  links
  {
    "System",
    "System.Core",
    "SharpLLVM",
    depsdir .. "/NUnit/nunit.framework",
  }