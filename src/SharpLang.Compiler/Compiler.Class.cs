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
            Class @class;
            if (classes.TryGetValue(typeReference, out @class))
                return @class;

            @class = CreateClass(typeReference);

            return @class;
        }

        /// <summary>
        /// Compiles the specified class.
        /// </summary>
        /// <param name="typeReference">The type definition.</param>
        /// <returns></returns>
        private Class CreateClass(TypeReference typeReference)
        {
            Class @class;
            if (classes.TryGetValue(typeReference, out @class))
                return @class;

            bool processClass = false;
            bool processFields = false;

            switch (typeReference.MetadataType)
            {
                case MetadataType.Void:
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
                case MetadataType.String:
                {
                    processClass = true;
                    break;
                }
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

            var type = GetType(typeReference);

            // Create class version (boxed version with VTable)
            var boxedType = type.ObjectType;
            var dataType = type.DataType;
            var stackType = type.StackType;

            @class = new Class(type, typeReference, dataType, boxedType, stackType);
            classes.Add(typeReference, @class);

            if (processClass)
            {
                var typeDefinition = typeReference.Resolve();

                var fieldTypes = new List<TypeRef>(typeDefinition.Fields.Count);

                var parentClass = typeDefinition.BaseType != null ? GetClass(ResolveGenericsVisitor.Process(typeReference, typeDefinition.BaseType)) : null;

                // Add parent class
                List<Class> superTypes = null;
                if (parentClass != null)
                {
                    @class.BaseType = parentClass;

                    // Add parent classes
                    @class.VirtualTable.AddRange(parentClass.VirtualTable);
                    foreach (var @interface in parentClass.Interfaces)
                        @class.Interfaces.Add(@interface);

                    @class.Depth = parentClass.Depth + 1;
                }

                // Build list of super types
                superTypes = new List<Class>(@class.Depth);
                var currentClass = @class;
                while (currentClass != null)
                {
                    superTypes.Add(currentClass);
                    currentClass = currentClass.BaseType;
                }

                // Reverse so that the list start with most inherited object
                // (allows faster type checking since a given type will always be at a given index)
                superTypes.Reverse();

                // Build methods slots
                // TODO: This will trigger their compilation, but maybe we might want to defer that later
                // (esp. since vtable is not built yet => recursion issues)
                CompileClassMethods(@class);

                if (typeDefinition.IsInterface)
                {
                    // Interface: No need for vtable, we can just use object's one
                    var vtableGlobal = GetClass(assembly.MainModule.Import(typeof(object))).GeneratedRuntimeTypeInfoGlobal;
                    LLVM.StructSetBody(boxedType, new[] { LLVM.TypeOf(vtableGlobal), dataType }, false);
                    @class.GeneratedRuntimeTypeInfoGlobal = vtableGlobal;
                }
                else
                {
                    // Get parent type RTTI
                    var runtimeTypeInfoType = LLVM.PointerType(LLVM.Int8TypeInContext(context), 0);
                    var parentRuntimeTypeInfo = parentClass != null
                        ? LLVM.ConstPointerCast(parentClass.GeneratedRuntimeTypeInfoGlobal, runtimeTypeInfoType)
                        : LLVM.ConstPointerNull(runtimeTypeInfoType);
    
                    // Build vtable
                    var vtableConstant = LLVM.ConstStructInContext(context, @class.VirtualTable.Select(x => x.GeneratedValue).ToArray(), false);
        
                    // Build IMT
                    var interfaceMethodTable = new LinkedList<InterfaceMethodTableEntry>[InterfaceMethodTableSize];
                    foreach (var @interface in typeDefinition.Interfaces)
                    {
                        var resolvedInterface = ResolveGenericsVisitor.Process(typeReference, @interface);
                        @class.Interfaces.Add(GetClass(resolvedInterface));
    
                        // TODO: Add any inherited interface inherited by the resolvedInterface as well
                    }
    
                    foreach (var @interface in @class.Interfaces)
                    {
                        foreach (var interfaceMethod in @interface.TypeReference.Resolve().Methods)
                        {
                            var resolvedInterfaceMethod = ResolveGenericMethod(@interface.TypeReference, interfaceMethod);

                            // If method is not fully resolved (generic method in interface), ignore it
                            // We are waiting for actual closed uses.
                            if (ResolveGenericsVisitor.ContainsGenericParameters(resolvedInterfaceMethod))
                                continue;
        
                            var resolvedFunction = CecilExtensions.TryMatchMethod(@class, resolvedInterfaceMethod);

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
                            var imtSlotIndex = (int)(methodId % interfaceMethodTable.Length);
        
                            var imtSlot = interfaceMethodTable[imtSlotIndex];
                            if (imtSlot == null)
                                interfaceMethodTable[imtSlotIndex] = imtSlot = new LinkedList<InterfaceMethodTableEntry>();
        
                            imtSlot.AddLast(new InterfaceMethodTableEntry { Function = resolvedFunction, MethodId = methodId, SlotIndex = imtSlotIndex });
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
                                    LLVM.ConstInt(int32Type, (ulong)imtEntry.MethodId, false),                          // i32 functionId
                                    LLVM.ConstPointerCast(imtEntry.Function.GeneratedValue, intPtrType),                // i8* functionPtr
                                });
                            })
                            .Concat(Enumerable.Repeat(LLVM.ConstNull(imtEntryType), 1)).ToArray()); // Append { 0, 0 } terminator
                            var imtEntryGlobal = LLVM.AddGlobal(module, LLVM.TypeOf(imtEntries), "IMTEntry");
                            LLVM.SetInitializer(imtEntryGlobal, imtEntries);
                            
                            // Add 1 to differentiate between single entry and IMT array
                            return LLVM.ConstIntToPtr(
                                LLVM.ConstAdd(
                                    LLVM.ConstPtrToInt(imtEntryGlobal, nativeIntType),
                                    LLVM.ConstInt(nativeIntType, 1, false)),
                                intPtrType);
                        }
                    }).ToArray());
        
                    // Build static fields
                    foreach (var field in typeDefinition.Fields)
                    {
                        if (!field.IsStatic)
                            continue;
    
                        var fieldType = CreateType(assembly.MainModule.Import(ResolveGenericsVisitor.Process(typeReference, field.FieldType)));
                        @class.Fields.Add(field, new Field(field, @class, fieldType, fieldTypes.Count));
                        fieldTypes.Add(fieldType.DefaultType);
                    }
    
                    var staticFieldsEmpty = LLVM.ConstNull(LLVM.StructTypeInContext(context, fieldTypes.ToArray(), false));
                    fieldTypes.Clear(); // Reused for non-static fields after

                    // Build super types
                    // Helpful for fast is/as checks on class hierarchy
                    var superTypeCount = LLVM.ConstInt(int32Type, (ulong)@class.Depth + 1, false);
                    ValueRef superTypesGlobal;

                    // Super types global
                    var superTypesConstantGlobal = LLVM.AddGlobal(module, LLVM.ArrayType(intPtrType, (uint)superTypes.Count), string.Empty);

                    var zero = LLVM.ConstInt(int32Type, 0, false);
                    superTypesGlobal = LLVM.ConstInBoundsGEP(superTypesConstantGlobal, new[] { zero, zero });

                    // Build RTTI
                    var runtimeTypeInfoConstant = LLVM.ConstStructInContext(context, new[]
                    {
                        parentRuntimeTypeInfo,
                        superTypeCount,
                        superTypesGlobal,
                        interfaceMethodTableConstant,
                        vtableConstant,
                        staticFieldsEmpty,
                    }, false);
                    var vtableGlobal = LLVM.AddGlobal(module, LLVM.TypeOf(runtimeTypeInfoConstant), string.Empty);
                    LLVM.SetInitializer(vtableGlobal, runtimeTypeInfoConstant);
                    LLVM.StructSetBody(boxedType, new[] { LLVM.TypeOf(vtableGlobal), dataType }, false);
                    @class.GeneratedRuntimeTypeInfoGlobal = vtableGlobal;

                    // Build super type list (after RTTI since we need pointer to RTTI)
                    var superTypesConstant = LLVM.ConstArray(intPtrType,
                        superTypes.Select(superType => LLVM.ConstPointerCast(superType.GeneratedRuntimeTypeInfoGlobal, intPtrType)).ToArray());
                    LLVM.SetInitializer(superTypesConstantGlobal, superTypesConstant);
                }

                // Sometime, GetType might already define DataType (for standard CLR types such as int, enum, string, array, etc...).
                // In that case, do not process fields.
                if (processFields && LLVM.GetTypeKind(dataType) == TypeKind.StructTypeKind && LLVM.IsOpaqueStruct(dataType))
                {
                    // Build actual type data (fields)
                    // Add fields and vtable slots from parent class
                    if (parentClass != null && typeReference.MetadataType == MetadataType.Class)
                    {
                        fieldTypes.Add(parentClass.DataType);
                    }

                    foreach (var field in typeDefinition.Fields)
                    {
                        if (field.IsStatic)
                            continue;

                        var fieldType = CreateType(assembly.MainModule.Import(ResolveGenericsVisitor.Process(typeReference, field.FieldType)));
                        @class.Fields.Add(field, new Field(field, @class, fieldType, fieldTypes.Count));
                        fieldTypes.Add(fieldType.DefaultType);
                    }

                    LLVM.StructSetBody(dataType, fieldTypes.ToArray(), false);
                }
            }

            return @class;
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