using System.IO;
using Mono.Cecil;

namespace SharpLang.CompilerServices.Cecil
{
    class CustomAssemblyResolver : DefaultAssemblyResolver
    {
        public CustomAssemblyResolver()
        {
            // Start with an empty search directory list
            foreach (var searchDirectory in GetSearchDirectories())
            {
                RemoveSearchDirectory(searchDirectory);
            }
        }

        /// <summary>
        /// Registers the specified assembly.
        /// </summary>
        /// <param name="assembly">The assembly to register.</param>
        public void Register(AssemblyDefinition assembly)
        {
            this.RegisterAssembly(assembly);
        }
    }
}