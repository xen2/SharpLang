using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;

namespace System
{
    /// <summary>
    /// <see cref="Type"/> implementation for type definitions (including generic type definitions).
    /// </summary>
    unsafe class SharpLangTypeDefinition : SharpLangType
    {
        private static readonly SharpLangTypeGenericParameter[] EmptyGenericParameters = new SharpLangTypeGenericParameter[0];
        private static readonly Dictionary<SharpLangTypeDefinition, SharpLangTypeGenericParameter[]> instantiatedGenericParameters = new Dictionary<SharpLangTypeDefinition, SharpLangTypeGenericParameter[]>(new DefaultComparer<SharpLangTypeDefinition>());

        internal SharpLangModule InternalModule;
        internal TypeDefinitionHandle InternalHandle;

        public SharpLangTypeDefinition(SharpLangEEType* eeType, SharpLangModule module, TypeDefinitionHandle handle) : base(eeType)
        {
            this.InternalModule = module;
            this.InternalHandle = handle;
        }

        public override string Name
        {
            get
            {
                var typeDefinition = InternalModule.MetadataReader.GetTypeDefinition(InternalHandle);
                return InternalModule.MetadataReader.GetString(typeDefinition.Name);
            }
        }

        public override string Namespace
        {
            get
            {
                var typeDefinition = InternalModule.MetadataReader.GetTypeDefinition(InternalHandle);
                var @namespace = InternalModule.MetadataReader.GetNamespaceDefinition(typeDefinition.Namespace);
                var @namespaceName = InternalModule.MetadataReader.GetString(@namespace.Name);
                
                // Empty string means no namespace, returns null
                if (string.IsNullOrEmpty(@namespaceName))
                    return null;

                return @namespaceName;
            }
        }

        public override Type BaseType
        {
            get
            {
                // Test: Force going through ResolveBaseType
                //if (EEType != null)
                //    return base.BaseType;

                return ResolveBaseType(this);
            }
        }

        public override Type DeclaringType
        {
            get { return ResolveDeclaringType(this); }
        }

        public override Module Module
        {
            get { return InternalModule; }
        }

        public override Assembly Assembly
        {
            get { return InternalModule.Assembly; }
        }

        protected override TypeAttributes GetAttributeFlagsImpl()
        {
            var typeDefinition = InternalModule.MetadataReader.GetTypeDefinition(InternalHandle);
            return typeDefinition.Attributes;
        }

        internal Type ResolveBaseType(SharpLangType context)
        {
            var typeDefinition = InternalModule.MetadataReader.GetTypeDefinition(InternalHandle);

            var baseType = typeDefinition.BaseType;
            if (baseType.IsNil)
                return null;

            return InternalModule.ResolveTypeHandle(context, baseType);
        }

        internal Type ResolveDeclaringType(SharpLangType context)
        {
            var typeDefinition = InternalModule.MetadataReader.GetTypeDefinition(InternalHandle);

            var declaringType = typeDefinition.GetDeclaringType();
            if (declaringType.IsNil)
                return null;

            return InternalModule.ResolveTypeHandle(context, declaringType);
        }

        public override Type[] GetGenericArguments()
        {
            var genericParameters = InternalGetGenericParameters();

            // Make a copy of the array
            var result = new Type[genericParameters.Length];
            for (int i = 0; i < genericParameters.Length; ++i)
                result[i] = genericParameters[i];

            return result;
        }

        public override MethodInfo[] GetMethods(BindingFlags bindingAttr)
        {
            // Returns true if there is any generic parameters
            var typeDefinition = InternalModule.MetadataReader.GetTypeDefinition(InternalHandle);
            var methods = typeDefinition.GetMethods();

            foreach (var method in methods)
            {
                
            }

            throw new NotImplementedException();
        }

