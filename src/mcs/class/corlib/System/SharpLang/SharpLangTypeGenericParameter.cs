using System.Reflection.Metadata;

namespace System
{
    /// <summary>
    /// Shared <see cref="Type"/> implementation for generic parameters.
    /// </summary>
    class SharpLangTypeGenericParameter : SharpLangType
    {
        internal SharpLangModule InternalModule;
        internal GenericParameterHandle InternalHandle;

        unsafe public SharpLangTypeGenericParameter(SharpLangModule module, GenericParameterHandle handle) : base(null)
        {
            this.InternalModule = module;
            this.InternalHandle = handle;
        }

        public override int GenericParameterPosition
        {
            get
            {
                var genericParameter = InternalModule.MetadataReader.GetGenericParameter(InternalHandle);
                return genericParameter.Index;
            }
        }

        public override string Name
        {
            get
            {
                var genericParameter = InternalModule.MetadataReader.GetGenericParameter(InternalHandle);
                return InternalModule.MetadataReader.GetString(genericParameter.Name);
            }
        }

        public override string FullName
        {
            get { return null; }
        }

        public override string Namespace
        {
            get { throw new NotImplementedException(); }
        }

        public override Type DeclaringType
        {
            get
            {
                var genericParameter = InternalModule.MetadataReader.GetGenericParameter(InternalHandle);

                // TODO: Resolve genericParameter.Parent
                throw new NotImplementedException();
            }
        }
    }
}