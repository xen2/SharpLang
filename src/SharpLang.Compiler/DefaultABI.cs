using SharpLLVM;

namespace SharpLang.CompilerServices
{
    class DefaultABI : IABI
    {
        private readonly ContextRef context;
        private readonly TargetDataRef targetData;
        private readonly int intPtrSize;

        public DefaultABI(ContextRef context, TargetDataRef targetData)
        {
            this.context = context;
            this.targetData = targetData;

            var intPtrLLVM = LLVM.PointerType(LLVM.Int8TypeInContext(context), 0);
            intPtrSize = (int)LLVM.ABISizeOfType(targetData, intPtrLLVM);
        }

        public ABIParameterInfo GetParameterInfo(Type type)
        {
            if (type.StackType == StackValueType.Value)
            {
                // Types smaller than register size will be coerced to integer register type
                var structSize = LLVM.ABISizeOfType(targetData, type.DefaultTypeLLVM);
                if (structSize <= (ulong)intPtrSize && structSize != 0)
                {
                    return new ABIParameterInfo(ABIParameterInfoKind.Coerced, LLVM.IntTypeInContext(context, (uint)structSize * 8));
                }

                // Otherwise, fallback to passing by pointer + byval (x86) or direct (x64)
                if (intPtrSize == 8)
                    return new ABIParameterInfo(ABIParameterInfoKind.Direct);
                return new ABIParameterInfo(ABIParameterInfoKind.Indirect);
            }

            // Other types are passed by value (pointers, int32, int64, float, etc...)
            return new ABIParameterInfo(ABIParameterInfoKind.Direct);
        }
    }
} 