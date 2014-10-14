local defaultMakeOptions = 'CC=../../../deps/llvm/build/RelWithDebInfo/bin/clang CXX=../../../deps/llvm/build/RelWithDebInfo/bin/clang LLVM_LINK=../../../deps/llvm/build/RelWithDebInfo/bin/llvm-link.exe CFLAGS="-I../../../deps/llvm/include -I../../../deps/mingw32/i686-w64-mingw32/include -I../../../deps/mingw32/i686-w64-mingw32/include/c++ -I../../../deps/mingw32/i686-w64-mingw32/include/c++/i686-w64-mingw32"'

project "SharpLang.Runtime"
  kind "Makefile"
  language "C++"
  location (path.join(builddir, "runtime"))

  buildcommands { "..\\..\\..\\deps\\mingw32\\bin\\mingw32-make config=%{string.lower(cfg.buildcfg)}_%{cfg.platform} all " .. defaultMakeOptions }
  rebuildcommands { "..\\..\\..\\deps\\mingw32\\bin\\mingw32-make config=%{string.lower(cfg.buildcfg)}_%{cfg.platform} clean all " .. defaultMakeOptions }
  cleancommands { "..\\..\\..\\deps\\mingw32\\bin\\mingw32-make config=%{string.lower(cfg.buildcfg)}_%{cfg.platform} clean " .. defaultMakeOptions }
  targetextension ".bc"
  
  files { "**.cpp", "**.c", "**.h" }