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
                    break;
                }
                case MetadataType.ValueType:
                case MetadataType.Class:
                case MetadataType.Object:
                case MetadataType.GenericInstance:
                {
                    // Process non-static fields
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

            if (processFields)
            {
                var typeDefinition = typeReference.Resolve();

                var fieldTypes = new List<TypeRef>(typeDefinition.Fields.Count);

                var parentClass = typeDefinition.BaseType != null ? GetClass(ResolveGenericsVisitor.Process(typeReference, typeDefinition.BaseType)) : null;

                // Add parent class
                if (parentClass != null)
                {
                    @class.BaseType = parentClass;

                    // Add parent classes
                    @class.VirtualTable.AddRange(parentClass.VirtualTable);
                    foreach (var @interface in parentClass.Interfaces)
                        @class.Interfaces.Add(@interface);
                }

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
        
                            var resolvedFunction = CecilExtensions.TryMatchMethod(@class, resolvedInterfaceMethod);
        
                            var methodId = GetMethodId(resolvedInterfaceMethod);
                            var imtSlotIndex = (int)(methodId % interfaceMethodTable.Length);
        
                            var imtSlot = interfaceMethodTable[imtSlotIndex];
                            if (imtSlot == null)
                                interfaceMethodTable[imtSlotIndex] = imtSlot = new LinkedList<InterfaceMethodTableEntry>();
        
                            imtSlot.AddLast(new InterfaceMethodTableEntry { Function = resolvedFunction, MethodId = methodId, SlotIndex = imtSlotIndex });
                        }
                    }
                    var interfaceMethodTableConstant = LLVM.ConstArray(imtEntryType, interfaceMethodTable.Select(imtSlot =>
                    {
                        if (imtSlot == null)
                        {
                            // No entries: null slot
                            return LLVM.ConstNull(imtEntryType);
                        }
        
                        if (imtSlot.Count > 1)
                            throw new NotImplementedException("IMT with more than one entry per slot is not implemented yet.");
        
                        var imtEntry = imtSlot.First.Value;
                        return LLVM.ConstNamedStruct(imtEntryType, new[]
                        {
                            LLVM.ConstPointerCast(imtEntry.Function.GeneratedValue, intPtrType),                // i8* functionPtr
                            LLVM.ConstInt(LLVM.Int32TypeInContext(context), (ulong)imtEntry.MethodId, false),   // i32 functionId
                            LLVM.ConstPointerNull(LLVM.PointerType(imtEntryType, 0)),                           // IMTEntry* nextSlot
                        });
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
    
                    // Build RTTI
                    var runtimeTypeInfoConstant = LLVM.ConstStructInContext(context, new[] { parentRuntimeTypeInfo, interfaceMethodTableConstant, vtableConstant, staticFieldsEmpty }, false);
                    var vtableGlobal = LLVM.AddGlobal(module, LLVM.TypeOf(runtimeTypeInfoConstant), string.Empty);
                    LLVM.SetInitializer(vtableGlobal, runtimeTypeInfoConstant);
                    LLVM.StructSetBody(boxedType, new[] { LLVM.TypeOf(vtableGlobal), dataType }, false);
                    @class.GeneratedRuntimeTypeInfoGlobal = vtableGlobal;
                }

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