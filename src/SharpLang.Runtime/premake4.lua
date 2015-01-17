local defaultMakeOptions32 = 'CC=../../../deps/llvm/build_x32/RelWithDebInfo/bin/clang CXX=../../../deps/llvm/build_x32/RelWithDebInfo/bin/clang LLVM_LINK=../../../deps/llvm/build_x32/RelWithDebInfo/bin/llvm-link.exe'

local defaultMakeOptions64 = 'CC=../../../deps/llvm/build_x32/RelWithDebInfo/bin/clang CXX=../../../deps/llvm/build_x32/RelWithDebInfo/bin/clang LLVM_LINK=../../../deps/llvm/build_x32/RelWithDebInfo/bin/llvm-link.exe'

project "SharpLang.Runtime"
  kind "Makefile"
  language "C++"
  location (path.join(builddir, "runtime"))
  targetextension ".bc"
  files { "**.cpp", "**.c", "**.h" }

  configuration { "x32" }
    buildcommands { "..\\..\\..\\deps\\mingw32\\bin\\mingw32-make config=%{string.lower(cfg.buildcfg)}_%{cfg.platform} all " .. defaultMakeOptions32 }
    rebuildcommands { "..\\..\\..\\deps\\mingw32\\bin\\mingw32-make config=%{string.lower(cfg.buildcfg)}_%{cfg.platform} clean all " .. defaultMakeOptions32 }
    cleancommands { "..\\..\\..\\deps\\mingw32\\bin\\mingw32-make config=%{string.lower(cfg.buildcfg)}_%{cfg.platform} clean " .. defaultMakeOptions32 }
  configuration { "x64" }
    buildcommands { "..\\..\\..\\deps\\mingw32\\bin\\mingw32-make config=%{string.lower(cfg.buildcfg)}_%{cfg.platform} all " .. defaultMakeOptions64 }
    rebuildcommands { "..\\..\\..\\deps\\mingw32\\bin\\mingw32-make config=%{string.lower(cfg.buildcfg)}_%{cfg.platform} clean all " .. defaultMakeOptions64 }
    cleancommands { "..\\..\\..\\deps\\mingw32\\bin\\mingw32-make config=%{string.lower(cfg.buildcfg)}_%{cfg.platform} clean " .. defaultMakeOptions64 }
