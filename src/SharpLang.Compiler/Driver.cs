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
            Clang = "clang";
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

        public static void CompileAssembly(string inputFile, string outputFile, bool generateIR = false)
        {
            // Force PdbReader to be referenced
            typeof(Mono.Cecil.Pdb.PdbReader).ToString();

            var assemblyResolver = new CustomAssemblyResolver();

            // Check if there is a PDB
            var readPdb = File.Exists(System.IO.Path.ChangeExtension(inputFile, "pdb"));

            var assemblyDefinition = AssemblyDefinition.ReadAssembly(inputFile,
                new ReaderParameters { AssemblyResolver = assemblyResolver, ReadSymbols = readPdb });

            // Register self to assembly resolver
            assemblyResolver.Register(assemblyDefinition);

            var compiler = new Compiler();
            var module = compiler.CompileAssembly(assemblyDefinition);

            if (generateIR)
            {
                var irFile = Path.ChangeExtension(outputFile, "ll");
                var ir = LLVM.PrintModuleToString(module);
                File.WriteAllText(irFile, ir);
            }

            LLVM.WriteBitcodeToFile(module, outputFile);
        }

        const string ClangWrapper = "vs_clang.bat";

        public static void LinkBitcodes(string outputFile, params string[] bitcodeFiles)
        {
            GenerateObjects(bitcodeFiles);

            var filesToLink = new List<string>();
            filesToLink.AddRange(bitcodeFiles.Select(x => Path.ChangeExtension(x, "obj")));
            filesToLink.Add(Path.Combine(Utils.GetTestsDirectory(), "MiniCorlib.c"));

            var arguments = new StringBuilder();
            arguments.AppendFormat("{0} -o {1}", string.Join(" ", filesToLink), outputFile);

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
            
            if (isWindowsOS)
            {
                if (useMSVCToolchain)
                {
                    CreateCompilerWrapper();
                }
                else
                {
                    // Add mingw32 paths
                    arguments.AppendFormat(" --target=i686-pc-mingw32");
                    processStartInfo.EnvironmentVariables.Add("CPATH", @"..\..\..\..\deps\mingw32\i686-w64-mingw32\include");
                    processStartInfo.EnvironmentVariables["PATH"] += @";..\..\..\..\deps\mingw32\bin";
                }
            }

            processStartInfo.Arguments = arguments.ToString();

            string processLinkerOutput;
            var processLinker = ExecuteAndCaptureOutput(processStartInfo, out processLinkerOutput);
            processLinker.WaitForExit();

            if (processLinker.ExitCode != 0)
                throw new InvalidOperationException(string.Format("Error executing clang: {0}",
                    processLinkerOutput));
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
                var processLLC = ExecuteAndCaptureOutput(processStartInfo, out processLLCOutput);
                if (processLLC.ExitCode != 0)
                {
                    throw new InvalidOperationException(string.Format("Error executing llc: {0}", processLLCOutput));
                }
            }
        }

        /// <summary>
        /// Executes process and capture its output.
        /// </summary>
        /// <param name="processStartInfo">The process start information.</param>
        /// <param name="output">The output.</param>
        /// <returns></returns>
        private static Process ExecuteAndCaptureOutput(ProcessStartInfo processStartInfo, out string output)
        {
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;

            var process = new Process {StartInfo = processStartInfo};

            var outputBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, args) =>
            {
                lock (outputBuilder)
                {
                    if (args.Data != null)
                        outputBuilder.AppendLine(args.Data);
                }
            };
            process.ErrorDataReceived += (sender, args) =>
            {
                lock (outputBuilder)
                {
                    if (args.Data != null)
                        outputBuilder.AppendLine(args.Data);
                }
            };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            output = outputBuilder.ToString();
            return process;
        }
    }
}