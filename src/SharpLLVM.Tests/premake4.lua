project "SharpLLVM.Tests"

  kind "SharedLib"
  language "C#"
  flags { "Unsafe"}

  SetupManagedProject()

  files { "**.cs" }

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