        internal SharpLangTypeGenericParameter[] InternalGetGenericParameters()
        {
            lock (instantiatedGenericParameters)
            {
                SharpLangTypeGenericParameter[] genericParameters;
                if (!instantiatedGenericParameters.TryGetValue(this, out genericParameters))
                {
                    // First time, create generic parameters
                    var typeDefinition = InternalModule.MetadataReader.GetTypeDefinition(InternalHandle);
                    var metadataGenericParameters = typeDefinition.GetGenericParameters();

                    // Early exit if empty
                    if (metadataGenericParameters.Count == 0)
                        return EmptyGenericParameters;

                    genericParameters = new SharpLangTypeGenericParameter[metadataGenericParameters.Count];
                    for (int i = 0; i < genericParameters.Length; ++i)
                    {
                        genericParameters[i] = new SharpLangTypeGenericParameter(InternalModule, metadataGenericParameters[i]);
                    }

                    instantiatedGenericParameters.Add(this, genericParameters);
                }
                return genericParameters;
            }
        }

        public override bool IsGenericTypeDefinition
        {
            get
            {
                // Returns true if there is any generic parameters
                var typeDefinition = InternalModule.MetadataReader.GetTypeDefinition(InternalHandle);
                return typeDefinition.GetGenericParameters().Count > 0;
            }
        }

        internal override string InternalAssemblyName
        {
            get
            {
                var assemblyDefinition = InternalModule.MetadataReader.GetAssemblyDefinition();
                return GetAssemblyQualifiedName(assemblyDefinition);
            }
        }

        public override Type MakeGenericType(params Type[] typeArguments)
        {
            return SharpLangModule.ResolveGenericType(null, this, (SharpLangType[])typeArguments);
        }

        string GetAssemblyQualifiedName(AssemblyDefinition assemblyDefinition)
        {
            // TODO: Should move that to future SharpLangAssembly type
            var assemblyName = InternalModule.MetadataReader.GetString(assemblyDefinition.Name);
            var assemblyVersion = assemblyDefinition.Version;
            var assemblyCulture = InternalModule.MetadataReader.GetString(assemblyDefinition.Culture);
            if (string.IsNullOrEmpty(assemblyCulture))
                assemblyCulture = "neutral";

            var assemblyPublicKeyBytes = InternalModule.MetadataReader.GetBlobBytes(assemblyDefinition.PublicKey);
            var assemblyPublicKey = assemblyPublicKeyBytes.Length > 0 ? BytesToHexString(ComputePublicKeyToken(assemblyPublicKeyBytes, assemblyDefinition.HashAlgorithm)) : "null";

            return assemblyName + ", Version=" + assemblyVersion + ", Culture=" + assemblyCulture + ", PublicKeyToken=" + assemblyPublicKey;
        }

        static byte[] ComputePublicKeyToken(byte[] publicKey, AssemblyHashAlgorithm hashAlgorithm)
        {
            var publicKeyHashed = HashPublicKey(publicKey, hashAlgorithm);
            
            // Copy last 8 bytes, in reverse order
            var result = new byte[8];
            for (int i = 0; i < 8; ++i)
                result[i] = publicKeyHashed[publicKeyHashed.Length - 1 - i];

            return result;
        }

        static byte[] HashPublicKey(byte[] publicKey, AssemblyHashAlgorithm hashAlgorithm)
        {
            HashAlgorithm algorithm;

            switch (hashAlgorithm)
            {
                case AssemblyHashAlgorithm.MD5:
                    algorithm = new MD5CryptoServiceProvider();
                    break;
                case AssemblyHashAlgorithm.Sha1:
                    // None default to SHA1
                    algorithm = new SHA1Managed ();
                    break;
                case AssemblyHashAlgorithm.Sha256:
                    // None default to SHA1
                    algorithm = new SHA256Managed();
                    break;
                case AssemblyHashAlgorithm.Sha512:
                    // None default to SHA1
                    algorithm = new SHA512Managed();
                    break;
                default:
                    throw new InvalidOperationException();
            }

            using (algorithm)
                return algorithm.ComputeHash(publicKey);
        }

        static string BytesToHexString(byte[] bytes)
        {
            var result = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                result.Append(b.ToString("x2"));
            return result.ToString();
        }
    }
}