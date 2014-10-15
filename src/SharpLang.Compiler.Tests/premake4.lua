project "SharpLang.Compiler.Tests"

  kind "SharedLib"
  language "C#"
  flags { "Unsafe" }

  SetupManagedProject()

  files { "*.cs" }
  excludes { "obj/**" }

  libdirs 
  {
    depsdir .. "/NUnit",
  }

  dependson { "corlib-net_4_5" }
  
  links
  {
    "System",
    "System.Core",
    "System.Data",
    "System.Data.DataSetExtensions",
    "System.Xml",
    "System.Xml.Linq",
    "SharpLang.Compiler",
    "SharpLLVM",
    "Microsoft.CSharp",
    "nunit.framework",
    "Mono.Linker"
  }