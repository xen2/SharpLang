using System;
using Mono.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public sealed class Compiler
    {
        public ModuleRef CompileAssembly(AssemblyDefinition assembly)
        {
            var module = LLVM.ModuleCreateWithName(assembly.Name.Name);

            foreach (var assemblyModule in assembly.Modules)
            {
                foreach (var type in assemblyModule.Types)
                {
                    CompileType(type);
                }
            }

            return module;
        }

        ValueRef CompileType(TypeDefinition type)
        {
            throw new NotImplementedException();
        }

        ValueRef CompileMethod(MethodDefinition method)
        {
            throw new NotImplementedException();
        }
    }
}