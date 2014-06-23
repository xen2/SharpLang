using System.IO;
using Mono.Cecil;

namespace SharpLang.CompilerServices.Cecil
{
    class CustomAssemblyResolver : DefaultAssemblyResolver
    {
        /// <summary>
        /// Registers the specified assembly.
        /// </summary>
        /// <param name="assembly">The assembly to register.</param>
        public void Register(AssemblyDefinition assembly)
        {
            this.RegisterAssembly(assembly);
            this.AddSearchDirectory(Path.GetDirectoryName(assembly.MainModule.FullyQualifiedName));
        }
    }
}