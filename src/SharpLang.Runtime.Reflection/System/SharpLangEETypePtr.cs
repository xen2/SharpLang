namespace System
{
    unsafe struct SharpLangEETypePtr : IEquatable<SharpLangEETypePtr>
    {
        public readonly SharpLangEEType* Value;

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

        public bool Equals(SharpLangEETypePtr other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is SharpLangEETypePtr && Equals((SharpLangEETypePtr)obj);
        }

        public override int GetHashCode()
        {
            return ((IntPtr)Value).GetHashCode();
        }
    }
}