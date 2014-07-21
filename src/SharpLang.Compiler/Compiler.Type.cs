using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using SharpLang.CompilerServices.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public partial class Compiler
    {
        /// <summary>
        /// Gets the specified type.
        /// </summary>
        /// <param name="typeReference">The type reference.</param>
        /// <returns></returns>
        Type GetType(TypeReference typeReference)
        {
            Type type;
            if (types.TryGetValue(typeReference, out type))
                return type;

            type = BuildType(typeReference);

            types.Add(typeReference, type);

            return type;
        }
        
        /// <summary>
        /// Compiles the specified type.
        /// </summary>
        /// <param name="typeReference">The type reference.</param>
        /// <returns></returns>
        private Type CreateType(TypeReference typeReference)
        {
            Type type;
            if (types.TryGetValue(typeReference, out type))
                return type;

            type = BuildType(typeReference);

            types.Add(typeReference, type);

            return type;
        }

        /// <summary>
        /// Internal helper to actually builds the type.
        /// </summary>
        /// <param name="typeReference">The type definition.</param>
        /// <returns></returns>
        private Type BuildType(TypeReference typeReference)
        {
            if (typeReference.ContainsGenericParameter())
                return null;

            TypeRef dataType;
            StackValueType stackType;

            switch (typeReference.MetadataType)
            {
                case MetadataType.Pointer:
                {
                    var type = GetType(((PointerType)typeReference).ElementType);
                    // Special case: void*
                    if (LLVM.VoidTypeInContext(context) == type.DataType)
                        dataType = intPtrType;
                    else
                        dataType = LLVM.PointerType(type.DataType, 0);
                    stackType = StackValueType.NativeInt;
                    break;
                }
                case MetadataType.ByReference:
                {
                    var type = GetType(((ByReferenceType)typeReference).ElementType);
                    dataType = LLVM.PointerType(type.DefaultType, 0);
                    stackType = StackValueType.Reference;
                    break;
                }
                case MetadataType.RequiredModifier:
                    // TODO: Add support for this feature
                    return GetType(((RequiredModifierType)typeReference).ElementType);
                case MetadataType.Void:
                    dataType = LLVM.VoidTypeInContext(context);
                    stackType = StackValueType.Unknown;
                    break;
                case MetadataType.Boolean:
                    dataType = LLVM.Int1TypeInContext(context);
                    stackType = StackValueType.Int32;
                    break;
                case MetadataType.Single:
                    dataType = LLVM.FloatTypeInContext(context);
                    stackType = StackValueType.Float;
                    break;
                case MetadataType.Double:
                    dataType = LLVM.DoubleTypeInContext(context);
                    stackType = StackValueType.Float;
                    break;
                case MetadataType.Char:
                case MetadataType.Byte:
                case MetadataType.SByte:
                    dataType = LLVM.Int8TypeInContext(context);
                    stackType = StackValueType.Int32;
                    break;
                case MetadataType.Int16:
                case MetadataType.UInt16:
                    dataType = LLVM.Int16TypeInContext(context);
                    stackType = StackValueType.Int32;
                    break;
                case MetadataType.Int32:
                case MetadataType.UInt32:
                    dataType = int32Type;
                    stackType = StackValueType.Int32;
                    break;
                case MetadataType.Int64:
                case MetadataType.UInt64:
                    dataType = int64Type;
                    stackType = StackValueType.Int64;
                    break;
                case MetadataType.UIntPtr:
                case MetadataType.IntPtr:
                    dataType = intPtrType;
                    stackType = StackValueType.NativeInt;
                    break;
                case MetadataType.Array:
                case MetadataType.String:
                case MetadataType.TypedByReference:
                case MetadataType.GenericInstance:
                case MetadataType.ValueType:
                case MetadataType.Class:
                case MetadataType.Object:
                {
                    // When resolved, void becomes a real type
                    if (typeReference.FullName == typeof(void).FullName)
                    {
                        goto case MetadataType.Void;
                    }

                    var typeDefinition = typeReference.Resolve();
                    if (typeDefinition.IsValueType && typeDefinition.IsEnum)
                    {
                        // Special case: enum
                        var enumUnderlyingType = GetType(typeDefinition.GetEnumUnderlyingType());
                        dataType = enumUnderlyingType.DataType;
                        stackType = enumUnderlyingType.StackType;
                    }
                    else
                    {
                        dataType = LLVM.StructCreateNamed(context, typeReference.FullName);
                        stackType = typeReference.MetadataType != MetadataType.Array && typeDefinition.IsValueType ? StackValueType.Value : StackValueType.Object;
                    }

                    break;
                }
                default:
                    throw new NotImplementedException();
            }

            // Create class version (boxed version with VTable)
            var boxedType = LLVM.StructCreateNamed(context, typeReference.FullName);

            var result = new Type(typeReference, dataType, boxedType, stackType);

            bool isLocal = typeReference.Resolve().Module.Assembly == assembly;
            if (isLocal)
                EmitType(result);

            return result;
        }

        private void EmitType(Type type)
        {
            if (type.IsLocal)
                return;

            type.IsLocal = true;
            classesToGenerate.Enqueue(type);
        }
    }
}