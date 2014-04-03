using Mono.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    class Function
    {
        public Function(MethodReference methodReference, ValueRef generatedValue, Type returnType, Type[] parameterTypes)
        {
            MethodReference = methodReference;
            GeneratedValue = generatedValue;
            ReturnType = returnType;
            ParameterTypes = parameterTypes;
        }

        public MethodReference MethodReference { get; private set; }

        /// <summary>
        /// Gets or sets the LLVM generated value.
        /// </summary>
        /// <value>
        /// The LLVM generated value.
        /// </value>
        public ValueRef GeneratedValue { get; internal set; }

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