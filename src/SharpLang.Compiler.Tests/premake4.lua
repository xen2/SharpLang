project "SharpLang.Compiler.Tests"

  kind "SharedLib"
  language "C#"
  flags { "Unsafe" }

  SetupManagedProject()

  files { "*.cs" }
  excludes { "obj/**" }

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
    depsdir .. "/NUnit/nunit.framework",
    "Mono.Linker"
  }