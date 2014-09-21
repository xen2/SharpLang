using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;
using SharpLang.Compiler.Utils;

namespace SharpLang.CompilerServices.Tests
{
    public class TestFiles
    {
        [ThreadStatic]
        public static CodeDomProvider codeDomProvider;

        [Test, TestCaseSource("TestCases")]
        public static void Test(string sourceFile)
        {
            CompilerParameters compilerParameters;
            var outputAssembly = CompileAssembly(sourceFile);

            var bitcodeFile = Path.ChangeExtension(outputAssembly, "bc");

            SetupLocalToolchain();

            // Compile with a few additional corlib types useful for normal execution
            Driver.CompileAssembly(outputAssembly, bitcodeFile, additionalTypes: new[]
            {
                typeof(Exception),
                typeof(OverflowException),
                typeof(InvalidCastException),
                typeof(NotSupportedException),
                typeof(Array),
            });

            var outputFile = Path.Combine(Path.GetDirectoryName(outputAssembly),
                Path.GetFileNameWithoutExtension(outputAssembly) + "-llvm.exe");

            // Compile MiniCorlib.c
            Driver.ExecuteClang(string.Format("{0} -emit-llvm -c -o {1}", Path.Combine(Utils.GetTestsDirectory(), "MiniCorlib.c"), Path.Combine(Utils.GetTestsDirectory(), "MiniCorlib.bc")));

            // Link bitcode and runtime
            Driver.LinkBitcodes(outputFile, bitcodeFile, Path.Combine(Utils.GetTestsDirectory(), "MiniCorlib.bc"));

            // Execute original and ours
            var output1 = ExecuteAndCaptureOutput(outputAssembly);
            var output2 = ExecuteAndCaptureOutput(outputFile);

            // Compare output
            Assert.That(output2, Is.EqualTo(output1));
        }

        private static string CompileAssembly(string sourceFile)
        {
            if (codeDomProvider == null)
                codeDomProvider = new Microsoft.CSharp.CSharpCodeProvider();

            var outputBase = Path.Combine(Path.GetDirectoryName(sourceFile), "output");
            var outputAssembly = Path.Combine(outputBase, Path.GetFileNameWithoutExtension(sourceFile) + ".exe");
            Directory.CreateDirectory(outputBase);

            var compilerParameters = new CompilerParameters
            {
                IncludeDebugInformation = false,
                GenerateInMemory = false,
                GenerateExecutable = true,
                TreatWarningsAsErrors = false,
                CompilerOptions = "/unsafe /debug+ /debug:full",
                OutputAssembly = outputAssembly,
            };

            var compilerResults = codeDomProvider.CompileAssemblyFromFile(
                compilerParameters, sourceFile);

            if (compilerResults.Errors.HasErrors)
            {
                var errors = new StringBuilder();
                errors.AppendLine(string.Format("Serialization assembly compilation: {0} error(s)",
                    compilerResults.Errors.Count));
                foreach (var error in compilerResults.Errors)
                    errors.AppendLine(error.ToString());

                throw new InvalidOperationException(errors.ToString());
            }

            return outputAssembly;
        }

        private static void SetupLocalToolchain()
        {
            // Try to use locally compiled llc and clang (if they exist)
            var llcLocal = @"../../../../deps/llvm/build/RelWithDebInfo/bin/llc".Replace('/', Path.DirectorySeparatorChar);
            if (File.Exists(llcLocal) || File.Exists(llcLocal + ".exe"))
                Driver.LLC = llcLocal;

            var clangLocal = @"../../../../deps/llvm/build/RelWithDebInfo/bin/clang".Replace('/', Path.DirectorySeparatorChar);
            if (File.Exists(clangLocal) || File.Exists(clangLocal + ".exe"))
                Driver.Clang = clangLocal;

            // Probe again for local Ninja builds
            llcLocal = @"../../../../deps/llvm/build/bin/llc".Replace('/', Path.DirectorySeparatorChar);
            if (File.Exists(llcLocal) || File.Exists(llcLocal + ".exe"))
                Driver.LLC = llcLocal;

            clangLocal = @"../../../../deps/llvm/build/bin/clang".Replace('/', Path.DirectorySeparatorChar);
            if (File.Exists(clangLocal) || File.Exists(clangLocal + ".exe"))
                Driver.Clang = clangLocal;
        }

        /// <summary>
        /// Executes process and capture its output.
        /// </summary>
        /// <param name="executableFile">The executable file.</param>
        /// <returns></returns>
        private static string ExecuteAndCaptureOutput(string executableFile)
        {
            var processStartInfo = new ProcessStartInfo(executableFile);

            var output = new StringBuilder();
            processStartInfo.UseShellExecute = false;
            processStartInfo.CreateNoWindow = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;

            var process = new Process {StartInfo = processStartInfo};

            process.OutputDataReceived += (sender, args) =>
            {
                lock (output)
                {
                    if (args.Data != null)
                        output.AppendLine(args.Data);
                }
            };
            process.ErrorDataReceived += (sender, args) =>
            {
                lock (output)
                {
                    if (args.Data != null)
                        output.AppendLine(args.Data);
                }
            };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new InvalidOperationException("Invalid exit code.");

            return output.ToString();
        }

        public static IEnumerable<string> TestCases
        {
            get
            {
                var directory = Utils.GetTestsDirectory();
                var files = Directory.EnumerateFiles(directory, "*.cs",
                    SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    yield return file;
                }
            }
        }
    }
}