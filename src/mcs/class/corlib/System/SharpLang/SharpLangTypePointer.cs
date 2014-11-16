namespace System
{
    /// <summary>
    /// Shared <see cref="Type"/> implementation for pointer types.
    /// </summary>
    class SharpLangTypePointer : SharpLangTypeElement
    {
        unsafe public SharpLangTypePointer(SharpLangEEType* eeType, SharpLangType elementType) : base(eeType, elementType)
        {
        }

        protected override string NameSuffix
        {
            get { return "*"; }
        }

        protected override bool IsPointerImpl()
        {
            return true;
        }
    }
}