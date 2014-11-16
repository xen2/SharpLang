namespace System
{
    /// <summary>
    /// Shared <see cref="Type"/> implementation for byref types.
    /// </summary>
    class SharpLangTypeByRef : SharpLangTypeElement
    {
        unsafe public SharpLangTypeByRef(SharpLangEEType* eeType, SharpLangType elementType) : base(eeType, elementType)
        {
        }

        protected override string NameSuffix
        {
            get { return "&"; }
        }

        protected override bool IsByRefImpl()
        {
            return true;
        }
    }
}