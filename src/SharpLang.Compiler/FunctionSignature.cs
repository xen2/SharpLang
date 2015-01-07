using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace SharpLang.CompilerServices
{
    class FunctionSignature
    {
        public FunctionSignature(IABI abi, Type returnType, Type[] parameterTypes, MethodCallingConvention callingConvention, PInvokeInfo pinvoke)
        {
            ReturnType = new FunctionParameterType(abi, returnType);
            ParameterTypes = new FunctionParameterType[parameterTypes.Length];
            for (int index = 0; index < parameterTypes.Length; index++)
                ParameterTypes[index] = new FunctionParameterType(abi, parameterTypes[index]);

            CallingConvention = callingConvention;
            PInvokeInfo = pinvoke;
        }

        /// <summary>
        /// Gets the return type.
        /// </summary>
        /// <value>
        /// The return type.
        /// </value>
        public FunctionParameterType ReturnType { get; private set; }

        /// <summary>
        /// Gets the parameter types.
        /// </summary>
        /// <value>
        /// The parameter types.
        /// </value>
        public FunctionParameterType[] ParameterTypes { get; private set; }

        /// <summary>
        /// Gets the calling convention.
        /// </summary>
        /// <value>
        /// The calling convention.
        /// </value>
        public MethodCallingConvention CallingConvention { get; private set; }

        public PInvokeInfo PInvokeInfo { get; private set; }

        public int GetParameterIndexForThis()
        {
            return ReturnType.ABIParameterInfo.Kind == ABIParameterInfoKind.Indirect ? 1 : 0;
        }
    }
}