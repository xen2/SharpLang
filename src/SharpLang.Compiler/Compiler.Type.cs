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
                case MetadataType.Pinned:
                    // TODO: Add support for this feature
                    return GetType(((PinnedType)typeReference).ElementType, TypeState.Opaque);
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
            TypeRef dataType;

            var typeDefinition = GetMethodTypeDefinition(typeReference);

            // Struct / Class
            // Auto layout or Sequential Layout with packing size 0 (auto) will result in normal LLVM struct (optimized for target)
            // Otherwise (Explicit layout or Sequential layout with custom packing), make a i8 array and access field with GEP in it.
            if (IsCustomLayout(typeDefinition))
            {
                var classSize = ComputeClassSize(typeDefinition, typeReference);

                dataType = LLVM.ArrayType(LLVM.Int8TypeInContext(context), (uint)classSize);
            }
            else
            {
                dataType = LLVM.StructCreateNamed(context, typeReference.MangledName() + ".data");
            }
            return dataType;
        }

        private int ComputeClassSize(TypeDefinition typeDefinition, TypeReference typeReference)
        {
            // Maybe class size is explicitely declared?
            var classSize = typeDefinition.ClassSize;

            // If not, use fields
            if (classSize == -1 || classSize == 0)
            {
                if (typeDefinition.PackingSize > 4)
                    throw new NotImplementedException("Only pack size 1, 2 and 4 are supported.");

                // TODO: Check if type is blittable, otherwise I think it is supposed to affect marshalled version only (or affect managed version differently?)

                if (typeDefinition.IsExplicitLayout)
                {
                    // Find offset of last field
                    foreach (var field in typeDefinition.Fields)
                    {
                        if (field.IsStatic)
                            continue;

                        // TODO: Align using pack size? Need to study .NET behavior.

                        var fieldType = GetType(assembly.MainModule.Import(ResolveGenericsVisitor.Process(typeReference, field.FieldType)), TypeState.StackComplete);
                        classSize = Math.Max((int)classSize, field.Offset + (int)LLVM.ABISizeOfType(targetData, fieldType.DefaultType));
                    }
                }
                else if (typeDefinition.IsSequentialLayout)
                {
                    foreach (var field in typeDefinition.Fields)
                    {
                        if (field.IsStatic)
                            continue;

                        // Align for next field, according to packing size
                        classSize = (classSize + typeDefinition.PackingSize - 1) & ~(typeDefinition.PackingSize - 1);

                        // Add size of field
                        var fieldType = GetType(assembly.MainModule.Import(ResolveGenericsVisitor.Process(typeReference, field.FieldType)), TypeState.StackComplete);
                        classSize += (int)LLVM.ABISizeOfType(targetData, fieldType.DefaultType);
                    }
                }
            }
            return classSize;
        }

        private static bool IsCustomLayout(TypeDefinition typeDefinition)
        {
            return typeDefinition.IsExplicitLayout || (typeDefinition.IsSequentialLayout && typeDefinition.PackingSize != -1 && typeDefinition.PackingSize != 0);
        }

        private void CompleteType(Type type)
        {
            var typeReference = type.TypeReference;
            switch (typeReference.MetadataType)
            {
                case MetadataType.Pointer:
                case MetadataType.ByReference:
                case MetadataType.RequiredModifier:
                    return;
            }

            var valueType = type.ValueType;
            var typeDefinition = GetMethodTypeDefinition(typeReference);
            var stackType = type.StackType;

            // Sometime, GetType might already define DataType (for standard CLR types such as int, enum, string, array, etc...).
            // In that case, do not process fields.
            if (type.Fields == null && (LLVM.GetTypeKind(valueType) == TypeKind.StructTypeKind || LLVM.GetTypeKind(valueType) == TypeKind.ArrayTypeKind))
            {
                var fields = new Dictionary<FieldDefinition, Field>(MemberEqualityComparer.Default);

                // Avoid recursion (need a better way?)
                type.Fields = fields;

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
                    bool isCustomLayout = IsCustomLayout(typeDefinition); // Do we use a struct or array?
                    int classSize = 0; // Used for sequential layout

                    foreach (var field in typeDefinition.Fields)
                    {
                        if (field.IsStatic)
                            continue;

                        var fieldType = GetType(assembly.MainModule.Import(ResolveGenericsVisitor.Process(typeReference, field.FieldType)), TypeState.StackComplete);

                        // Compute struct index (that we can use to access the field). Either struct index or array offset.
                        int structIndex;
                        if (!isCustomLayout)
                        {
                            // Default case, if no custom layout (index so that we can use it in GEP)
                            structIndex = fieldTypes.Count;
                        }
                        else if (typeDefinition.IsExplicitLayout)
                        {
                            structIndex = field.Offset;
                        }
                        else if (typeDefinition.IsSequentialLayout)
                        {
                            // Align for next field, according to packing size
                            classSize = (classSize + typeDefinition.PackingSize - 1) & ~(typeDefinition.PackingSize - 1);
                            structIndex = classSize;
                            classSize += (int)LLVM.ABISizeOfType(targetData, fieldType.DefaultType);
                        }
                        else
                        {
                            throw new InvalidOperationException("Invalid class layouting when computing field offsets.");
                        }

                        fields.Add(field, new Field(field, type, fieldType, structIndex));
                        fieldTypes.Add(fieldType.DefaultType);
                    }
                }

                // Set struct (if not custom layout with array type)
                if (LLVM.GetTypeKind(valueType) == TypeKind.StructTypeKind)
                    LLVM.StructSetBody(valueType, fieldTypes.ToArray(), false);
            }
        }

        internal Linkage GetLinkageType(TypeReference typeReference, bool force = false)
        {
            // Is it inside this assembly
            bool isLocal = (typeReference.Resolve().Module.Assembly == assembly);

            // TODO: Ideally we would check if type is still not being imported in a referenced assembly
            // However, it didn't seem worth it. Might need further investigation.

            if (!isLocal && !force)
                return Linkage.ExternalWeakLinkage;

            // Manually emit Array classes locally (until proper mscorlib + generic instantiation exists).
            bool isTemplate = typeReference.MetadataType == MetadataType.Array;

            // Also emit generic types locally
            isTemplate |= typeReference.HasGenericParameters || typeReference is GenericInstanceType;

            return isTemplate ? Linkage.LinkOnceAnyLinkage : Linkage.ExternalLinkage;
        }

        private void EmitType(Type type, bool force = false)
        {
            // Already emitted?
            if (type.IsLocal)
                return;

            var linkageType = GetLinkageType(type.TypeReference, force);
            type.Linkage = linkageType;

            if (linkageType != Linkage.ExternalWeakLinkage)
            {
                // Type is local, make sure it's complete right away because it will be needed anyway
                CompleteType(type);

                // Enqueue for later generation
                type.IsLocal = true;
                classesToGenerate.Enqueue(type);
            }
        }
    }
}