// // Copyright (c) 2014 SharpLang - Virgile Bello

using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;

namespace System
{
    /// <summary>
    /// Implementation of <see cref="Module"/> for SharpLang runtime.
    /// </summary>
    public class SharpLangModule : Module
    {
        internal static object SystemTypeLock = new object();

        internal readonly List<SharpLangAssembly> Assemblies = new List<SharpLangAssembly>();

        // Sorted list of SharpLangEEType* (contains all codegen types)
        // We might look into that list to get a SharpLangEEType* of a SharpLangType created without its SharpLangEEType*
        // (happens with methods such as GetType(name) or MakeGenericType()).
        private static readonly List<SharpLangEETypePtr> types = new List<SharpLangEETypePtr>();

        private static readonly Dictionary<SharpLangEETypeDefinition, SharpLangTypeDefinition> typeDefinitions = new Dictionary<SharpLangEETypeDefinition, SharpLangTypeDefinition>(new ObjectEqualityComparer<SharpLangEETypeDefinition>());
        private static readonly Dictionary<SharpLangType, SharpLangTypeElement> pointerTypes = new Dictionary<SharpLangType, SharpLangTypeElement>(new ObjectEqualityComparer<SharpLangType>());
        private static readonly Dictionary<SharpLangType, SharpLangTypeElement> byRefTypes = new Dictionary<SharpLangType, SharpLangTypeElement>(new ObjectEqualityComparer<SharpLangType>());
        private static readonly Dictionary<SharpLangType, SharpLangTypeElement> arrayTypes = new Dictionary<SharpLangType, SharpLangTypeElement>(new ObjectEqualityComparer<SharpLangType>());
        private static readonly Dictionary<GenericKey, SharpLangTypeGeneric> genericTypes = new Dictionary<GenericKey, SharpLangTypeGeneric>(new GenericEqualityComparer<GenericKey>());

        private readonly Dictionary<Handle, SharpLangModule> moduleHandleCache = new Dictionary<Handle, SharpLangModule>(new GenericEqualityComparer<Handle>());

        public readonly IntPtr MetadataStart;
        public readonly int MetadataLength;
        
        private MetadataReader metadataReader;
        private SharpLangAssembly assembly;

        internal MetadataReader MetadataReader
        {
            get
            {
                if (metadataReader != null)
                    return metadataReader;

                lock (SystemTypeLock)
                unsafe
                {
                    if (metadataReader == null)
                        metadataReader = new MetadataReader((byte*)MetadataStart, MetadataLength);

                    return metadataReader;
                }
            }
        }

        internal string InternalAssemblyName
        {
            get
            {
                var assemblyDefinition = MetadataReader.GetAssemblyDefinition();
                return GetAssemblyQualifiedName(assemblyDefinition);
            }
        }

        public SharpLangModule(IntPtr metadataStart, int metadataLength)
        {
            MetadataStart = metadataStart;
            MetadataLength = metadataLength;

            lock (SystemTypeLock)
            {
                assembly = new SharpLangAssembly(this);
                Assemblies.Add(assembly);
            }
        }

        internal unsafe static void RegisterType(SharpLangEEType* type)
        {
            types.Add(type);
        }

        internal unsafe static void RegisterTypes(SharpLangEEType** typesToRegister, int typeCount)
        {
            for (int i = 0; i < typeCount; ++i)
            {
                types.Add(*typesToRegister++);
            }
        }

        internal unsafe static void SortTypes()
        {
            // Sort
            types.Sort(SharpLangEETypeComparer.Default);

            // Remove duplicates
            for (int i = 1; i < types.Count; ++i)
            {
                if (types[i].Value == types[i - 1].Value)
                    types.RemoveAt(i--);
            }
        }

        public override Assembly Assembly
        {
            get { return assembly; }
        }

        internal SharpLangType ResolveType(string fullname)
        {
            string @namespace;
            string name;
            SplitFullName(fullname, out @namespace, out name);

            return ResolveType(@namespace, name);
        }

        internal unsafe SharpLangType ResolveType(string @namespace, string name)
        {
            var stringComparer = MetadataReader.StringComparer;

            foreach (var typeDefHandle in MetadataReader.TypeDefinitions)
            {
                var typeDef = MetadataReader.GetTypeDefinition(typeDefHandle);
                if (stringComparer.Equals(typeDef.Name, name)
                    && stringComparer.Equals(typeDef.Namespace, @namespace))
                {
                    // Look for type in types
                    lock (SystemTypeLock)
                    {
                        // Find this type in list of instantiated types
                        return ResolveTypeDef(null, typeDefHandle);
                    }
                }
            }

            return null;
        }

