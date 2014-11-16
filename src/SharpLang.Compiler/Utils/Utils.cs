using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SharpLang.Compiler.Utils
{
    public class Utils
    {
        /// <summary>
        /// Executes process and capture its output.
        /// </summary>
        /// <param name="processStartInfo">The process start information.</param>
        /// <param name="output">The output.</param>
        /// <returns></returns>
        public static Process ExecuteAndCaptureOutput(ProcessStartInfo processStartInfo, out string output)
        {
            processStartInfo.CreateNoWindow = true;
            processStartInfo.UseShellExecute = false;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;

            var process = new Process { StartInfo = processStartInfo };

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

        public static string GetTestsDirectory(string subdir)
        {
            var directory = Directory.GetParent(Directory.GetCurrentDirectory());
            var testsSubPath = Path.Combine("src", "SharpLang.Compiler.Tests", subdir);

            while (directory != null)
            {
                var path = Path.Combine(directory.FullName, testsSubPath);

                if (Directory.Exists(path))
                    return path;

                directory = directory.Parent;
            }

            throw new Exception("Tests directory was not found");
        }
    }
}
