using SharpLLVM;

namespace SharpLang.CompilerServices
{
    /// <summary>
    /// Describes a type (could be a <see cref="Class"/>, a delegate, a pointer to a <see cref="Type"/>, etc...).
    /// </summary>
    class Type
    {
        public Type(TypeRef generatedType)
        {
            GeneratedType = generatedType;
        }

        /// <summary>
        /// Gets or sets the LLVM generated type.
        /// </summary>
        /// <value>
        /// The LLVM generated type.
        /// </value>
        public TypeRef GeneratedType { get; private set; }
    }
}