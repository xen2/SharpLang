using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public class Driver
    {
        public static void CompileAssembly(string inputFile, string outputFile)
        {
            var assemblyResolver = new DefaultAssemblyResolver();
            var assemblyDefinition = AssemblyDefinition.ReadAssembly(inputFile,
                new ReaderParameters { AssemblyResolver = assemblyResolver, ReadSymbols = true });

            var compiler = new Compiler();
            var module = compiler.CompileAssembly(assemblyResolver, assemblyDefinition);

            LLVM.WriteBitcodeToFile(module, outputFile);
        } 
    }
}