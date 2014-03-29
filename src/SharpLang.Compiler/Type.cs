using Mono.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    /// <summary>
    /// Describes a type (could be a <see cref="Class"/>, a delegate, a pointer to a <see cref="Type"/>, etc...).
    /// </summary>
    class Type
    {
        public Type(TypeReference typeReference, TypeRef generatedType)
        {
            TypeReference = typeReference;
            GeneratedType = generatedType;
        }

        /// <summary>
        /// Gets or sets the LLVM generated type.
        /// </summary>
        /// <value>
        /// The LLVM generated type.
        /// </value>
        public TypeRef GeneratedType { get; private set; }

        public TypeReference TypeReference { get; private set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return TypeReference.ToString();
        }
    }
}