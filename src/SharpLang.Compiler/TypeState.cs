using System;

namespace SharpLang.CompilerServices
{
    /// <summary>
    /// Describes how much info has been generated in a <see cref="Type"/>.
    /// </summary>
    enum TypeState
    {
        /// <summary>
        /// Type exists but <see cref="ValueType"/> might be opaque.
        /// </summary>
        Opaque,

        /// <summary>
        /// <see cref="Type.DefaultTypeLLVM"/> will be non opaque.
        /// </summary>
        StackComplete,

        /// <summary>
        /// <see cref="Type.ValueTypeLLVM"/> will be non opaque, and <see cref="Type.Fields"/> can be used.
        /// </summary>
        TypeComplete,

        /// <summary>
        /// <see cref="Type.ObjectTypeLLVM"/> will be valid.
        /// </summary>
        VTableEmitted,
    }
}