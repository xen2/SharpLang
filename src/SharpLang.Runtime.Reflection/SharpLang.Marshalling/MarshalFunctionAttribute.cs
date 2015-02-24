using System;

namespace SharpLang.Marshalling
{
    [AttributeUsage(AttributeTargets.Method)]
    class MarshalFunctionAttribute : Attribute
    {
        public Type DelegateType { get; private set; }

        public MarshalFunctionAttribute(Type delegateType)
        {
            DelegateType = delegateType;
        }
    }
}