using System.Reflection;

namespace System
{
    /// <summary>
    /// Shared <see cref="Type"/> implementation for element types: array, byref and pointer of another type.
    /// </summary>
    abstract class SharpLangTypeElement : SharpLangType
    {
        protected SharpLangType elementType;

        unsafe protected SharpLangTypeElement(SharpLangEEType* eeType, SharpLangType elementType) : base(eeType)
        {
            this.elementType = elementType;
        }

        public override string Name
        {
            get { return elementType.Name + NameSuffix; }
        }

        public override string Namespace
        {
            get { return elementType.Namespace; }
        }

        public override Module Module
        {
            get { return elementType.Module; }
        }

        protected abstract string NameSuffix { get; }

        internal override string InternalAssemblyName
        {
            get { return elementType.InternalAssemblyName; }
        }

        public override Type GetElementType()
        {
            return elementType;
        }

        protected override bool HasElementTypeImpl()
        {
            return true;
        }
    }
}