        public static void SplitFullName(string fullname, out string @namespace, out string name)
        {
            var lastDotIndex = fullname.LastIndexOf('.');

            if (lastDotIndex != -1)
            {
                @namespace = fullname.Substring(0, lastDotIndex);
                name = fullname.Substring(lastDotIndex + 1);
            }
            else
            {
                @namespace = string.Empty;
                name = fullname;
            }
        }

        internal SharpLangModule ResolveModule(Handle handle)
        {
            lock (SystemTypeLock)
            {
                // Check cache
                SharpLangModule module;
                if (moduleHandleCache.TryGetValue(handle, out module))
                    return module;

                switch (handle.Kind)
                {
                    case HandleKind.AssemblyReference:
                    {
                        var assemblyReference = MetadataReader.GetAssemblyReference((AssemblyReferenceHandle)handle);
                        var assemblyReferenceName = MetadataReader.GetString(assemblyReference.Name);
                        foreach (var assembly in Assemblies)
                        {
                            foreach (var currentModule in assembly.Modules)
                            {
                                var assemblyDefinition = currentModule.MetadataReader.GetAssemblyDefinition();

                                // TODO: Check public token, and version checking/rebinding?
                                if (currentModule.MetadataReader.GetString(assemblyDefinition.Name) == assemblyReferenceName)
                                {
                                    module = currentModule;
                                    break;
                                }
                            }
                        }
                        break;
                    }
                    default:
                        throw new NotImplementedException();
                }

                // Register in cache
                moduleHandleCache.Add(handle, module);
                return module;
            }
        }

