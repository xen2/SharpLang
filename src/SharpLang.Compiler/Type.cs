using Mono.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    /// <summary>
    /// Describes a type (could be a <see cref="Class"/>, a delegate, a pointer to a <see cref="Type"/>, etc...).
    /// </summary>
    class Type
    {
        public Type(TypeReference typeReference, TypeRef dataType, TypeRef objectType, StackValueType stackType)
        {
            TypeReference = typeReference;
            DataType = dataType;
            ObjectType = objectType;
            StackType = stackType;
            DefaultType = stackType == StackValueType.Object ? LLVM.PointerType(ObjectType, 0) : DataType;
            StackType = stackType;
        }

        /// <summary>
        /// Gets the LLVM default type.
        /// </summary>
        /// <value>
        /// The LLVM default type.
        /// </value>
        public TypeRef DefaultType { get; private set; }

        /// <summary>
        /// Gets the LLVM object type (object header and <see cref="DataType"/>).
        /// </summary>
        /// <value>
        /// The LLVM boxed type (object header and <see cref="DataType"/>).
        /// </value>
        public TypeRef ObjectType { get; private set; }

        /// <summary>
        /// Gets the LLVM data type.
        /// </summary>
        /// <value>
        /// The LLVM data type (fields).
        /// </value>
        public TypeRef DataType { get; private set; }

        public TypeReference TypeReference { get; private set; }

        public StackValueType StackType { get; private set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return TypeReference.ToString();
        }
    }
}