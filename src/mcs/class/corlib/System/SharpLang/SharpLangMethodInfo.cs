// Copyright (c) 2014 SharpLang - Virgile Bello

using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;

namespace System
{
    // Future RuntimeMethodHandle (WIP)
    struct RuntimeMethodHandle2
    {
        internal SharpLangModule Module;
        internal MethodDefinitionHandle Token;
        internal SharpLangEETypePtr DeclaringType;
        internal ushort Slot;
    }

    public class SharpLangMethodInfo : MethodInfo, ISharpLangGenericContext
    {
        private static readonly SharpLangTypeGenericParameter[] EmptyGenericParameters = new SharpLangTypeGenericParameter[0];
        private static readonly Dictionary<SharpLangMethodInfo, SharpLangTypeGenericParameter[]> instantiatedGenericParameters = new Dictionary<SharpLangMethodInfo, SharpLangTypeGenericParameter[]>();

        private readonly SharpLangModule module;
        private readonly MethodDefinitionHandle definitionHandle;
        private readonly SharpLangType declaringType;

        public override Type DeclaringType
        {
            get { return declaringType; }
        }

        public override string Name
        {
            get
            {
                var methodDefinition = module.MetadataReader.GetMethodDefinition(definitionHandle);
                return module.MetadataReader.GetString(methodDefinition.Name);
            }
        }

        public override Type ReflectedType
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            throw new NotImplementedException();
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            throw new NotImplementedException();
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            throw new NotImplementedException();
        }

        public override MethodImplAttributes GetMethodImplementationFlags()
        {
            var methodDefinition = module.MetadataReader.GetMethodDefinition(definitionHandle);
            return methodDefinition.ImplAttributes;
        }

        public override ParameterInfo[] GetParameters()
        {
            var methodDefinition = module.MetadataReader.GetMethodDefinition(definitionHandle);
            var signatureReader = module.MetadataReader.GetBlobReader(methodDefinition.Signature);

            var callingConvention = (CallingConventions)signatureReader.ReadByte();
            if (((byte)callingConvention & 0x10) != 0) // Generic
                signatureReader.ReadCompressedInteger();

            var paramCount = signatureReader.ReadCompressedInteger();

            var returnType = module.ReadSignature(this, signatureReader);

            for (int i = 0; i < paramCount; ++i)
            {
                var paramType = module.ReadSignature(this, signatureReader);
            }

            throw new NotImplementedException();
        }

        public override Type ReturnType
        {
            get { throw new NotImplementedException(); }
        }

        public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override RuntimeMethodHandle MethodHandle
        {
            get { throw new NotImplementedException(); }
        }

        public override MethodAttributes Attributes
        {
            get
            {
                var methodDefinition = module.MetadataReader.GetMethodDefinition(definitionHandle);
                return methodDefinition.Attributes;
            }
        }

        public override MethodInfo GetBaseDefinition()
        {
            throw new NotImplementedException();
        }

        public override ICustomAttributeProvider ReturnTypeCustomAttributes
        {
            get { throw new NotImplementedException(); }
        }

        internal SharpLangTypeGenericParameter[] InternalGetGenericParameters()
        {
            lock (instantiatedGenericParameters)
            {
                SharpLangTypeGenericParameter[] genericParameters;
                if (!instantiatedGenericParameters.TryGetValue(this, out genericParameters))
                {
                    // First time, create generic parameters
                    var methodDefinition = module.MetadataReader.GetMethodDefinition(definitionHandle);
                    var metadataGenericParameters = methodDefinition.GetGenericParameters();

                    // Early exit if empty
                    if (metadataGenericParameters.Count == 0)
                        return EmptyGenericParameters;

                    genericParameters = new SharpLangTypeGenericParameter[metadataGenericParameters.Count];
                    for (int i = 0; i < genericParameters.Length; ++i)
                    {
                        genericParameters[i] = new SharpLangTypeGenericParameter(module, metadataGenericParameters[i]);
                    }

                    instantiatedGenericParameters.Add(this, genericParameters);
                }
                return genericParameters;
            }
        }
    }
}