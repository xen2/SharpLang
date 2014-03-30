using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;
using NUnit.Framework;
using SharpLLVM;

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

            Driver.CompileAssembly(compilerParameters.OutputAssembly, Path.ChangeExtension(sourceFile, "bc"));

            // TODO: Execute and compare output
            throw new NotImplementedException();
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