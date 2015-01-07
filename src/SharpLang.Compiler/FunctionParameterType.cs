namespace SharpLang.CompilerServices
{
    struct FunctionParameterType
    {
        public readonly Type Type;
        public readonly ABIParameterInfo ABIParameterInfo;

        public FunctionParameterType(IABI abi, Type type)
        {
            Type = type;
            ABIParameterInfo = abi.GetParameterInfo(type);
        }
    }
}