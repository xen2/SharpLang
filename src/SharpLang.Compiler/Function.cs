using System.Linq;
using Mono.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    class Function
    {
        public Function(Type declaringType, MethodReference methodReference, TypeRef functionType, ValueRef generatedValue, FunctionSignature signature)
        {
            Signature = signature;
            DeclaringType = declaringType;
            MethodReference = methodReference;
            FunctionType = functionType;
            GeneratedValue = generatedValue;
            VirtualSlot = -1;

            MethodDefinition = methodReference.Resolve();

            ParameterTypes = signature.ParameterTypes.Select(x => x.Type).ToArray();

            // Generate function type when being called from vtable/IMT (if it applies)
            // If declaring type is a value type, needs to unbox "this" for virtual method
            if (DeclaringType.TypeDefinitionCecil.IsValueType
                && (MethodDefinition.Attributes & MethodAttributes.Virtual) != 0)
            {
                bool hasStructValueReturn = signature.ReturnType.ABIParameterInfo.Kind == ABIParameterInfoKind.Indirect;

                // Create function type with boxed "this"
                var argumentCount = LLVM.CountParamTypes(FunctionType);
                var argumentTypes = new TypeRef[argumentCount];
                LLVM.GetParamTypes(FunctionType, argumentTypes);
                // Change first type to boxed "this"
                var thisIndex = hasStructValueReturn ? 1 : 0;
                argumentTypes[thisIndex] = LLVM.PointerType(DeclaringType.ObjectTypeLLVM, 0);
                VirtualFunctionType = LLVM.FunctionType(LLVM.GetReturnType(FunctionType), argumentTypes, LLVM.IsFunctionVarArg(FunctionType));
            }
            else
            {
                VirtualFunctionType = FunctionType;
            }
        }

        /// <summary>
        /// Gets the declaring class.
        /// </summary>
        /// <value>
        /// The declaring class.
        /// </value>
        public Type DeclaringType { get; private set; }

        public MethodReference MethodReference { get; private set; }

        public MethodDefinition MethodDefinition { get; private set; }

        /// <summary>
        /// Gets the LLVM function type.
        /// </summary>
        /// <value>
        /// The LLVM function type.
        /// </value>
        public TypeRef FunctionType { get; private set; }

        /// <summary>
        /// Gets the LLVM function type when stored in vtable.
        /// </summary>
        /// <value>
        /// The LLVM function type when stored in vtable.
        /// </value>
        public TypeRef VirtualFunctionType { get; private set; }

        /// <summary>
        /// Gets the LLVM generated value.
        /// </summary>
        /// <value>
        /// The LLVM generated value.
        /// </value>
        public ValueRef GeneratedValue { get; internal set; }

        /// <summary>
        /// Gets or sets the LLVM unbox trampoline (suitable for virtual call).
        /// </summary>
        /// <value>
        /// The LLVM unbox trampoline (suitable for virtual call).
        /// </value>
        public ValueRef UnboxTrampoline { get; internal set; }

        /// <summary>
        /// Gets the return type.
        /// </summary>
        /// <value>
        /// The return type.
        /// </value>
        public Type ReturnType { get { return Signature.ReturnType.Type; } }

        /// <summary>
        /// Gets the parameter types.
        /// </summary>
        /// <value>
        /// The parameter types.
        /// </value>
        public Type[] ParameterTypes { get; private set; }

        public FunctionSignature Signature { get; private set; }

        public int VirtualSlot { get; set; }

        public ValueRef InterfaceSlot { get; set; }

        public bool IsLocal { get; set; }

        public override string ToString()
        {
            return MethodReference.ToString();
        }
    }
}