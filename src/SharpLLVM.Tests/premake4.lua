project "SharpLLVM.Tests"

  kind "SharedLib"
  language "C#"
  flags { "Unsafe"}

  SetupManagedProject()

  files { "**.cs" }
  excludes { "obj/**" }

  libdirs 
  {
    depsdir .. "/NUnit",
  }
    
  links
  {
    "System",
    "System.Core",
    "SharpLLVM",
    "nunit.framework"
  }