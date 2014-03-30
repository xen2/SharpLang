namespace SharpLang.CompilerServices
{
    /// <summary>
    /// The type of value on the stack.
    /// </summary>
    enum StackValueType
    {
        Unknown,
        Int32,
        Int64,
        NativeInt,
        Float,
        Object,
        Reference,
        Value,
        Pointer,
    }
}