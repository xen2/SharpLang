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
        /// <see cref="Type.DefaultType"/> will be non opaque.
        /// </summary>
        StackComplete,

        /// <summary>
        /// <see cref="Type.ValueType"/> will be non opaque, and <see cref="Type.Fields"/> can be used.
        /// </summary>
        TypeComplete,

        /// <summary>
        /// <see cref="Type.ObjectType"/> will be valid.
        /// </summary>
        VTableEmitted,
    }
}