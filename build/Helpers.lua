action = _ACTION or ""

depsdir = path.getabsolute("../deps");
srcdir = path.getabsolute("../src");
incdir = path.getabsolute("../include");
bindir = path.getabsolute("../bin");
examplesdir = path.getabsolute("../examples");
testsdir = path.getabsolute("../tests");
toolsdir = path.getabsolute("../tools");

builddir = path.getabsolute("./" .. action);
if _ARGS[1] then
    builddir = path.getabsolute("./" .. _ARGS[1]);
end

libdir = path.join(builddir, "lib", "%{cfg.buildcfg}_%{cfg.platform}");
gendir = path.join(builddir, "gen");

common_flags = { "Unicode", "Symbols" }
msvc_buildflags = { }

msvc_cpp_defines = { }

function SetupNativeProject()
  location (path.join(builddir, "projects"))

  local c = configuration "Debug"
    defines { "DEBUG" }
    
  configuration "Release"
    defines { "NDEBUG" }
    optimize "On"
    
  -- Compiler-specific options
  
  configuration "vs*"
    buildoptions { msvc_buildflags }
    defines { msvc_cpp_defines }

  configuration { "gmake" }
    buildoptions { gcc_buildflags }
    
  local lang = premake.api.scope.project.language
  if lang == "C++" then
    configuration { "macosx" }
      buildoptions { "-std=c++11", "-stdlib=libc++" }
      links { "c++" }
  end
  
  -- OS-specific options
  
  configuration "Windows"
    defines { "WIN32", "_WINDOWS" }
  
  configuration(c)
end

function SetupManagedProject()
  language "C#"
  location (path.join(builddir, "projects"))

  if not os.is("macosx") then
    local c = configuration { "vs*" }
      location "."
    configuration(c)
  end
end

function IncludeDir(dir)
  local deps = os.matchdirs(dir .. "/*")
  
  for i,dep in ipairs(deps) do
    local fp = path.join(dep, "premake4.lua")
    fp = path.join(os.getcwd(), fp)
    
    if os.isfile(fp) then
      print(string.format(" including %s", dep))
      include(dep)
    end
  end
end