        unsafe internal SharpLangType ReadSignature(ISharpLangGenericContext context, BlobReader signatureReader)
        {
            var signatureTypeCode = signatureReader.ReadSignatureTypeCode();

            switch (signatureTypeCode)
            {
                #region Primitive types
                case SignatureTypeCode.Void:
                    return (SharpLangType)typeof(void);
                case SignatureTypeCode.Boolean:
                    return (SharpLangType)typeof(bool);
                case SignatureTypeCode.Char:
                    return (SharpLangType)typeof(char);
                case SignatureTypeCode.Byte:
                    return (SharpLangType)typeof(byte);
                case SignatureTypeCode.SByte:
                    return (SharpLangType)typeof(sbyte);
                case SignatureTypeCode.UInt16:
                    return (SharpLangType)typeof(ushort);
                case SignatureTypeCode.Int16:
                    return (SharpLangType)typeof(short);
                case SignatureTypeCode.UInt32:
                    return (SharpLangType)typeof(uint);
                case SignatureTypeCode.Int32:
                    return (SharpLangType)typeof(int);
                case SignatureTypeCode.UInt64:
                    return (SharpLangType)typeof(ulong);
                case SignatureTypeCode.Int64:
                    return (SharpLangType)typeof(long);
                case SignatureTypeCode.Single:
                    return (SharpLangType)typeof(float);
                case SignatureTypeCode.Double:
                    return (SharpLangType)typeof(double);
                case SignatureTypeCode.UIntPtr:
                    return (SharpLangType)typeof(UIntPtr);
                case SignatureTypeCode.IntPtr:
                    return (SharpLangType)typeof(IntPtr);
                case SignatureTypeCode.Object:
                    return (SharpLangType)typeof(object);
                case SignatureTypeCode.String:
                    return (SharpLangType)typeof(string);
                case SignatureTypeCode.TypedReference:
                    return (SharpLangType)typeof(TypedReference);
                #endregion
                case SignatureTypeCode.TypeHandle:
                    return ResolveTypeHandle(context, signatureReader.ReadTypeHandle());
                case SignatureTypeCode.Pointer:
                    return ResolveElementType(null, ReadSignature(context, signatureReader), SharpLangEEType.Kind.Pointer);
                case SignatureTypeCode.ByReference:
                    return ResolveElementType(null, ReadSignature(context, signatureReader), SharpLangEEType.Kind.ByRef);
                case SignatureTypeCode.SZArray:
                    return ResolveElementType(null, ReadSignature(context, signatureReader), SharpLangEEType.Kind.Array);
                case SignatureTypeCode.Array:
                {
                    var elementType = ReadSignature(context, signatureReader);

                    // Read ArrayShape
                    var rank = signatureReader.ReadCompressedInteger();
                    var numSizes = signatureReader.ReadCompressedInteger();
                    for (int i = 0; i < numSizes; ++i)
                        signatureReader.ReadCompressedInteger();
                    var numLoBounds = signatureReader.ReadCompressedInteger();
                    for (int i = 0; i < numSizes; ++i)
                        signatureReader.ReadCompressedSignedInteger();

                    return ResolveElementType(null, elementType, SharpLangEEType.Kind.Array);
                }
                case SignatureTypeCode.GenericTypeInstance:
                {
                    var isValueType = signatureReader.ReadByte();
                    var genericTypeDefinition = (SharpLangTypeDefinition)ResolveTypeHandle(context, signatureReader.ReadTypeHandle());
                    var genericArgumentCount = signatureReader.ReadCompressedInteger();
                    var genericArguments = new SharpLangType[genericArgumentCount];
                    for (int i = 0; i < genericArgumentCount; ++i)
                        genericArguments[i] = ReadSignature(context, signatureReader);

                    return ResolveGenericType(null, genericTypeDefinition, genericArguments);
                }
                case SignatureTypeCode.GenericTypeParameter:
                {
                    if (context == null)
                        throw new InvalidOperationException();

                    var methodInfoContext = context as SharpLangMethodInfo;
                    if (methodInfoContext != null)
                        context = (SharpLangType)((SharpLangMethodInfo)context).DeclaringType;

                    var index = signatureReader.ReadCompressedInteger();

                    var typeDefinitionContext = context as SharpLangTypeDefinition;
                    if (typeDefinitionContext != null)
                    {
                        var genericParameters = typeDefinitionContext.InternalGetGenericParameters();
                        return genericParameters[index];
                    }

                    var typeGenericContext = context as SharpLangTypeGeneric;
                    if (typeGenericContext != null)
                    {
                        var genericArguments = typeGenericContext.InternalArguments;
                        return genericArguments[index];
                    }

                    // Not sure yet what other cases could happen here...
                    throw new NotSupportedException();
                }
                case SignatureTypeCode.GenericMethodParameter:
                {
                    if (context == null)
                        throw new InvalidOperationException();

                    var index = signatureReader.ReadCompressedInteger();

                    var methodInfoContext = context as SharpLangMethodInfo;
                    if (methodInfoContext != null)
                    {
                        var genericParameters = methodInfoContext.InternalGetGenericParameters();
                        return genericParameters[index];
                    }

                    // Not sure yet what other cases could happen here...
                    throw new NotSupportedException();
                }
                case SignatureTypeCode.OptionalModifier:
                case SignatureTypeCode.RequiredModifier:
                case SignatureTypeCode.Pinned:
                case SignatureTypeCode.Sentinel:
                default:
                    throw new NotImplementedException();
            }
        }

        internal unsafe SharpLangType ResolveTypeHandle(ISharpLangGenericContext context, Handle handle)
        {
            switch (handle.Kind)
            {
                case HandleKind.TypeDefinition:
                    return ResolveTypeDef(null, (TypeDefinitionHandle)handle);
                case HandleKind.TypeReference:
                {
                    var typeReference = MetadataReader.GetTypeReference((TypeReferenceHandle)handle);
                    var module = ResolveModule(typeReference.ResolutionScope);
                    if (module == null)
                        throw new InvalidOperationException("Could not resolve module");
                    return module.ResolveType(MetadataReader.GetString(typeReference.Namespace), MetadataReader.GetString(typeReference.Name));
                }
                case HandleKind.TypeSpecification:
                {
                    var typeSpecification = MetadataReader.GetTypeSpecification((TypeSpecificationHandle)handle);
                    var signatureReader = MetadataReader.GetBlobReader(typeSpecification.Signature);

                    return ReadSignature(context, signatureReader);
                }
                case HandleKind.GenericParameter:
                default:
                    throw new NotImplementedException();
            }
        }

