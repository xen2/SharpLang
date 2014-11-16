-- This is the starting point of the build scripts for the project.
-- It defines the common build settings that all the projects share
-- and calls the build scripts of all the sub-projects.

config = {}

dofile "Helpers.lua"
--dofile "Tests.lua"
dofile "LLVM.lua"

solution "SharpLang"

  configurations { "Debug", "Release" }
  platforms { "x32", "x64" }
  flags { common_flags }

  location (builddir)
  objdir (path.join(builddir, "obj"))
  targetdir (libdir)
  debugdir (bindir)

  framework "4.5"
  
  configuration "windows"
    defines { "WINDOWS" }

  configuration {}
    
  group "Libraries"
    include (srcdir .. "/SharpLang.Compiler")
    include (srcdir .. "/SharpLang.Compiler.Tests")
    include (srcdir .. "/SharpLang.Runtime")

    include (srcdir .. "/SharpLLVM")
    include (srcdir .. "/SharpLLVM.Native")
    include (srcdir .. "/SharpLLVM.Tests")

  group "Cecil"
    external "Mono.Cecil"
      location (depsdir .. "/Mono.Cecil")
      uuid "D68133BD-1E63-496E-9EDE-4FBDBF77B486"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["Release"] = "net_4_0_Release",
          ["Debug"] = "net_4_0_Debug",
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "Mono.Cecil.Pdb"
      location (depsdir .. "/Mono.Cecil/symbols/pdb")
      uuid "63E6915C-7EA4-4D76-AB28-0D7191EEA626"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["Release"] = "net_4_0_Release",
          ["Debug"] = "net_4_0_Debug",
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "Mono.Cecil.Rocks"
      location (depsdir .. "/Mono.Cecil/rocks")
      uuid "FBC6DD59-D09D-499C-B03C-99C1C78FF2AC"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["Release"] = "net_4_0_Release",
          ["Debug"] = "net_4_0_Debug",
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }

  group "corefx"
    external "System.Collections.Immutable"
      location (srcdir .. "/corefx/System.Collections.Immutable")
      uuid "1DD0FF15-6234-4BD6-850A-317F05479554"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "System.Reflection.Metadata"
      location (srcdir .. "/corefx/System.Reflection.Metadata")
      uuid "F3E433C8-352F-4944-BF7F-765CE435370D"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }

  group "Tools"
    external "Mono.Linker"
      location (srcdir .. "/mcs/tools/linker")
      uuid "DD28E2B1-057B-4B4D-A04D-B2EBD9E76E46"
      kind "ConsoleApp"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }

  group "Class Libraries"
    external "jay"
        location (srcdir .. "/mcs/jay")
        uuid "5D485D32-3B9F-4287-AB24-C8DA5B89F537"
        kind "ConsoleApp"
        language "C++"
    external "corlib-net_4_5"
      location (srcdir .. "/mcs/class/corlib")
      uuid "33BF0182-AC5C-464C-995B-C9CFE74E1A95"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "System-bare-net_4_5"
      location (srcdir .. "/mcs/class/System")
      uuid "91CDF14E-F60F-4AB7-BC9D-5CBD7E669076"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "System.Xml-bare-net_4_5"
      location (srcdir .. "/mcs/class/System.XML")
      uuid "21FB091E-0F84-479E-AB16-6503D36852F9"
      kind "SharedLib"
      language "C#"
      dependson { "jay" }
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "Mono.Security-net_4_5"
      location (srcdir .. "/mcs/class/Mono.Security")
      uuid "D68D4FED-CA32-4800-A628-58BAE485562C"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "System-secxml-net_4_5"
      location (srcdir .. "/mcs/class/System")
      uuid "46F151F2-A422-4A1B-9D29-2E148CE73629"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "System.Security-net_4_5"
      location (srcdir .. "/mcs/class/System.Security")
      uuid "B55E59B2-31CA-438B-ADB8-4B9A9A547830"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "System.Configuration-net_4_5"
      location (srcdir .. "/mcs/class/System.Configuration")
      uuid "D52A2CEA-245E-4877-950E-3EAC5F541B8E"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "System-net_4_5"
      location (srcdir .. "/mcs/class/System")
      uuid "FFAC0B73-D997-493B-9C62-27656CD858BB"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "Mono.Posix-net_4_5"
      location (srcdir .. "/mcs/class/Mono.Posix")
      uuid "85A9B29B-58FF-4FBE-8998-B0A89AC22880"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "System.Core-net_4_5"
      location (srcdir .. "/mcs/class/System.Core")
      uuid "D92530F4-3F2B-4351-BD21-9D6BEE6D2A4E"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    --[[external "System.Xml-net_4_5"
      location (srcdir .. "/mcs/class/System.XML")
      uuid "2951DEC2-4393-4A78-83F9-42EB39CB708F"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "System.Drawing-net_4_5"
      location (srcdir .. "/mcs/class/System.Drawing")
      uuid "CB1CB976-AA54-4553-B0E7-05665B199EC4"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "Accessibility-net_4_5"
      location (srcdir .. "/mcs/class/Accessibility")
      uuid "FACE8136-C95A-4788-90B2-DB0ECF7A020A"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "System.Transactions-net_4_5"
      location (srcdir .. "/mcs/class/System.Transactions")
      uuid "AF2BBF50-AB57-4CA1-8EF5-2B54C7418434"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "System.Data-net_4_5"
      location (srcdir .. "/mcs/class/System.Data")
      uuid "9A33954F-57A8-4D75-B1D8-0F81808A0DD4"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "System.EnterpriseServices-net_4_5"
      location (srcdir .. "/mcs/class/System.EnterpriseServices")
      uuid "773D8BA7-9A88-49AC-81C9-740436270588"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "Mono.Data.Tds-net_4_5"
      location (srcdir .. "/mcs/class/Mono.Data.Tds")
      uuid "CB7949AC-5FA7-4FAB-893A-8FAF436BF514"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "Mono.WebBrowser-net_4_5"
      location (srcdir .. "/mcs/class/Mono.WebBrowser")
      uuid "1D660912-8164-4499-A2D7-A3B2FE742E17"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "System.Runtime.Serialization.Formatters.Soap-net_4_5"
      location (srcdir .. "/mcs/class/System.Runtime.Serialization.Formatters.Soap")
      uuid "B12AABBC-30D1-4885-BF3F-A53B970F68FB"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }
    external "System.Windows.Forms-net_4_5"
      location (srcdir .. "/mcs/class/Managed.Windows.Forms")
      uuid "B102F27C-1A07-487F-BA5B-D5155D201112"
      kind "SharedLib"
      language "C#"
      removeplatforms "*"
      platforms { "Any CPU" }
      configmap {
          ["x32"] = "Any CPU",
          ["x64"] = "Any CPU"
      }]]
