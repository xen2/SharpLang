using SharpLLVM;

namespace SharpLang.CompilerServices
{
    /// <summary>
    /// A value on the IL stack.
    /// </summary>
    class StackValue
    {
        public StackValue(StackValueType stackType, Type type, ValueRef value)
        {
            StackType = stackType;
            Type = type;
            Value = value;
        }

        /// <summary>
        /// Gets the type of the stack value.
        /// </summary>
        /// <value>
        /// The type of the stack value.
        /// </value>
        public StackValueType StackType { get; private set; }

        public ValueRef Value { get; private set; }

        public Type Type { get; private set; }
    }
}