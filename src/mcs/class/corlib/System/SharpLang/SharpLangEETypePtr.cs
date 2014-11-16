namespace System
{
    unsafe struct SharpLangEETypePtr
    {
        public SharpLangEEType* Value;

        public SharpLangEETypePtr(SharpLangEEType* value)
        {
            Value = value;
        }

        public static implicit operator SharpLangEETypePtr(SharpLangEEType* value)
        {
            return new SharpLangEETypePtr(value);
        }

        public static implicit operator SharpLangEEType*(SharpLangEETypePtr eeType)
        {
            return eeType.Value;
        }
    }
}