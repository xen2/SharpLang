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
        Type GetType(TypeReference typeReference, TypeState state)
        {
            Type type;
            if (!types.TryGetValue(typeReference, out type))
                type = BuildType(typeReference);

            if (type == null)
                return null;

            if ((state >= TypeState.StackComplete && type.StackType == StackValueType.Value)
                || state >= TypeState.TypeComplete)
                CompleteType(type);

            if (state >= TypeState.VTableEmitted)
                GetClass(type);

            return type;
        }

        /// <summary>
        /// Internal helper to actually builds the type.
        /// </summary>
        /// <param name="typeReference">The type definition.</param>
        /// <returns></returns>
        private Type BuildType(TypeReference typeReference)
        {
            // Open type?
            if (typeReference.ContainsGenericParameter())
                return null;

            TypeRef valueType = TypeRef.Empty;
            TypeRef dataType;
            StackValueType stackType;

            var typeDefinition = GetMethodTypeDefinition(typeReference);

            switch (typeReference.MetadataType)
            {
                case MetadataType.Pointer:
                {
                    var type = GetType(((PointerType)typeReference).ElementType, TypeState.Opaque);
                    // Special case: void*
                    if (LLVM.VoidTypeInContext(context) == type.DataType)
                        dataType = intPtrType;
                    else
                        dataType = LLVM.PointerType(type.DataType, 0);
                    valueType = dataType;
                    stackType = StackValueType.NativeInt;
                    break;
                }
                case MetadataType.ByReference:
                {
                    var type = GetType(((ByReferenceType)typeReference).ElementType, TypeState.Opaque);
                    dataType = LLVM.PointerType(type.DefaultType, 0);
                    valueType = dataType;
                    stackType = StackValueType.Reference;
                    break;
                }
                case MetadataType.RequiredModifier:
                    // TODO: Add support for this feature
                    return GetType(((RequiredModifierType)typeReference).ElementType, TypeState.Opaque);
                case MetadataType.Void:
                    dataType = LLVM.VoidTypeInContext(context);
                    stackType = StackValueType.Unknown;
                    break;
                case MetadataType.Boolean:
                    dataType = LLVM.Int8TypeInContext(context);
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
                    dataType = CharUsesUTF8
                        ? LLVM.Int8TypeInContext(context)
                        : LLVM.Int16TypeInContext(context);
                    stackType = StackValueType.Int32;
                    break;
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
                    // Open type?
                    if (typeDefinition.HasGenericParameters && !(typeReference is GenericInstanceType))
                        return null;

                    // When resolved, void becomes a real type
                    if (typeReference.FullName == typeof(void).FullName)
                    {
                        goto case MetadataType.Void;
                    }

                    if (typeDefinition.IsValueType && typeDefinition.IsEnum)
                    {
                        // Special case: enum
                        // Uses underlying type
                        var enumUnderlyingType = GetType(typeDefinition.GetEnumUnderlyingType(), TypeState.StackComplete);
                        dataType = enumUnderlyingType.DataType;
                        stackType = enumUnderlyingType.StackType;
                    }
                    else
                    {
                        stackType = typeDefinition.IsValueType ? StackValueType.Value : StackValueType.Object;
                        dataType = GenerateDataType(typeReference);
                    }

                    valueType = dataType;

                    break;
                }
                default:
                    throw new NotImplementedException();
            }

            // Create class version (boxed version with VTable)
            var boxedType = LLVM.StructCreateNamed(context, typeReference.MangledName() + ".class");
            if (valueType == TypeRef.Empty)
                valueType = LLVM.StructCreateNamed(context, typeReference.MangledName() + ".value");

            var result = new Type(typeReference, typeDefinition, dataType, valueType, boxedType, stackType);
            types.Add(typeReference, result);

            // Enqueue class generation, if needed
            EmitType(result);

            return result;
        }

        private TypeRef GenerateDataType(TypeReference typeReference)
        {
            return LLVM.StructCreateNamed(context, typeReference.MangledName() + ".data");
        }

        private void CompleteType(Type type)
        {
            var valueType = type.ValueType;
            var typeReference = type.TypeReference;
            var typeDefinition = GetMethodTypeDefinition(typeReference);
            var stackType = type.StackType;

            // Sometime, GetType might already define DataType (for standard CLR types such as int, enum, string, array, etc...).
            // In that case, do not process fields.
            if (LLVM.GetTypeKind(valueType) == TypeKind.StructTypeKind && LLVM.IsOpaqueStruct(valueType))
            {
                // Avoid recursion
                //type.IsBeingComplete = true;

                var fields = new Dictionary<FieldDefinition, Field>(MemberEqualityComparer.Default);

                var baseType = GetBaseTypeDefinition(typeReference);
                var parentType = baseType != null ? GetType(ResolveGenericsVisitor.Process(typeReference, baseType), TypeState.TypeComplete) : null;

                // Build actual type data (fields)
                // Add fields and vtable slots from parent class
                var fieldTypes = new List<TypeRef>(typeDefinition.Fields.Count + 1);

                if (parentType != null && stackType == StackValueType.Object)
                {
                    fieldTypes.Add(parentType.DataType);
                }

                // Special cases: Array
                if (typeReference.MetadataType == MetadataType.Array)
                {
                    // String: length (native int) + first element pointer
                    var arrayType = (ArrayType)typeReference;
                    var elementType = GetType(arrayType.ElementType, TypeState.StackComplete);
                    fieldTypes.Add(intPtrType);
                    fieldTypes.Add(LLVM.PointerType(elementType.DefaultType, 0));
                }
                else
                {
                    foreach (var field in typeDefinition.Fields)
                    {
                        if (field.IsStatic)
                            continue;

                        var fieldType = GetType(assembly.MainModule.Import(ResolveGenericsVisitor.Process(typeReference, field.FieldType)), TypeState.StackComplete);

                        fields.Add(field, new Field(field, type, fieldType, fieldTypes.Count));
                        fieldTypes.Add(fieldType.DefaultType);
                    }
                }

                LLVM.StructSetBody(valueType, fieldTypes.ToArray(), false);

                type.Fields = fields;
            }
        }

        private void EmitType(Type type, bool force = false)
        {
            // Already emitted?
            if (type.IsLocal)
                return;

            // Should we emit it?
            bool isLocal = type.TypeReference.Resolve().Module.Assembly == assembly;

            bool isTemplate;
            // Manually emit Array classes locally (until proper mscorlib + generic instantiation exists).
            isTemplate = type.TypeReference.MetadataType == MetadataType.Array;

            // Also emit generic types locally
            isTemplate |= type.TypeReference.HasGenericParameters;

            if (!(isLocal || isTemplate) && !force)
                return;

            // Type is local, make sure it's complete right away because it will be needed anyway
            CompleteType(type);

            // Setup proper linkage
            type.Linkage = isTemplate ? Linkage.LinkOnceAnyLinkage : Linkage.ExternalLinkage;

            // Enqueue for later generation
            type.IsLocal = true;
            classesToGenerate.Enqueue(type);
        }
    }
}