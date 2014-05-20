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
        public Class(TypeDefinition typeDefinition, TypeRef dataType, TypeRef objectType, StackValueType stackType)
        {
            TypeDefinition = typeDefinition;
            DataType = dataType;
            ObjectType = objectType;
            StackType = stackType;
            Type = stackType == StackValueType.Object ? LLVM.PointerType(ObjectType, 0) : DataType;
            Fields = new Dictionary<FieldDefinition, Field>();
            VirtualTable = new List<Function>();

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
        /// The LLVM type.
        /// </value>
        public TypeRef Type { get; private set; }

        /// <summary>
        /// Gets the LLVM object type (object header and <see cref="DataType"/>).
        /// </summary>
        /// <value>
        /// The LLVM boxed type (object header and <see cref="DataType"/>).
        /// </value>
        public TypeRef ObjectType { get; private set; }

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

        /// <summary>
        /// Gets the Cecil type definition.
        /// </summary>
        /// <value>
        /// The Cecil type definition.
        /// </value> 
        public TypeDefinition TypeDefinition { get; private set; }

        public StackValueType StackType { get; private set; }

        public Dictionary<FieldDefinition, Field> Fields { get; private set; }

        public List<Function> VirtualTable { get; private set; }

        /// <summary>
        /// Gets or sets the parent class.
        /// </summary>
        /// <value>
        /// The parent class.
        /// </value>
        public Class BaseType { get; internal set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return TypeDefinition.ToString();
        }
    }
}