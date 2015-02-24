// Copyright (c) 2014 SharpLang - Virgile Bello

using System.Collections.Generic;

namespace System.Reflection
{
    /// <summary>
    /// Implementation of <see cref="Assembly"/> for SharpLang runtime.
    /// </summary>
    public class SharpLangAssembly : Assembly
    {
        internal SharpLangModule[] Modules;

        internal SharpLangAssembly(SharpLangModule mainModule)
        {
            this.Modules = new [] { mainModule };
        }

        public override string ToString()
        {
            lock (SharpLangModule.SystemTypeLock)
            {
                var mainModule = Modules[0];
                return mainModule.InternalAssemblyName;
            }
        }

        public override Type GetType(string name, bool throwOnError, bool ignoreCase)
        {
            throw new NotImplementedException();
        }

        public override AssemblyName[] GetReferencedAssemblies()
        {
            throw new NotImplementedException();
        }

        public override Module[] GetModules(bool getResourceModules)
        {
            lock (SharpLangModule.SystemTypeLock)
            {
                var result = new Module[Modules.Length];

                for (int i = 0; i < result.Length; ++i)
                {
                    result[i] = Modules[i];
                }

                return result;
            }
        }
    }
}