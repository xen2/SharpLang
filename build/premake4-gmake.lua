function premake.make.cpp.buildcommand(prj, objext, node)
    local extraFlags = ""
	local iscfile = node and path.iscfile(node.abspath) or false
	if not iscfile then
	  extraFlags = extraFlags .. " -std=c++11"
	end
	if node.name ~= "Exception.cpp" then
	  extraFlags = extraFlags .. " -fno-exceptions"
	end
	local flags = iif(prj.language == "C" or iscfile, '$(CC) $(ALL_CFLAGS)', '$(CXX) $(ALL_CXXFLAGS)')
	_p('\t$(SILENT) %s $(FORCE_INCLUDE) %s -o "$@" -MF $(@:%%.%s=%%.d) -c "$<"', flags, extraFlags, objext)
end

local cppTools_old = premake.make.cppTools;
function premake.make.cppTools(cfg, toolset)
	cppTools_old(cfg, toolset)
	_p('  LLVM_LINK = llvm-link')
end

function premake.make.linkCmd(cfg, toolset)
	_p('  LINKCMD = $(LLVM_LINK) -o $(TARGET) $(OBJECTS)')
end

newoption {
   trigger     = "outdir",
   value       = "path",
   description = "Output directory for the project"
}

dofile "Helpers.lua"

builddir = path.getabsolute("./" .. _OPTIONS["outdir"]);
libdir = path.join(builddir, "lib", "%{cfg.buildcfg}_%{cfg.platform}");

solution "SharpLang.Runtime"
  configurations { "debug", "release" }
  platforms { "x32", "x64" }
  location (path.join(builddir, "runtime"))
  objdir (path.join(builddir, "runtime/obj"))
  targetdir (libdir)
  toolset "clang"
  project "SharpLang.Runtime"
	
    kind "SharedLib"
    language "C++"
    flags { "Unsafe"}

    files { "../src/SharpLang.Runtime/**.cpp", "../src/SharpLang.Runtime/**.c", "../deps/libcxxabi/src/cxa_guard.cpp", "../deps/libcxxabi/src/abort_message.cpp", "../deps/compiler-rt/lib/builtins/mulodi4.c" }
    excludes { "obj/**" }
	
	targetextension ".bc"
	
	defines { "__STDC_CONSTANT_MACROS", "__STDC_LIMIT_MACROS", "LIBCXXABI_HAS_NO_THREADS" }

	includedirs { "../deps/llvm/include" }
    buildoptions { "-emit-llvm -O3" }

	configuration { "x32" }
		buildoptions { "--target=i686-pc-mingw32" }
		includedirs { "../deps/llvm/build_x32/include", "../deps/mingw32/i686-w64-mingw32/include", "../deps/mingw32/i686-w64-mingw32/include/c++", "../deps/mingw32/i686-w64-mingw32/include/c++/i686-w64-mingw32" }

	configuration { "x64" }
		buildoptions { "--target=x86_64-pc-mingw32" }
		includedirs { "../deps/llvm/build_x64/include", "../deps/mingw32/x86_64-w64-mingw32/include", "../deps/mingw32/x86_64-w64-mingw32/include/c++", "../deps/mingw32/x86_64-w64-mingw32/include/c++/x86_64-w64-mingw32" }
