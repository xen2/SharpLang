using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using SharpLang.CompilerServices.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public partial class Compiler
    {
        public const int InterfaceMethodTableSize = 19;

        /// <summary>
        /// Gets the specified class.
        /// </summary>
        /// <param name="typeReference">The type definition.</param>
        /// <returns></returns>
        private Class GetClass(TypeReference typeReference)
        {
            var type = GetType(typeReference);

            return GetClass(type);
        }

        /// <summary>
        /// Compiles the specified class.
        /// </summary>
        /// <param name="typeReference">The type definition.</param>
        /// <returns></returns>
        private Class GetClass(Type type)
        {
            bool processClass = false;
            bool processFields = false;
            var typeReference = type.TypeReference;

            switch (typeReference.MetadataType)
            {
                case MetadataType.ByReference:
                case MetadataType.Void:
                case MetadataType.Pointer:
                    // Should return something similar to IntPtr?
                    return null;
                case MetadataType.Boolean:
                case MetadataType.Char:
                case MetadataType.Byte:
                case MetadataType.SByte:
                case MetadataType.Int16:
                case MetadataType.UInt16:
                case MetadataType.Int32:
                case MetadataType.UInt32:
                case MetadataType.Int64:
                case MetadataType.UInt64:
                case MetadataType.IntPtr:
                case MetadataType.UIntPtr:
                case MetadataType.Single:
                case MetadataType.Double:
                {
                    processClass = true;
                    processFields = true;
                    break;
                }
                case MetadataType.Array:
                case MetadataType.String:
                case MetadataType.TypedByReference:
                case MetadataType.ValueType:
                case MetadataType.Class:
                case MetadataType.Object:
                case MetadataType.GenericInstance:
                {
                    // Process class and instance fields
                    processClass = true;
                    processFields = true;
                    break;
                }
                default:
                    throw new NotImplementedException();
            }

            // Create class version (boxed version with VTable)
            var boxedType = type.ObjectType;
            var dataType = type.DataType;
            var valueType = type.ValueType;

            if (type.Class != null)
            {
                return type.Class;
            }

            var @class = type.Class = new Class(type);

            if (processClass)
            {
                var baseType = GetBaseTypeDefinition(typeReference);
                var typeDefinition = GetMethodTypeDefinition(typeReference);

                var fieldTypes = new List<TypeRef>(typeDefinition.Fields.Count);

                var parentClass = baseType != null ? GetClass(ResolveGenericsVisitor.Process(typeReference, baseType)) : null;

                // Add parent class
                if (parentClass != null)
                {
                    @class.BaseType = parentClass;

                    // Add parent classes
                    @class.VirtualTable.AddRange(parentClass.VirtualTable);
                    foreach (var @interface in parentClass.Interfaces)
                        @class.Interfaces.Add(@interface);

                    @class.Depth = parentClass.Depth + 1;
                }

                if (typeReference is ArrayType)
                {
                    var elementType = ResolveGenericsVisitor.Process(typeReference, ((ArrayType)typeReference).ElementType);

                    // Array types implicitely inherits from IList<T>, ICollection<T>, IReadOnlyList<T>, IReadOnlyCollection<T> and IEnumerable<T>
                    foreach (var interfaceType in new[] { typeof(IList<>), typeof(ICollection<>), typeof(IReadOnlyCollection<>), typeof(IReadOnlyList<>), typeof(IEnumerable<>) })
                    {
                        var @interfaceGeneric = corlib.MainModule.GetType(interfaceType.FullName);
                        var @interface = @interfaceGeneric.MakeGenericInstanceType(elementType);
                        @class.Interfaces.Add(GetClass(@interface));
                    }
                }

                // Build methods slots
                // TODO: This will trigger their compilation, but maybe we might want to defer that later
                // (esp. since vtable is not built yet => recursion issues)
                PrepareClassMethods(type);

                if (typeDefinition.IsInterface)
                {
                    // Interface: No need for vtable, we can just use object's one
                    var vtableGlobal = GetClass(assembly.MainModule.Import(typeof(object))).GeneratedRuntimeTypeInfoGlobal;
                    LLVM.StructSetBody(boxedType, new[] { LLVM.TypeOf(vtableGlobal), valueType }, false);
                    @class.GeneratedRuntimeTypeInfoGlobal = vtableGlobal;
                }
                else
                {
                    // Get parent type RTTI
                    var parentRuntimeTypeInfoType = parentClass != null
                        ? LLVM.TypeOf(parentClass.GeneratedRuntimeTypeInfoGlobal)
                        : intPtrType;
    
                    // Build vtable
                    var vtableType = LLVM.StructTypeInContext(context, @class.VirtualTable.Select(x => LLVM.TypeOf(x.GeneratedValue)).ToArray(), false);
        
                    foreach (var @interface in typeDefinition.Interfaces)
                    {
                        var resolvedInterface = ResolveGenericsVisitor.Process(typeReference, @interface);
                        @class.Interfaces.Add(GetClass(resolvedInterface));
    
                        // TODO: Add any inherited interface inherited by the resolvedInterface as well
                    }
            
                    // Build static fields
                    foreach (var field in typeDefinition.Fields)
                    {
                        if (!field.IsStatic)
                            continue;
    
                        var fieldType = CreateType(assembly.MainModule.Import(ResolveGenericsVisitor.Process(typeReference, field.FieldType)));
                        @class.Fields.Add(field, new Field(field, @class, fieldType, fieldTypes.Count));
                        fieldTypes.Add(fieldType.DefaultType);
                    }
    
                    var staticFieldsType = LLVM.StructTypeInContext(context, fieldTypes.ToArray(), false);
                    fieldTypes.Clear(); // Reused for non-static fields after

                    var runtimeTypeInfoType = LLVM.StructTypeInContext(context, new []
                    {
                        parentRuntimeTypeInfoType,
                        int32Type,
                        int32Type,
                        LLVM.PointerType(intPtrType, 0),
                        LLVM.PointerType(intPtrType, 0),
                        LLVM.ArrayType(intPtrType, InterfaceMethodTableSize),
                        vtableType,
                        staticFieldsType,
                    }, false);

                    var runtimeTypeInfoGlobal = LLVM.AddGlobal(module, runtimeTypeInfoType, typeReference.FullName + ".rtti");
                    @class.GeneratedRuntimeTypeInfoGlobal = runtimeTypeInfoGlobal;

                    if (@class.Type.IsLocal)
                    {
                        BuildRuntimeType(@class);
                    }
                    else
                    {
                        LLVM.SetLinkage(runtimeTypeInfoGlobal, Linkage.ExternalWeakLinkage);
                    }

                    LLVM.StructSetBody(boxedType, new[] { LLVM.TypeOf(runtimeTypeInfoGlobal), valueType }, false);
                }

                // Sometime, GetType might already define DataType (for standard CLR types such as int, enum, string, array, etc...).
                // In that case, do not process fields.
                if (processFields && LLVM.GetTypeKind(valueType) == TypeKind.StructTypeKind && LLVM.IsOpaqueStruct(valueType))
                {
                    // Build actual type data (fields)
                    // Add fields and vtable slots from parent class
                    if (parentClass != null && type.StackType == StackValueType.Object)
                    {
                        fieldTypes.Add(parentClass.Type.DataType);
                    }

                    // Special cases: Array and String
                    if (typeReference.MetadataType == MetadataType.String)
                    {
                        // String: length (int32) + char pointer
                        fieldTypes.Add(int32Type);
                        fieldTypes.Add(intPtrType);
                    }
                    else if (typeReference.MetadataType == MetadataType.Array)
                    {
                        // String: length (native int) + first element pointer
                        var arrayType = (ArrayType)typeReference;
                        var elementType = CreateType(arrayType.ElementType);
                        fieldTypes.Add(intPtrType);
                        fieldTypes.Add(LLVM.PointerType(elementType.DefaultType, 0));
                    }
                    else
                    {
                        foreach (var field in typeDefinition.Fields)
                        {
                            if (field.IsStatic)
                                continue;

                            var fieldType = CreateType(assembly.MainModule.Import(ResolveGenericsVisitor.Process(typeReference, field.FieldType)));
                            @class.Fields.Add(field, new Field(field, @class, fieldType, fieldTypes.Count));
                            fieldTypes.Add(fieldType.DefaultType);
                        }
                    }

                    LLVM.StructSetBody(valueType, fieldTypes.ToArray(), false);
                }
            }

            return @class;
        }

        private void BuildRuntimeType(Class @class)
        {
            if (@class.IsEmitted)
                return;

            @class.IsEmitted = true;

            // Build IMT
            var interfaceMethodTable = new LinkedList<InterfaceMethodTableEntry>[InterfaceMethodTableSize];
            foreach (var @interface in @class.Interfaces)
            {
                foreach (var interfaceMethod in @interface.Type.TypeReference.Resolve().Methods)
                {
                    var resolvedInterfaceMethod = ResolveGenericMethod(@interface.Type.TypeReference, interfaceMethod);

                    // If method is not fully resolved (generic method in interface), ignore it
                    // We are waiting for actual closed uses.
                    if (ResolveGenericsVisitor.ContainsGenericParameters(resolvedInterfaceMethod))
                        continue;

                    var resolvedFunction = CecilExtensions.TryMatchMethod(@class, resolvedInterfaceMethod);
                    if (resolvedFunction == null && @class.Type.TypeReference is ArrayType)
                    {
                        var arrayType = corlib.MainModule.GetType(typeof(Array).FullName);
                        var matchingMethod = (MethodReference)arrayType.Methods.First(x => x.Name.StartsWith("InternalArray_") && x.Name.EndsWith(resolvedInterfaceMethod.Name));
                        if (matchingMethod != null)
                        {
                            if (matchingMethod.HasGenericParameters)
                            {
                                matchingMethod = matchingMethod.MakeGenericMethod(((ArrayType)@class.Type.TypeReference).ElementType);
                            }

                            resolvedFunction = GetFunction(matchingMethod);

                            // Manually emit Array functions locally (until proper mscorlib + generic instantiation exists).
                            EmitFunction(resolvedFunction);
                        }
                    }

                    if (resolvedFunction == null)
                        throw new InvalidOperationException(string.Format("Could not find matching method for {0} in {1}", resolvedInterfaceMethod, @class));

                    var isInterface = resolvedFunction.DeclaringType.TypeReference.Resolve().IsInterface;
                    if (!isInterface && resolvedFunction.VirtualSlot != -1)
                    {
                        // We might have found a base virtual method matching this interface method.
                        // Let's get the actual method override for this virtual slot.
                        resolvedFunction = @class.VirtualTable[resolvedFunction.VirtualSlot];
                    }

                    // If method is not found, it could be due to covariance/contravariance
                    if (resolvedFunction == null)
                        throw new InvalidOperationException("Interface method not found");

                    var methodId = GetMethodId(resolvedInterfaceMethod);
                    var imtSlotIndex = (int) (methodId%interfaceMethodTable.Length);

                    var imtSlot = interfaceMethodTable[imtSlotIndex];
                    if (imtSlot == null)
                        interfaceMethodTable[imtSlotIndex] = imtSlot = new LinkedList<InterfaceMethodTableEntry>();

                    imtSlot.AddLast(new InterfaceMethodTableEntry
                    {
                        Function = resolvedFunction,
                        MethodId = methodId,
                        SlotIndex = imtSlotIndex
                    });
                }
            }
            var interfaceMethodTableConstant = LLVM.ConstArray(intPtrType, interfaceMethodTable.Select(imtSlot =>
            {
                if (imtSlot == null)
                {
                    // No entries: null slot
                    return LLVM.ConstNull(intPtrType);
                }

                if (imtSlot.Count == 1)
                {
                    // Single entry
                    var imtEntry = imtSlot.First.Value;
                    return LLVM.ConstPointerCast(imtEntry.Function.GeneratedValue, intPtrType);
                }
                else
                {
                    // Multiple entries, create IMT array with all entries
                    // TODO: Support covariance/contravariance?
                    var imtEntries = LLVM.ConstArray(imtEntryType, imtSlot.Select(imtEntry =>
                    {
                        return LLVM.ConstNamedStruct(imtEntryType, new[]
                        {
                            LLVM.ConstInt(int32Type, (ulong) imtEntry.MethodId, false), // i32 functionId
                            LLVM.ConstPointerCast(imtEntry.Function.GeneratedValue, intPtrType), // i8* functionPtr
                        });
                    })
                        .Concat(Enumerable.Repeat(LLVM.ConstNull(imtEntryType), 1)).ToArray()); // Append { 0, 0 } terminator
                    var imtEntryGlobal = LLVM.AddGlobal(module, LLVM.TypeOf(imtEntries), @class.Type.TypeReference.FullName + ".imt");
                    LLVM.SetInitializer(imtEntryGlobal, imtEntries);

                    // Add 1 to differentiate between single entry and IMT array
                    return LLVM.ConstIntToPtr(
                        LLVM.ConstAdd(
                            LLVM.ConstPtrToInt(imtEntryGlobal, nativeIntType),
                            LLVM.ConstInt(nativeIntType, 1, false)),
                        intPtrType);
                }
            }).ToArray());


            // Build list of super types
            var superTypes = new List<Class>(@class.Depth);
            var currentClass = @class;
            while (currentClass != null)
            {
                superTypes.Add(currentClass);
                currentClass = currentClass.BaseType;
            }

            // Reverse so that the list start with most inherited object
            // (allows faster type checking since a given type will always be at a given index)
            superTypes.Reverse();

            // Build super types
            // Helpful for fast is/as checks on class hierarchy
            var superTypeCount = LLVM.ConstInt(int32Type, (ulong) @class.Depth + 1, false);
            var interfacesCount = LLVM.ConstInt(int32Type, (ulong) @class.Interfaces.Count, false);

            var zero = LLVM.ConstInt(int32Type, 0, false);

            // Super types global
            var superTypesConstantGlobal = LLVM.AddGlobal(module, LLVM.ArrayType(intPtrType, (uint) superTypes.Count),
                @class.Type.TypeReference.FullName + ".supertypes");
            var superTypesGlobal = LLVM.ConstInBoundsGEP(superTypesConstantGlobal, new[] {zero, zero});

            // Interface map global
            var interfacesConstantGlobal = LLVM.AddGlobal(module, LLVM.ArrayType(intPtrType, (uint) @class.Interfaces.Count),
                @class.Type.TypeReference.FullName + ".interfaces");
            var interfacesGlobal = LLVM.ConstInBoundsGEP(interfacesConstantGlobal, new[] {zero, zero});

            // Build VTable
            var vtableConstant = LLVM.ConstStructInContext(context, @class.VirtualTable.Select(x => x.GeneratedValue).ToArray(), false);

            // Build RTTI
            var runtimeTypeInfoGlobal = @class.GeneratedRuntimeTypeInfoGlobal;
            var runtimeTypeInfoType = LLVM.GetElementType(LLVM.TypeOf(runtimeTypeInfoGlobal));
            var runtimeTypeInfoTypeElements = new TypeRef[LLVM.CountStructElementTypes(runtimeTypeInfoType)];
            LLVM.GetStructElementTypes(runtimeTypeInfoType, runtimeTypeInfoTypeElements);
            var runtimeTypeInfoConstant = LLVM.ConstNamedStruct(runtimeTypeInfoType, new[]
            {
                @class.BaseType != null ? @class.BaseType.GeneratedRuntimeTypeInfoGlobal : LLVM.ConstPointerNull(intPtrType),
                superTypeCount,
                interfacesCount,
                superTypesGlobal,
                interfacesGlobal,
                interfaceMethodTableConstant,
                vtableConstant,
                LLVM.ConstNull(runtimeTypeInfoTypeElements[(int)RuntimeTypeInfoFields.StaticFields]),
            });
            LLVM.SetInitializer(runtimeTypeInfoGlobal, runtimeTypeInfoConstant);

            // Build super type list (after RTTI since we need pointer to RTTI)
            var superTypesConstant = LLVM.ConstArray(intPtrType,
                superTypes.Select(superType => LLVM.ConstPointerCast(superType.GeneratedRuntimeTypeInfoGlobal, intPtrType))
                    .ToArray());
            LLVM.SetInitializer(superTypesConstantGlobal, superTypesConstant);

            // Build interface map
            var interfacesConstant = LLVM.ConstArray(intPtrType,
                @class.Interfaces.Select(
                    @interface => LLVM.ConstPointerCast(@interface.GeneratedRuntimeTypeInfoGlobal, intPtrType)).ToArray());
            LLVM.SetInitializer(interfacesConstantGlobal, interfacesConstant);

            // Mark RTTI as external
            LLVM.SetLinkage(runtimeTypeInfoGlobal, Linkage.ExternalLinkage);
        }

        private static uint GetMethodId(MethodReference resolvedInterfaceMethod)
        {
            // For now, use full name has code for IMT slot
            // (might need a more robust method later, esp. since runtime needs to compute it for covariance/contravariance)
            var methodId = StringHashCode(resolvedInterfaceMethod.FullName);
            return methodId;
        }

        private static uint StringHashCode(string str)
        {
            uint hash = 17;
            foreach (char c in str)
            {
                unchecked
                {
                    hash = hash*23 + c;
                }
            }

            return hash;
        }

        /// <summary>
        /// Represents an entry in the Interface Method Table (IMT).
        /// </summary>
        private struct InterfaceMethodTableEntry
        {
            public Function Function;
            public uint MethodId;
            public int SlotIndex;
        }
    }
}