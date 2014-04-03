using Mono.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    /// <summary>
    /// Describes a value type or class. It usually has fields and methods.
    /// </summary>
    class Class
    {
        public Class(TypeDefinition typeDefinition, TypeRef dataType, StackValueType stackType)
        {
            TypeDefinition = typeDefinition;
            DataType = dataType;
            Type = dataType;
            StackType = stackType;
        }

        /// <summary>
        /// Gets the LLVM type.
        /// </summary>
        /// <value>
        /// The LLVM type (object header and <see cref="DataType"/>).
        /// </value>
        public TypeRef Type { get; private set; }

        /// <summary>
        /// Gets the LLVM data type.
        /// </summary>
        /// <value>
        /// The LLVM data type (fields).
        /// </value>
        public TypeRef DataType { get; private set; }

        public TypeDefinition TypeDefinition { get; private set; }

        public StackValueType StackType { get; private set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return TypeDefinition.ToString();
        }
    }
}