// Copyright (c) 2014 SharpLang - Virgile Bello
namespace System
{
    public partial class Type
    {
        private static Type internal_from_name(string name, bool throwOnError, bool ignoreCase)
        {
            string fullname, assemblyName;

            SplitAssemblyQualifiedName(name, out fullname, out assemblyName);

            throw new NotImplementedException();
        }

        private static void SplitAssemblyQualifiedName(string assemblyQualifiedName, out string fullname, out string assemblyName)
        {
            var assemblyNameStartIndex = assemblyQualifiedName.LastIndexOf(',');

            if (assemblyNameStartIndex != -1)
            {
                fullname = assemblyQualifiedName.Substring(0, assemblyNameStartIndex++);

                while (assemblyQualifiedName[assemblyNameStartIndex] == ' ')
                    assemblyNameStartIndex++;

                assemblyName = fullname.Substring(assemblyNameStartIndex);
            }
            else
            {
                fullname = assemblyQualifiedName;
                assemblyName = string.Empty;
            }
        }
    }
}