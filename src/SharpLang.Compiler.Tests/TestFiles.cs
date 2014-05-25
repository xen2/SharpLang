using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace SharpLang.CompilerServices.Tests
{
    public class TestFiles
    {
        [ThreadStatic]
        public static CodeDomProvider codeDomProvider;

        [Test, TestCaseSource("TestCases")]
        public void Test(string sourceFile)
        {
            if (codeDomProvider == null)
                codeDomProvider = new Microsoft.CSharp.CSharpCodeProvider();

            var compilerParameters = new CompilerParameters();
            compilerParameters.IncludeDebugInformation = false;
            compilerParameters.GenerateInMemory = false;
            compilerParameters.GenerateExecutable = true;
            compilerParameters.TreatWarningsAsErrors = false;

            compilerParameters.OutputAssembly = Path.ChangeExtension(sourceFile, "exe");
            var compilerResults = codeDomProvider.CompileAssemblyFromFile(compilerParameters, sourceFile);

            if (compilerResults.Errors.HasErrors)
            {
                var errors = new StringBuilder();
                errors.AppendLine(string.Format("Serialization assembly compilation: {0} error(s)", compilerResults.Errors.Count));
                foreach (var error in compilerResults.Errors)
                    errors.AppendLine(error.ToString());

                throw new InvalidOperationException(errors.ToString());
            }

            var bitcodeFile = Path.ChangeExtension(sourceFile, "bc");
            Driver.CompileAssembly(compilerParameters.OutputAssembly, bitcodeFile);

            var outputFile = Path.Combine(Path.GetDirectoryName(sourceFile),
                Path.GetFileNameWithoutExtension(sourceFile) + "-llvm.exe");

            // Link bitcode and runtime
            Driver.LinkBitcodes(outputFile, bitcodeFile);

            // Execute original and ours
            var output1 = ExecuteAndCaptureOutput(compilerParameters.OutputAssembly);
            var output2 = ExecuteAndCaptureOutput(outputFile);

            // Compare output
            Assert.That(output2, Is.EqualTo(output1));
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

            var process = new Process();
            process.StartInfo = processStartInfo;

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
                foreach (var file in Directory.EnumerateFiles("tests", "*.cs", SearchOption.AllDirectories))
                {
                    yield return file;
                }
            }
        }
    }
}