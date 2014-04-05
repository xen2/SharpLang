using System;
using System.Collections.Generic;
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
            Fields = new Dictionary<FieldDefinition, Field>();

            switch (stackType)
            {
                case StackValueType.Int32:
                    TypeOnStack = LLVM.Int32TypeInContext(LLVM.GetTypeContext(dataType));
                    break;
                case StackValueType.Int64:
                    TypeOnStack = LLVM.Int64TypeInContext(LLVM.GetTypeContext(dataType));
                    break;
                case StackValueType.Value:
                case StackValueType.Object:
                    TypeOnStack = Type;
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Gets the LLVM type.
        /// </summary>
        /// <value>
        /// The LLVM type (object header and <see cref="DataType"/>).
        /// </value>
        public TypeRef Type { get; private set; }

        /// <summary>
        /// Gets the LLVM type when on the stack.
        /// </summary>
        /// <value>
        /// The LLVM type when on the stack.
        /// </value>
        public TypeRef TypeOnStack { get; private set; }

        /// <summary>
        /// Gets the LLVM data type.
        /// </summary>
        /// <value>
        /// The LLVM data type (fields).
        /// </value>
        public TypeRef DataType { get; private set; }

        public TypeDefinition TypeDefinition { get; private set; }

        public StackValueType StackType { get; private set; }

        public Dictionary<FieldDefinition, Field> Fields { get; private set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return TypeDefinition.ToString();
        }
    }
}