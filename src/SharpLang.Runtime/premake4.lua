local defaultMakeOptions32 = 'CC=../../../../deps/llvm/build_x32/RelWithDebInfo/bin/clang CXX=../../../../deps/llvm/build_x32/RelWithDebInfo/bin/clang LLVM_LINK=../../../../deps/llvm/build_x32/RelWithDebInfo/bin/llvm-link.exe CPPFLAGS="--target=i686-pc-windows-gnu -I../../../../deps/mingw32/i686-w64-mingw32/include -I../../../../deps/mingw32/i686-w64-mingw32/include/c++ -I../../../../deps/mingw32/i686-w64-mingw32/include/c++/i686-w64-mingw32"'

local defaultMakeOptions64 = 'CC=../../../../deps/llvm/build_x64/RelWithDebInfo/bin/clang CXX=../../../../deps/llvm/build_x64/RelWithDebInfo/bin/clang LLVM_LINK=../../../../deps/llvm/build_x64/RelWithDebInfo/bin/llvm-link.exe CPPFLAGS="--target=x86_64-pc-windows-gnu -I../../../../deps/mingw64/x86_64-w64-mingw32/include -I../../../../deps/mingw64/x86_64-w64-mingw32/include/c++ -I../../../../deps/mingw64/x86_64-w64-mingw32/include/c++/x86_64-w64-mingw32"'

project "SharpLang.Runtime"
  kind "Makefile"
  language "C++"
  location (path.join(builddir, "runtime"))
  targetextension ".bc"
  files { "**.cpp", "**.c", "**.h" }

  configuration { "x32" }
    buildcommands { "..\\..\\..\\deps\\mingw32\\bin\\mingw32-make -C i686-pc-windows-gnu config=%{string.lower(cfg.buildcfg)} all " .. defaultMakeOptions32 }
    rebuildcommands { "..\\..\\..\\deps\\mingw32\\bin\\mingw32-make -C i686-pc-windows-gnu config=%{string.lower(cfg.buildcfg)} clean all " .. defaultMakeOptions32 }
    cleancommands { "..\\..\\..\\deps\\mingw32\\bin\\mingw32-make -C i686-pc-windows-gnu config=%{string.lower(cfg.buildcfg)} clean " .. defaultMakeOptions32 }
  configuration { "x64" }
    buildcommands { "..\\..\\..\\deps\\mingw32\\bin\\mingw32-make -C x86_64-pc-windows-gnu config=%{string.lower(cfg.buildcfg)} all " .. defaultMakeOptions64 }
    rebuildcommands { "..\\..\\..\\deps\\mingw32\\bin\\mingw32-make -C x86_64-pc-windows-gnu config=%{string.lower(cfg.buildcfg)} clean all " .. defaultMakeOptions64 }
    cleancommands { "..\\..\\..\\deps\\mingw32\\bin\\mingw32-make -C x86_64-pc-windows-gnu config=%{string.lower(cfg.buildcfg)} clean " .. defaultMakeOptions64 }
