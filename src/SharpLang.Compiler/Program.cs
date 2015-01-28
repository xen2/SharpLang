using System;
using System.IO;
using System.Reflection;
using Mono.Options;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    class Program
    {
        static int Main(string[] args)
        {
            var exeName = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
            var showHelp = false;
            var generateIR = false;
            int exitCode = 0;
            string outputFile = null;
            string target = Driver.GetDefaultTriple();

            var p = new OptionSet
                {
                    "SharpLang Compiler - Version: "
                    +
                    String.Format(
                        "{0}.{1}.{2}",
                        typeof(Program).Assembly.GetName().Version.Major,
                        typeof(Program).Assembly.GetName().Version.Minor,
                        typeof(Program).Assembly.GetName().Version.Build) + string.Empty,
                    string.Format("Usage: {0} assembly.dll [options]*", exeName),
                    string.Empty,
                    "=== Options ===",
                    string.Empty,
                    { "h|help", "Show this message and exit", v => showHelp = v != null },
                    { "o|output=", "Output filename. Default to [inputfilename].bc", v => outputFile = v },
                    { "d", "Generate debug LLVM IR assembly output", v => generateIR = true },
                    { "target", "Choose target triple", v => target = v },
                };

            try
            {
                var inputFiles = p.Parse(args);

                if (showHelp)
                {
                    p.WriteOptionDescriptions(Console.Out);
                    return 0;
                }

                if (inputFiles.Count == 0)
                {
                    throw new OptionException("No input file", string.Empty);
                }
                else if (inputFiles.Count > 1)
                {
                    throw new OptionException("Only one input file is currently accepted", string.Empty);
                }

                var inputFile = inputFiles[0];

                if (outputFile == null)
                {
                    outputFile = Path.ChangeExtension(inputFile, "bc");
                }

                Driver.CompileAssembly(inputFile, outputFile, target, generateIR);
            }
            catch (OptionException e)
            {
                Console.WriteLine("Command option '{0}': {1}", e.OptionName, e.Message);
                exitCode = -1;
            }
            catch (Exception e)
            {
                Console.WriteLine("Unexpected error: {0}", e);
                exitCode = -1;
            }

            return exitCode;
        }
    }
}
