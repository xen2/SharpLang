// Copyright (c) 2014 SharpLang - Virgile Bello

using System.Collections.Generic;
using System.Reflection;
using System.Security.Policy;

namespace System
{
    public partial class AppDomain
    {
        private static readonly AppDomain currentDomain = new AppDomain();

        internal readonly List<SharpLangAssembly> Assemblies = new List<SharpLangAssembly>();

        private static AppDomain getCurDomain()
        {
            return currentDomain;
        }

        private Assembly[] GetAssemblies(bool refOnly)
        {
            lock (SharpLangModule.SystemTypeLock)
            {
                var result = new Assembly[Assemblies.Count];
                for (int i = 0; i < Assemblies.Count; ++i)
                {
                    result[i] = Assemblies[i];
                }

                return result;
            }
        }

        internal Assembly LoadAssembly(string assemblyRef, Evidence securityEvidence, bool refOnly)
        {
            throw new NotImplementedException();
        }
    }
}