        internal unsafe SharpLangTypeDefinition ResolveTypeDef(SharpLangEEType* eeType, TypeDefinitionHandle typeDefHandle)
        {
            var typeDef = new SharpLangEETypeDefinition(this, typeDefHandle);

            lock (SystemTypeLock)
            {
                // Check if type has already been instantiated
                SharpLangTypeDefinition sharpLangType;
                if (typeDefinitions.TryGetValue(typeDef, out sharpLangType))
                    return sharpLangType;

                if (eeType == null)
                {
                    var sharpLangTypeSearchKey = new SharpLangEETypeComparer.SharpLangTypeSearchKey
                    {
                        Kind = SharpLangEEType.Kind.TypeDef,
                        TypeDefinition = typeDef,
                    };
                    var typeIndex = SharpLangEETypeComparer.BinarySearch(types, ref sharpLangTypeSearchKey);
                    if (typeIndex >= 0)
                        eeType = types[typeIndex];
                }

                sharpLangType = new SharpLangTypeDefinition(eeType, typeDef.Module, typeDef.Handle);
                typeDefinitions.Add(typeDef, sharpLangType);

                if (eeType != null)
                    eeType->CachedTypeField = (IntPtr)SharpLangHelper.GetObjectPointer(sharpLangType);

                return sharpLangType;
            }
        }

        internal static unsafe SharpLangTypeGeneric ResolveGenericType(SharpLangEEType* eeType, SharpLangTypeDefinition genericTypeDefinition, SharpLangType[] genericArguments)
        {
            var genericTypeKey = new GenericKey(genericTypeDefinition, genericArguments);

            lock (SystemTypeLock)
            {
                SharpLangTypeGeneric sharpLangType;
                if (genericTypes.TryGetValue(genericTypeKey, out sharpLangType))
                    return sharpLangType;

                if (eeType == null)
                {
                    var sharpLangTypeSearchKey = new SharpLangEETypeComparer.SharpLangTypeSearchKey
                    {
                        Kind = SharpLangEEType.Kind.Generics,
                        TypeDefinition = new SharpLangEETypeDefinition
                        {
                            Module = genericTypeDefinition.InternalModule,
                            Handle = genericTypeDefinition.InternalHandle,
                        },
                        GenericArguments = genericArguments,
                    };
                    var typeIndex = SharpLangEETypeComparer.BinarySearch(types, ref sharpLangTypeSearchKey);
                    if (typeIndex >= 0)
                        eeType = types[typeIndex];
                }

                sharpLangType = new SharpLangTypeGeneric(eeType, genericTypeDefinition, genericArguments);
                genericTypes.Add(genericTypeKey, sharpLangType);

                if (eeType != null)
                    eeType->CachedTypeField = (IntPtr)SharpLangHelper.GetObjectPointer(sharpLangType);

                return sharpLangType;
            }
        }

        internal unsafe static SharpLangEETypePtr ResolveEETypeFromMethodPointer(IntPtr methodPointer)
        {
            lock (SystemTypeLock)
            {
                foreach (var type in types)
                {
                    var vtableSize = type.Value->VirtualTableSize;
                    for (int i = 0; i < vtableSize; ++i)
                    {
                        var vtableMethod = (&type.Value->VirtualTable)[i];
                        if (vtableMethod == methodPointer)
                            return type;
                    }
                }
            }

            return new SharpLangEETypePtr(null);
        }

