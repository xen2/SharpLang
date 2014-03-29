using SharpLLVM;

namespace SharpLang.CompilerServices
{
    class StackValue
    {
        public StackValue(StackValueType type, ValueRef value)
        {
            Type = type;
            Value = value;
        }

        public StackValueType Type { get; private set; }

        public ValueRef Value { get; private set; }
    }
}