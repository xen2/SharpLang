using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;

namespace SharpLang.CompilerServices.Cecil
{
    class CustomAssemblyResolver : BaseAssemblyResolver
    {
        readonly Dictionary<string, AssemblyDefinition> cache = new Dictionary<string, AssemblyDefinition>(StringComparer.Ordinal);

        public CustomAssemblyResolver()
        {
            // Start with an empty search directory list
            foreach (var searchDirectory in GetSearchDirectories())
            {
                RemoveSearchDirectory(searchDirectory);
            }
        }

        /// <inheritdoc/>
        public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            AssemblyDefinition assembly;
            if (cache.TryGetValue(name.FullName, out assembly))
                return assembly;

            assembly = base.Resolve(name, parameters);
            cache[name.FullName] = assembly;

            return assembly;
        }


        /// <summary>
        /// Registers the specified assembly.
        /// </summary>
        /// <param name="assembly">The assembly to register.</param>
        public void Register(AssemblyDefinition assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException("assembly");

            var name = assembly.Name.FullName;
            if (cache.ContainsKey(name))
                return;

            cache[name] = assembly;
        }
    }
}