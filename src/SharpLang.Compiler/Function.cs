using Mono.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    class Function
    {
        public Function(MethodDefinition methodDefinition, ValueRef generatedValue, Type returnType, Type[] parameterTypes)
        {
            MethodDefinition = methodDefinition;
            GeneratedValue = generatedValue;
            ReturnType = returnType;
            ParameterTypes = parameterTypes;
            VirtualSlot = -1;
        }

        public MethodDefinition MethodDefinition { get; private set; }

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

        public int VirtualSlot { get; set; }
    }
}