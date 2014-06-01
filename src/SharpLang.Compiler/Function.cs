using Mono.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    class Function
    {
        public Function(MethodReference methodReference, TypeRef functionType, ValueRef generatedValue, Type returnType, Type[] parameterTypes)
        {
            MethodReference = methodReference;
            FunctionType = functionType;
            GeneratedValue = generatedValue;
            ReturnType = returnType;
            ParameterTypes = parameterTypes;
            VirtualSlot = -1;
        }

        public MethodReference MethodReference { get; private set; }

        /// <summary>
        /// Gets the LLVM function type.
        /// </summary>
        /// <value>
        /// The LLVM function type.
        /// </value>
        public TypeRef FunctionType { get; private set; }

        /// <summary>
        /// Gets the LLVM generated value.
        /// </summary>
        /// <value>
        /// The LLVM generated value.
        /// </value>
        public ValueRef GeneratedValue { get; private set; }

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