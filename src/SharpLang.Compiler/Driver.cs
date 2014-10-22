using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;
using SharpLang.Compiler.Utils;
using SharpLang.CompilerServices.Cecil;
using SharpLang.Toolsets;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public class Driver
    {
        private static bool clangWrapperCreated = false;

        static Driver()
        {
            LLC = "llc";
            Clang = "clang++";
        }

        /// <summary>
        /// Gets or sets the path to LLC.
        /// </summary>
        /// <value>
        /// The path to LLC.
        /// </value>
        public static string LLC { get; set; }

        /// <summary>
        /// Gets or sets the path to C compiler.
        /// </summary>
        /// <value>
        /// The path to C compiler.
        /// </value>
        public static string Clang { get; set; }

        public static void CompileAssembly(string inputFile, string outputFile, bool generateIR = false, bool verifyModule = true, System.Type[] additionalTypes = null)
        {
            var assemblyDefinition = LoadAssembly(inputFile);

            var compiler = new Compiler();
            compiler.RegisterMainAssembly(assemblyDefinition);

            if (additionalTypes != null)
            {
                foreach (var type in additionalTypes)
                {
                    var assembly = assemblyDefinition.MainModule.AssemblyResolver.Resolve(type.Assembly.FullName);
                    var resolvedType = assembly.MainModule.GetType(type.FullName);
                    compiler.RegisterType(assemblyDefinition.MainModule.Import(resolvedType));
                }
            }

            var module = compiler.GenerateModule();

            if (generateIR)
            {
                var irFile = Path.ChangeExtension(outputFile, "ll");
                string message;
                if (LLVM.PrintModuleToFile(module, irFile, out message))
                {
                    throw new InvalidOperationException(message);
                }
            }

            if (verifyModule)
            {
                // Verify module
                string message;
                if (LLVM.VerifyModule(module, VerifierFailureAction.PrintMessageAction, out message))
                {
                    throw new InvalidOperationException(message);
                }
            }

            LLVM.WriteBitcodeToFile(module, outputFile);
        }

        private static AssemblyDefinition LoadAssembly(string inputFile)
        {
            // Force PdbReader to be referenced
            typeof (Mono.Cecil.Pdb.PdbReader).ToString();

            var assemblyResolver = new CustomAssemblyResolver();

            // Check if there is a PDB
            var readPdb = File.Exists(System.IO.Path.ChangeExtension(inputFile, "pdb"));

            var assemblyDefinition = AssemblyDefinition.ReadAssembly(inputFile,
                new ReaderParameters {AssemblyResolver = assemblyResolver, ReadSymbols = readPdb});

            // Register self to assembly resolver
            assemblyResolver.Register(assemblyDefinition);
            assemblyResolver.AddSearchDirectory(Path.GetDirectoryName(assemblyDefinition.MainModule.FullyQualifiedName));
            assemblyResolver.AddSearchDirectory(@"..\..\..\..\src\mcs\class\lib\net_4_5");
            return assemblyDefinition;
        }

        const string ClangWrapper = "vs_clang.bat";

        private static readonly bool UseMSVCToolChain = Environment.OSVersion.Platform == PlatformID.Win32NT && false;

        public static void ExecuteClang(string args)
        {
            var arguments = new StringBuilder();

            // On Windows, we need to have vcvars32 called before clang (so that it can find linker)
            //var isWindowsOS = Environment.OSVersion.Platform == PlatformID.Win32NT;
            var isWindowsOS = Environment.OSVersion.Platform == PlatformID.Win32NT;
            var useMSVCToolchain = isWindowsOS && false; // currently disabled until we can improve exception support (libunwind, SEH, etc...)

            var processStartInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                FileName = useMSVCToolchain ? ClangWrapper : Clang,
            };

            if (UseMSVCToolChain)
            {
                // On Windows, we need to have vcvars32 called before clang (so that it can find linker)
                CreateCompilerWrapper();
            }
            else
            {
                // Add mingw32 paths
                arguments.AppendFormat("--target=i686-w64-mingw32 -g");
                arguments.AppendFormat(" -I../../../../deps/llvm/build/include -I../../../../deps/llvm/include");
                arguments.AppendFormat(" -I../../../../deps/mingw32/i686-w64-mingw32/include -I../../../../deps/mingw32/i686-w64-mingw32/include/c++ -I../../../../deps/mingw32/i686-w64-mingw32/include/c++/i686-w64-mingw32 -D__STDC_CONSTANT_MACROS -D__STDC_LIMIT_MACROS");
                processStartInfo.EnvironmentVariables["PATH"] += @";..\..\..\..\deps\mingw32\bin";
            }

            arguments.Append(' ').Append(args);

            processStartInfo.Arguments = arguments.ToString();

            string processLinkerOutput;
            var processLinker = Utils.ExecuteAndCaptureOutput(processStartInfo, out processLinkerOutput);
            processLinker.WaitForExit();

            if (processLinker.ExitCode != 0)
                throw new InvalidOperationException(string.Format("Error executing clang: {0}",
                    processLinkerOutput));
        }

        public static void LinkBitcodes(string outputFile, params string[] bitcodeFiles)
        {
            var filesToLink = new List<string>();
            filesToLink.Add(@"SharpLang.Runtime.bc");

            var arguments = new StringBuilder();
            
            if (UseMSVCToolChain)
            {
                GenerateObjects(bitcodeFiles);
                filesToLink.AddRange(bitcodeFiles.Select(x => Path.ChangeExtension(x, "obj")));
            }
            else
            {
                filesToLink.AddRange(bitcodeFiles);
            }

            //arguments.Append(" --driver-mode=g++ -std=c++11");
            arguments.AppendFormat(" {0} -o {1}", string.Join(" ", filesToLink), outputFile);

            ExecuteClang(arguments.ToString());
        }

        private static void CreateCompilerWrapper()
        {
            if (clangWrapperCreated)
                return;

            string vsDir;
            if (!MSVCToolchain.GetVisualStudioDir(out vsDir))
                throw new Exception("Could not find Visual Studio on the system");

            var vcvars = Path.Combine(vsDir, @"VC\vcvarsall.bat");
            if (!File.Exists(vcvars))
                throw new Exception("Could not find vcvarsall.bat on the system");

            File.WriteAllLines(ClangWrapper, new[]
                    {
                        string.Format("call \"{0}\" x86", Path.Combine(vsDir, "VC", "vcvarsall.bat")),
                        string.Format("{0} %*", Clang)
                    });

            clangWrapperCreated = true;
        }

        private static void GenerateObjects(IEnumerable<string> bitcodeFiles)
        {
            var processStartInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            };

            // Compile each LLVM .bc file to a .obj file with llc
            foreach (var bitcodeFile in bitcodeFiles)
            {
                processStartInfo.FileName = LLC;

                var objFile = Path.ChangeExtension(bitcodeFile, "obj");
                processStartInfo.Arguments = string.Format("-filetype=obj {0} -o {1}", bitcodeFile, objFile);

                string processLLCOutput;
                var processLLC = Utils.ExecuteAndCaptureOutput(processStartInfo, out processLLCOutput);
                if (processLLC.ExitCode != 0)
                {
                    throw new InvalidOperationException(string.Format("Error executing llc: {0}", processLLCOutput));
                }
            }
        }
    }
}