using System;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    struct ABIParameterInfo
    {
        public readonly ABIParameterInfoKind Kind;
        public readonly TypeRef CoerceType;

        public ABIParameterInfo(ABIParameterInfoKind kind)
        {
            Kind = kind;
            CoerceType = TypeRef.Empty;
        }

        public ABIParameterInfo(ABIParameterInfoKind kind, TypeRef coerceType)
        {
            if (kind != ABIParameterInfoKind.Coerced)
                throw new ArgumentException("kind");

            Kind = kind;
            CoerceType = coerceType;
        }
    }
}