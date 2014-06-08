namespace SharpLang.CompilerServices
{
    class FunctionSignature
    {
        public FunctionSignature(Type returnType, Type[] parameterTypes)
        {
            ReturnType = returnType;
            ParameterTypes = parameterTypes;
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
    }
}