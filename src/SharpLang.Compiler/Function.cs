using Mono.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    class Function
    {
        public Function(Type declaringType, MethodReference methodReference, TypeRef functionType, ValueRef generatedValue, Type returnType, Type[] parameterTypes)
        {
            Signature = new FunctionSignature(returnType, parameterTypes);
            DeclaringType = declaringType;
            MethodReference = methodReference;
            FunctionType = functionType;
            GeneratedValue = generatedValue;
            VirtualSlot = -1;
        }

        /// <summary>
        /// Gets the declaring class.
        /// </summary>
        /// <value>
        /// The declaring class.
        /// </value>
        public Type DeclaringType { get; private set; }

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
        public Type ReturnType { get { return Signature.ReturnType; } }

        /// <summary>
        /// Gets the parameter types.
        /// </summary>
        /// <value>
        /// The parameter types.
        /// </value>
        public Type[] ParameterTypes { get { return Signature.ParameterTypes; } }

        public FunctionSignature Signature { get; private set; }

        public int VirtualSlot { get; set; }

        public override string ToString()
        {
            return MethodReference.ToString();
        }
    }
}