using SharpLLVM;

namespace SharpLang.CompilerServices
{
    /// <summary>
    /// A value on the IL stack.
    /// </summary>
    class StackValue
    {
        public StackValue(StackValueType type, ValueRef value)
        {
            Type = type;
            Value = value;
        }

        /// <summary>
        /// Gets the type of the stack value.
        /// </summary>
        /// <value>
        /// The type of the stack value.
        /// </value>
        public StackValueType Type { get; private set; }

        public ValueRef Value { get; private set; }
    }
}