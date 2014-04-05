using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Mono.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public class Driver
    {
        /// <summary>
        /// Gets or sets the paths used when executing command line.
        /// </summary>
        /// <value>
        /// The paths used when executing command line.
        /// </value>
        public static string Path { get; set; }

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
        public static string CC { get; set; }

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

            // Prepare PATH to have access to various compiler tools
            if (!string.IsNullOrEmpty(Path))
            {
                var path = processStartInfo.EnvironmentVariables["PATH"];
                processStartInfo.EnvironmentVariables["PATH"] = Path + ";" + path;
            }

            // Compile each LLVM .bc file to a .obj file with llc
            foreach (var bitcodeFile in bitcodeFiles)
            {
                processStartInfo.FileName = LLC;
                processStartInfo.Arguments = string.Format("-filetype=obj {0} -o {1}", bitcodeFile, System.IO.Path.ChangeExtension(bitcodeFile, "obj"));

                var processLLC = Process.Start(processStartInfo);
                processLLC.WaitForExit();
                if (processLLC.ExitCode != 0)
                {
                    throw new InvalidOperationException("Error executing llc.");
                }
            }
            
            // Use gcc to link the .obj files and runtime
            processStartInfo.FileName = CC;
            processStartInfo.Arguments = string.Format("{0} tests\\MiniRuntime.c -o {1}", string.Join(" ", bitcodeFiles.Select(x => System.IO.Path.ChangeExtension(x, "obj"))), outputFile);

            var processLinker = Process.Start(processStartInfo);
            processLinker.WaitForExit();
            if (processLinker.ExitCode != 0)
            {
                throw new InvalidOperationException("Error executing llc.");
            }
        }
    }
}