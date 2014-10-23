using Mono.Cecil;

namespace SharpLang.CompilerServices
{
    class FunctionSignature
    {
        public FunctionSignature(Type returnType, Type[] parameterTypes, MethodCallingConvention callingConvention, PInvokeInfo pinvoke)
        {
            ReturnType = returnType;
            ParameterTypes = parameterTypes;
            CallingConvention = callingConvention;
            PInvokeInfo = pinvoke;
        }

        /// <summary>
        /// Gets the return type.
        /// </summary>
        /// <value>
        /// The return type.
        /// </value>
        public Type ReturnType { get; private set; }

        /// <summary>
        /// Gets the parameter types.
        /// </summary>
        /// <value>
        /// The parameter types.
        /// </value>
        public Type[] ParameterTypes { get; private set; }

        /// <summary>
        /// Gets the calling convention.
        /// </summary>
        /// <value>
        /// The calling convention.
        /// </value>
        public MethodCallingConvention CallingConvention { get; private set; }

        public PInvokeInfo PInvokeInfo { get; private set; }
    }
}