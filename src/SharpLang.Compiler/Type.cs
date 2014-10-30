using System;
using System.Collections.Generic;
using Mono.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    /// <summary>
    /// Describes a type (could be a <see cref="Class"/>, a delegate, a pointer to a <see cref="Type"/>, etc...).
    /// </summary>
    class Type
    {
        internal Class Class;
        internal bool IsLocal;

        public Type(TypeReference typeReference, TypeDefinition typeDefinition, TypeRef dataType, TypeRef valueType, TypeRef objectType, StackValueType stackType)
        {
            TypeReferenceCecil = typeReference;
            TypeDefinitionCecil = typeDefinition;
            DataTypeLLVM = dataType;
            ObjectTypeLLVM = objectType;
            StackType = stackType;
            ValueTypeLLVM = valueType;
            DefaultTypeLLVM = stackType == StackValueType.Object ? LLVM.PointerType(ObjectTypeLLVM, 0) : DataTypeLLVM;

            switch (stackType)
            {
                case StackValueType.NativeInt:
                    TypeOnStackLLVM = LLVM.PointerType(LLVM.Int8TypeInContext(LLVM.GetTypeContext(dataType)), 0);
                    break;
                case StackValueType.Float:
                    TypeOnStackLLVM = LLVM.DoubleTypeInContext(LLVM.GetTypeContext(dataType));
                    break;
                case StackValueType.Int32:
                    TypeOnStackLLVM = LLVM.Int32TypeInContext(LLVM.GetTypeContext(dataType));
                    break;
                case StackValueType.Int64:
                    TypeOnStackLLVM = LLVM.Int64TypeInContext(LLVM.GetTypeContext(dataType));
                    break;
                case StackValueType.Value:
                case StackValueType.Object:
                case StackValueType.Reference:
                    TypeOnStackLLVM = DefaultTypeLLVM;
                    break;
            }
        }

        /// <summary>
        /// Gets the LLVM default type.
        /// </summary>
        /// <value>
        /// The LLVM default type.
        /// </value>
        public TypeRef DefaultTypeLLVM { get; private set; }

        /// <summary>
        /// Gets the LLVM object type (object header and <see cref="DataTypeLLVM"/>).
        /// </summary>
        /// <value>
        /// The LLVM boxed type (object header and <see cref="DataTypeLLVM"/>).
        /// </value>
        public TypeRef ObjectTypeLLVM { get; private set; }

        /// <summary>
        /// Gets the LLVM data type.
        /// </summary>
        /// <value>
        /// The LLVM data type (fields).
        /// </value>
        public TypeRef DataTypeLLVM { get; private set; }

        /// <summary>
        /// Gets the LLVM value type.
        /// </summary>
        /// <value>
        /// The LLVM value type (fields).
        /// </value>
        public TypeRef ValueTypeLLVM { get; private set; }

        /// <summary>
        /// Gets the LLVM type when on the stack.
        /// </summary>
        /// <value>
        /// The LLVM type when on the stack.
        /// </value>
        public TypeRef TypeOnStackLLVM { get; private set; }

        /// <summary>
        /// Gets the linkage to use for this type.
        /// </summary>
        /// <value>
        /// The linkage type to use for this type.
        /// </value>
        public Linkage Linkage { get; set; }

        public TypeReference TypeReferenceCecil { get; private set; }
        public TypeDefinition TypeDefinitionCecil { get; private set; }

        public StackValueType StackType { get; private set; }

        public Dictionary<FieldDefinition, Field> Fields { get; set; }

        public TypeState State { get; set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return TypeReferenceCecil.ToString();
        }
    }
}