        internal unsafe static SharpLangTypeElement ResolveElementType(SharpLangEEType* eeType, SharpLangType elementType, SharpLangEEType.Kind kind)
        {
            Dictionary<SharpLangType, SharpLangTypeElement> elementTypes;
            switch (kind)
            {
                case SharpLangEEType.Kind.Array:
                    elementTypes = arrayTypes;
                    break;
                case SharpLangEEType.Kind.Pointer:
                    elementTypes = pointerTypes;
                    break;
                case SharpLangEEType.Kind.ByRef:
                    elementTypes = byRefTypes;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            lock (SystemTypeLock)
            {
                // Check if type has already been instantiated
                SharpLangTypeElement sharpLangType;
                if (elementTypes.TryGetValue(elementType, out sharpLangType))
                    return sharpLangType;

                if (eeType == null)
                {
                    var sharpLangTypeSearchKey = new SharpLangEETypeComparer.SharpLangTypeSearchKey
                    {
                        Kind = kind,
                        ElementType = elementType,
                    };
                    var typeIndex = SharpLangEETypeComparer.BinarySearch(types, ref sharpLangTypeSearchKey);
                    if (typeIndex >= 0)
                        eeType = types[typeIndex];
                }

                switch (kind)
                {
                    case SharpLangEEType.Kind.Array:
                        sharpLangType = new SharpLangTypeArray(eeType, elementType, 1);
                        break;
                    case SharpLangEEType.Kind.Pointer:
                        sharpLangType = new SharpLangTypePointer(eeType, elementType);
                        break;
                    case SharpLangEEType.Kind.ByRef:
                        sharpLangType = new SharpLangTypeByRef(eeType, elementType);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                elementTypes.Add(elementType, sharpLangType);

                if (eeType != null)
                    eeType->CachedTypeField = (IntPtr)SharpLangHelper.GetObjectPointer(sharpLangType);

                return sharpLangType;
            }
        }

        unsafe internal static SharpLangType ResolveType(SharpLangEEType* eeType)
        {
            // Check if already created
            var cachedTypeField = eeType->CachedTypeField;
            if (cachedTypeField != IntPtr.Zero)
                return (SharpLangType)SharpLangHelper.GetObjectFromPointer((void*)cachedTypeField);

            lock (SystemTypeLock)
            {
                // Check again inside the lock to avoid creation conflicts
                cachedTypeField = eeType->CachedTypeField;
                if (cachedTypeField != IntPtr.Zero)
                    return (SharpLangType)SharpLangHelper.GetObjectFromPointer((void*)cachedTypeField);

                // Create SharpLangType
                var kind = eeType->GetKind();
                if (kind == SharpLangEEType.Kind.Array || kind == SharpLangEEType.Kind.Pointer || kind == SharpLangEEType.Kind.ByRef)
                {
                    // Types with elements (ByRef, Pointer, Array)
                    var elementType = ResolveType((SharpLangEEType*)(eeType->ExtraTypeInfo - (int)kind));
                    return ResolveElementType(eeType, elementType, kind);
                }

                var typeDef = &eeType->TypeDefinition;

                if (kind == SharpLangEEType.Kind.TypeDef)
                {
                    // Normal type definition
                    return typeDef->Module.ResolveTypeDef(eeType, typeDef->Handle);
                }

                if (kind == SharpLangEEType.Kind.Generics)
                {
                    // Find generic arguments
                    var genericVTable = (SharpLangEEType**)eeType->ExtraTypeInfo;
                    int genericVTableCount = 0;

                    // First count them
                    for (var genericVTableIt = genericVTable; *genericVTableIt != null; ++genericVTableIt)
                    {
                        genericVTableCount++;
                    }

                    // Then build the array
                    var genericArguments = new SharpLangType[genericVTableCount];
                    for (int i = 0; i < genericVTableCount; ++i)
                    {
                        genericArguments[i] = ResolveType(*genericVTable++);
                    }

                    // TODO: Build dependent types (generic type def + generic arguments) lazily could make initialization faster
                    return ResolveGenericType(eeType, typeDef->Module.ResolveTypeDef(null, typeDef->Handle), genericArguments);
                }

                throw new InvalidOperationException(string.Format("Unknown type kind: {0}", kind));
            }
        }

        string GetAssemblyQualifiedName(AssemblyDefinition assemblyDefinition)
        {
            // TODO: Should move that to future SharpLangAssembly type
            var assemblyName = MetadataReader.GetString(assemblyDefinition.Name);
            var assemblyVersion = assemblyDefinition.Version;
            var assemblyCulture = MetadataReader.GetString(assemblyDefinition.Culture);
            if (string.IsNullOrEmpty(assemblyCulture))
                assemblyCulture = "neutral";

            var assemblyPublicKeyBytes = MetadataReader.GetBlobBytes(assemblyDefinition.PublicKey);
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
                    algorithm = new SHA1Managed();
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

        struct GenericKey : IEquatable<GenericKey>
        {
            public readonly SharpLangType TypeDefinition;
            public readonly SharpLangType[] GenericArguments;

            public GenericKey(SharpLangType typeDefinition, SharpLangType[] genericArguments)
            {
                TypeDefinition = typeDefinition;
                GenericArguments = genericArguments;
            }

            public bool Equals(GenericKey other)
            {
                return TypeDefinition.Equals(other.TypeDefinition) && GenericArguments.Equals(other.GenericArguments);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                return obj is GenericKey && Equals((GenericKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = TypeDefinition.GetHashCode() * 397;
                    hashCode = hashCode * 31 + GenericArguments.Length.GetHashCode();
                    foreach (var genericArgument in GenericArguments)
                    {
                        hashCode = hashCode * 31 + genericArgument.GetHashCode();
                    }

                    return hashCode;
                }
            }
        }
    }
}