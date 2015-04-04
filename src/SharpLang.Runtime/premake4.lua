local cmakePrepare32 = 'if not exist i686-pc-windows-gnu mkdir i686-pc-windows-gnu\r\ncd i686-pc-windows-gnu\r\ncmake ..\\..\\..\\.. -G "MinGW Makefiles"\r\n'

local cmakePrepare64 = 'if not exist x86_64-pc-windows-gnu mkdir x86_64-pc-windows-gnu\r\ncd x86_64-pc-windows-gnu\r\ncmake ..\\..\\..\\.. -G "MinGW Makefiles"\r\n'


project "SharpLang.Runtime"
  kind "Makefile"
  language "C++"
  location (path.join(builddir, "runtime"))
  targetextension ".bc"
  files { "**.cpp", "**.c", "**.h" }

  configuration { "x32" }
    buildcommands { cmakePrepare32 .. "..\\..\\..\\..\\deps\\mingw32\\bin\\mingw32-make all " }
    rebuildcommands { cmakePrepare32 .. "..\\..\\..\\..\\deps\\mingw32\\bin\\mingw32-make clean all " }
    cleancommands { cmakePrepare32 .. "..\\..\\..\\..\\deps\\mingw32\\bin\\mingw32-make clean " }
  configuration { "x64" }
    buildcommands { cmakePrepare64 .. "..\\..\\..\\..\\deps\\mingw64\\bin\\mingw32-make all " }
    rebuildcommands { cmakePrepare64 .. "..\\..\\..\\..\\deps\\mingw64\\bin\\mingw32-make clean all " }
    cleancommands { cmakePrepare64 .. "..\\..\\..\\..\\deps\\mingw64\\bin\\mingw32-make clean " }
