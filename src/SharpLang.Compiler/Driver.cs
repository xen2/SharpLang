using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;
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

        public static void CompileAssembly(string inputFile, string outputFile)
        {
            // Force PdbReader to be referenced
            typeof(Mono.Cecil.Pdb.PdbReader).ToString();

            var assemblyResolver = new DefaultAssemblyResolver();

            // Check if there is a PDB
            var readPdb = File.Exists(System.IO.Path.ChangeExtension(inputFile, "pdb"));

            var assemblyDefinition = AssemblyDefinition.ReadAssembly(inputFile,
                new ReaderParameters { AssemblyResolver = assemblyResolver, ReadSymbols = readPdb });

            var compiler = new Compiler();
            var module = compiler.CompileAssembly(assemblyDefinition);

            LLVM.WriteBitcodeToFile(module, outputFile);
        }

        public static void LinkBitcodes(string outputFile, params string[] bitcodeFiles)
        {
            var processStartInfo = new ProcessStartInfo();
            processStartInfo.CreateNoWindow = true;
            processStartInfo.UseShellExecute = false;

            // Setup environment using vcvars32.bat
            processStartInfo.FileName = @"C:\Program Files (x86)\Microsoft Visual Studio 12.0\VC\bin\vcvars32.bat";
            string output;
            ExecuteAndCaptureOutput(processStartInfo, out output);

            // Compile each LLVM .bc file to a .obj file with llc
            foreach (var bitcodeFile in bitcodeFiles)
            {
                processStartInfo.FileName = LLC;
                processStartInfo.Arguments = string.Format("-filetype=obj {0} -o {1}", bitcodeFile, System.IO.Path.ChangeExtension(bitcodeFile, "obj"));

                string processLLCOutput;
                var processLLC = ExecuteAndCaptureOutput(processStartInfo, out processLLCOutput);
                if (processLLC.ExitCode != 0)
                {
                    throw new InvalidOperationException(string.Format("Error executing llc: {0}", processLLCOutput));
                }
            }
            
            // Use clang to link the .obj files and runtime
            if (true) // Platform is windows
            {
                // On Windows, we need to have vcvars32 called before clang (so that it can find linker)
                var clangWrapper = "clang.bat";
                if (!clangWrapperCreated)
                {
                    var vc7Dir = GetPathToVC7("12.0") ?? GetPathToVC7("11.0") ?? GetPathToVC7("10.0");
                    if (vc7Dir == null)
                        throw new NotImplementedException("Could not find Visual C++ compiler path.");

                    File.WriteAllLines(clangWrapper,
                        new[]
                        {
                            string.Format("call \"{0}vcvarsall.bat\" x86", vc7Dir),
                            string.Format("{0} %*", Clang)
                        });
                    clangWrapperCreated = true;
                }

                processStartInfo.FileName = "clang.bat";
            }
            else
            {
                processStartInfo.FileName = Clang;
            }
            processStartInfo.Arguments = string.Format("{0} tests\\MiniCorlib.c -o {1}", string.Join(" ", bitcodeFiles.Select(x => System.IO.Path.ChangeExtension(x, "obj"))), outputFile);

            string processLinkerOutput;
            var processLinker = ExecuteAndCaptureOutput(processStartInfo, out processLinkerOutput);
            processLinker.WaitForExit();
            if (processLinker.ExitCode != 0)
            {
                throw new InvalidOperationException(string.Format("Error executing clang: {0}", processLinkerOutput));
            }
        }

        /// <summary>
        /// Gets the path to Visual C++ compiler.
        /// </summary>
        /// <param name="version">The Visual C++ compiler version.</param>
        /// <returns>The path to Visual C++ compiler.</returns>
        public static string GetPathToVC7(string version)
        {
            // Open Key
            var is64BitProcess = Environment.Is64BitProcess;
            var registryKeyName = string.Format(@"Software\{0}Microsoft\VisualStudio\SxS\VC7", is64BitProcess ? @"Wow6432Node\" : string.Empty);
            var vsKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(registryKeyName);
            if (vsKey == null)
                return null;

            // Find appropriate value
            var value = vsKey.GetValue(version);
            if (value == null)
                return null;

            return value.ToString();
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

            var process = new Process();
            process.StartInfo = processStartInfo;

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