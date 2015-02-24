using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;
using SharpLang.Compiler.Utils;
using SharpLang.CompilerServices.Cecil;
using SharpLang.CompilerServices.Marshalling;
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

        public static string GetDefaultTriple()
        {
            return LLVM.GetDefaultTargetTriple().Replace("msvc", "gnu");
        }

        public static void CompileAssembly(string inputFile, string outputFile, string triple, bool generateIR = false, bool verifyModule = true, System.Type[] additionalTypes = null)
        {
            var assemblyDefinition = LoadAssembly(inputFile);

            // Generate marshalling code for PInvoke
            var mcg = new MarshalCodeGenerator(assemblyDefinition);
            mcg.Generate();
            //mcg.AssemblyDefinition.Write(Path.Combine(Path.GetDirectoryName(inputFile), Path.GetFileNameWithoutExtension(inputFile) + ".Marshalled.dll"), new WriterParameters {  });

            var compiler = new Compiler(triple);
            compiler.TestMode = additionalTypes != null;
            compiler.PrepareAssembly(assemblyDefinition);

            if (additionalTypes != null)
            {
                foreach (var type in additionalTypes)
                {
                    var assembly = assemblyDefinition.MainModule.AssemblyResolver.Resolve(type.Assembly.FullName);
                    var resolvedType = assembly.MainModule.GetType(type.FullName);
                    compiler.RegisterType(resolvedType);
                }
            }

            foreach (var type in assemblyDefinition.MainModule.Types)
            {
                foreach (var attribute in type.CustomAttributes)
                {
                    if (attribute.AttributeType.Name == "EmbedTestAttribute")
                    {
                        compiler.RegisterType((TypeReference)attribute.ConstructorArguments[0].Value);
                    }
                }
            }

            compiler.ProcessAssembly(assemblyDefinition);

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
            assemblyResolver.AddSearchDirectory(@"..\..\..\..\build\vs2013\lib\runtime.net\x86".Replace('\\', Path.DirectorySeparatorChar));
            return assemblyDefinition;
        }

        const string ClangWrapper = "vs_clang.bat";

        private static readonly bool UseMSVCToolChain = Environment.OSVersion.Platform == PlatformID.Win32NT && false;

        public static void ExecuteClang(string triple, string args)
        {
            if (triple == null)
                triple = GetDefaultTriple();

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
                var cpu = triple.Split('-').First();
                var archSize = cpu == "x86_64" ? 64 : 32;
                var hostArchSize = IntPtr.Size * 8;
                arguments.AppendFormat("--target={0} -g -D__STDC_CONSTANT_MACROS -D__STDC_LIMIT_MACROS", triple);
                arguments.AppendFormat(" -I../../../../deps/llvm/build_x{0}/include -I../../../../deps/llvm/include", hostArchSize);

                if (triple.Contains("windows-gnu") || triple.Contains("mingw"))
                {
                    var mingwTriple = string.Format("{0}-w64-mingw32", cpu);

                    var mingwFolder = string.Format("../../../../deps/mingw{0}/{1}", archSize, mingwTriple);
                    arguments.AppendFormat(" -I{0}/include -I{0}/include/c++ -I{0}/include/c++/{1}", mingwFolder, mingwTriple);
                    processStartInfo.EnvironmentVariables["PATH"] = string.Format(@"..\..\..\..\deps\mingw{0}\bin;", archSize) + processStartInfo.EnvironmentVariables["PATH"];
                }
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

        public static void LinkBitcodes(string triple, string outputFile, params string[] bitcodeFiles)
        {
            if (triple == null)
                triple = GetDefaultTriple();

            var filesToLink = new List<string>();
            filesToLink.Add(Compiler.LocateRuntimeModule(triple));

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

            // Necessary for some CoreCLR new/delete
            // Note: not sure yet if we want to keep stdc++ deps or not?
            arguments.Append(" -lstdc++");
            if (triple.Contains("windows"))
                arguments.Append(" -loleaut32");

            ExecuteClang(triple, arguments.ToString());
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