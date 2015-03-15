function premake.make.cpp.buildcommand(prj, objext, node)
    local extraFlags = ""
	local iscfile = node and path.iscfile(node.abspath) or false
	if not iscfile then
	  extraFlags = extraFlags .. " -std=c++11"
	end
	--if node.name ~= "Exception.cpp" then
	--  extraFlags = extraFlags .. " -fno-exceptions"
	--end
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

if #_ARGS ~= 1 then
  error("** Usage: premake5 premake4-gmake.lua [triple]", 0)
end

-- TODO: ARM, etc...
is64 = string.find(_ARGS[1], "x86_64") ~= nil

builddir = path.getabsolute("./" .. _OPTIONS["outdir"]);
libdir = path.join(builddir, "lib", "runtime", _ARGS[1]);

solution "SharpLang.Runtime"
  configurations { "Debug", "Release" }
  location (path.join(builddir, "runtime", _ARGS[1]))
  objdir (path.join(builddir, "runtime/obj", _ARGS[1]))
  targetdir (libdir)
  toolset "clang"
  project "SharpLang.Runtime"
	targetprefix ""
    kind "SharedLib"
    language "C++"
    flags { "Unsafe"}

    files { "../src/SharpLang.Runtime/**.cpp", "../src/SharpLang.Runtime/**.c", "../deps/libcxxabi/src/cxa_guard.cpp", "../deps/libcxxabi/src/abort_message.cpp", "../deps/compiler-rt/lib/builtins/mulodi4.c" }
    excludes { "obj/**" }
	
	targetextension ".bc"
	
	defines { "__STDC_CONSTANT_MACROS", "__STDC_LIMIT_MACROS", "LIBCXXABI_HAS_NO_THREADS", "UNICODE", "USE_STL", "_BLD_CLR", "FEATURE_CORECLR", "FEATURE_BCL_FORMATTING", "FEATURE_CRYPTO", "FEATURE_COREFX_GLOBALIZATION" }

	includedirs { "../deps/llvm/include", "../src/SharpLang.Runtime/coreclr/vm", "../src/SharpLang.Runtime/coreclr/inc", "../src/SharpLang.Runtime/coreclr/classlibnative/inc", "../src/SharpLang.Runtime/coreclr/classlibnative/cryptography" }
	buildoptions { "-emit-llvm -O3 -g0 --target=" .. _ARGS[1] }

	if is64 then
		includedirs { "../deps/llvm/build_x64/include" }
	else
		includedirs { "../deps/llvm/build_x32/include" }
	end
