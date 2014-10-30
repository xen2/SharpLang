using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
            var type = GetType(typeReference, TypeState.Opaque);

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
            var typeReference = type.TypeReferenceCecil;

            // Need complete type
            GetType(type.TypeReferenceCecil, TypeState.TypeComplete);

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
                    break;
                }
                default:
                    throw new NotImplementedException();
            }

            // Create class version (boxed version with VTable)
            var boxedType = type.ObjectTypeLLVM;
            var valueType = type.ValueTypeLLVM;

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

                    // Add parent virtual methods
                    @class.VirtualTable.AddRange(parentClass.VirtualTable.TakeWhile(x => x.MethodReference.Resolve().IsVirtual));
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

                if (baseType != null && baseType.FullName == typeof(MulticastDelegate).FullName)
                {
                    // Add GetMulticastDispatchMethod runtime class on delegates
                    var getMulticastDispatchMethod = new MethodDefinition("GetMulticastDispatchMethod", MethodAttributes.Private, intPtr.TypeReferenceCecil);
                    getMulticastDispatchMethod.HasThis = true;
                    getMulticastDispatchMethod.Attributes = MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final;
                    getMulticastDispatchMethod.ImplAttributes = MethodImplAttributes.Runtime;
                    typeDefinition.Methods.Add(getMulticastDispatchMethod);
                }

                // Build methods slots
                // TODO: This will trigger their compilation, but maybe we might want to defer that later
                // (esp. since vtable is not built yet => recursion issues)
                PrepareClassMethods(type);

                if (typeDefinition.IsInterface)
                {
                    // Interface: Generate an empty vtable (so that we can still compare pointer in isInstInterface, etc...)
                    // Remove invalid characters so that we can easily link against it from C++
                    var runtimeTypeInfoType = LLVM.GetElementType(LLVM.TypeOf(GetClass(@object).GeneratedRuntimeTypeInfoGlobalLLVM));
                    var mangledRttiName = Regex.Replace(typeReference.MangledName() + ".rtti", @"(\W)", "_");
                    var runtimeTypeInfoGlobal = LLVM.AddGlobal(module, runtimeTypeInfoType, mangledRttiName);
                    LLVM.StructSetBody(boxedType, new[] { LLVM.TypeOf(runtimeTypeInfoGlobal), valueType }, false);
                    @class.GeneratedRuntimeTypeInfoGlobalLLVM = runtimeTypeInfoGlobal;

                    if (@class.Type.IsLocal)
                    {
                        LLVM.SetInitializer(runtimeTypeInfoGlobal, LLVM.ConstNull(runtimeTypeInfoType));
                    }

                    // Apply linkage
                    LLVM.SetLinkage(runtimeTypeInfoGlobal, @class.Type.Linkage);
                }
                else
                {
                    // Get parent type RTTI
                    var parentRuntimeTypeInfoType = parentClass != null
                        ? LLVM.TypeOf(parentClass.GeneratedRuntimeTypeInfoGlobalLLVM)
                        : intPtrLLVM;
    
                    // Build vtable
                    @class.VTableTypeLLVM = LLVM.StructCreateNamed(context, typeReference.MangledName() + ".vtable");
                    LLVM.StructSetBody(@class.VTableTypeLLVM, @class.VirtualTable.Select(x => LLVM.TypeOf(x.GeneratedValue)).ToArray(), false);
        
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

                        var fieldType = GetType(ResolveGenericsVisitor.Process(typeReference, field.FieldType), TypeState.StackComplete);

                        @class.StaticFields.Add(field, new Field(field, type, fieldType, fieldTypes.Count));
                        fieldTypes.Add(fieldType.DefaultTypeLLVM);
                    }

                    var staticFieldsType = LLVM.StructCreateNamed(context, typeReference.MangledName() + ".static");
                    LLVM.StructSetBody(staticFieldsType, fieldTypes.ToArray(), false);
                    fieldTypes.Clear(); // Reused for non-static fields after

                    var runtimeTypeInfoType = LLVM.StructCreateNamed(context, typeReference.MangledName() + ".rtti_type");
                    LLVM.StructSetBody(runtimeTypeInfoType, new[]
                    {
                        parentRuntimeTypeInfoType,
                        int32LLVM,
                        int32LLVM,
                        LLVM.PointerType(intPtrLLVM, 0),
                        LLVM.PointerType(intPtrLLVM, 0),
                        LLVM.Int1TypeInContext(context),
                        LLVM.Int32TypeInContext(context),
                        LLVM.Int32TypeInContext(context),
                        LLVM.ArrayType(intPtrLLVM, InterfaceMethodTableSize),
                        @class.VTableTypeLLVM,
                        staticFieldsType,
                    }, false);

                    // Remove invalid characters so that we can easily link against it from C++
                    var mangledRttiName = Regex.Replace(typeReference.MangledName() + ".rtti", @"(\W)", "_");
                    var runtimeTypeInfoGlobal = LLVM.AddGlobal(module, runtimeTypeInfoType, mangledRttiName);
                    @class.GeneratedRuntimeTypeInfoGlobalLLVM = runtimeTypeInfoGlobal;

                    LLVM.StructSetBody(boxedType, new[] { LLVM.TypeOf(runtimeTypeInfoGlobal), valueType }, false);

                    if (@class.Type.IsLocal)
                    {
                        BuildRuntimeType(@class);
                    }

                    // Apply linkage
                    LLVM.SetLinkage(runtimeTypeInfoGlobal, @class.Type.Linkage);
                }

                // Prepare class initializer
                if (@class.StaticCtor != null || typeDefinition.Methods.Any(x => x.HasPInvokeInfo))
                {
                    //  void EnsureClassInitialized()
                    //  {
                    //      //lock (initMutex) < TODO: not implemented yet
                    //      {
                    //          if (!classInitialized)
                    //          {
                    //              classInitialized = true;
                    //              InitializeClass();
                    //          }
                    //      }
                    //  }
                    var initTypeFunction = LLVM.AddFunction(module, typeReference.MangledName() + "_inittype", LLVM.FunctionType(LLVM.VoidTypeInContext(context), new TypeRef[0], false));

                    // TODO: Temporarily emit it multiple time (once per assembly), that should be fixed!
                    LLVM.SetLinkage(initTypeFunction, Linkage.LinkOnceAnyLinkage);

                    var block = LLVM.AppendBasicBlockInContext(context, initTypeFunction, string.Empty);
                    LLVM.PositionBuilderAtEnd(builder2, block);

                    // Check if class is initialized
                    var indices = new[]
                    {
                        LLVM.ConstInt(int32LLVM, 0, false),                                                 // Pointer indirection
                        LLVM.ConstInt(int32LLVM, (int)RuntimeTypeInfoFields.TypeInitialized, false),        // Type initialized flag
                    };

                    var classInitializedAddress = LLVM.BuildInBoundsGEP(builder2, @class.GeneratedRuntimeTypeInfoGlobalLLVM, indices, string.Empty);
                    var classInitialized = LLVM.BuildLoad(builder2, classInitializedAddress, string.Empty);

                    var typeNeedInitBlock = LLVM.AppendBasicBlockInContext(context, initTypeFunction, string.Empty);
                    var nextBlock = LLVM.AppendBasicBlockInContext(context, initTypeFunction, string.Empty);

                    LLVM.BuildCondBr(builder2, classInitialized, nextBlock, typeNeedInitBlock);

                    // Initialize class (first time)
                    LLVM.PositionBuilderAtEnd(builder2, typeNeedInitBlock);

                    // Set flag so that it won't be initialized again
                    LLVM.BuildStore(builder2, LLVM.ConstInt(LLVM.Int1TypeInContext(context), 1, false), classInitializedAddress);

                    // TODO: PInvoke initialization
                    foreach (var pinvokeModule in typeDefinition.Methods.Where(x => x.HasPInvokeInfo).GroupBy(x => x.PInvokeInfo.Module))
                    {
                        var libraryName = CreateStringConstant(pinvokeModule.Key.Name, false, true);
                        var pinvokeLoadLibraryResult = LLVM.BuildCall(builder2, pinvokeLoadLibraryFunctionLLVM, new[] { libraryName }, string.Empty);

                        foreach (var method in pinvokeModule)
                        {
                            var entryPoint = CreateStringConstant(method.PInvokeInfo.EntryPoint, false, true);
                            var pinvokeGetProcAddressResult = LLVM.BuildCall(builder2, pinvokeGetProcAddressFunctionLLVM,
                                new[]
                                {
                                    pinvokeLoadLibraryResult,
                                    entryPoint,
                                }, string.Empty);

                            // TODO: Resolve method using generic context.
                            indices = new[]
                            {
                                LLVM.ConstInt(int32LLVM, 0, false),                                         // Pointer indirection
                                LLVM.ConstInt(int32LLVM, (int)RuntimeTypeInfoFields.VirtualTable, false),   // Access vtable
                                LLVM.ConstInt(int32LLVM, (ulong)GetFunction(method).VirtualSlot, false),    // Access specific vtable slot
                            };

                            // Get vtable slot and cast to proper type
                            var vtableSlot = LLVM.BuildInBoundsGEP(builder2, @class.GeneratedRuntimeTypeInfoGlobalLLVM, indices, string.Empty);
                            pinvokeGetProcAddressResult = LLVM.BuildPointerCast(builder2, pinvokeGetProcAddressResult, LLVM.GetElementType(LLVM.TypeOf(vtableSlot)), string.Empty);

                            // Store value
                            LLVM.BuildStore(builder2, pinvokeGetProcAddressResult, vtableSlot);
                        }
                    }

                    // Static ctor
                    if (@class.StaticCtor != null)
                        LLVM.BuildCall(builder2, @class.StaticCtor.GeneratedValue, new ValueRef[0], string.Empty);

                    LLVM.BuildBr(builder2, nextBlock);

                    LLVM.PositionBuilderAtEnd(builder2, nextBlock);
                    LLVM.BuildRetVoid(builder2);

                    @class.InitializeType = initTypeFunction;
                }
            }

            return @class;
        }

        private void BuildRuntimeType(Class @class)
        {
            if (@class.IsEmitted)
                return;

            Console.WriteLine("Build type {0}", @class);

            @class.IsEmitted = true;

            // Build IMT
            var interfaceMethodTable = new LinkedList<InterfaceMethodTableEntry>[InterfaceMethodTableSize];
            foreach (var @interface in @class.Interfaces)
            {
                foreach (var interfaceMethod in @interface.Type.TypeReferenceCecil.Resolve().Methods)
                {
                    var resolvedInterfaceMethod = ResolveGenericMethod(@interface.Type.TypeReferenceCecil, interfaceMethod);

                    // If method is not fully resolved (generic method in interface), ignore it
                    // We are waiting for actual closed uses.
                    if (ResolveGenericsVisitor.ContainsGenericParameters(resolvedInterfaceMethod))
                        continue;

                    var resolvedFunction = CecilExtensions.TryMatchMethod(@class, resolvedInterfaceMethod);
                    if (resolvedFunction == null && @class.Type.TypeReferenceCecil is ArrayType)
                    {
                        var arrayType = corlib.MainModule.GetType(typeof(Array).FullName);
                        var matchingMethod = (MethodReference)arrayType.Methods.First(x => x.Name.StartsWith("InternalArray_") && x.Name.EndsWith(resolvedInterfaceMethod.Name));
                        if (matchingMethod != null)
                        {
                            if (matchingMethod.HasGenericParameters)
                            {
                                matchingMethod = matchingMethod.MakeGenericMethod(((ArrayType)@class.Type.TypeReferenceCecil).ElementType);
                            }

                            resolvedFunction = GetFunction(matchingMethod);

                            // Manually emit Array functions locally (until proper mscorlib + generic instantiation exists).
                            EmitFunction(resolvedFunction);
                            LLVM.SetLinkage(resolvedFunction.GeneratedValue, Linkage.LinkOnceAnyLinkage);
                        }
                    }

                    if (resolvedFunction == null)
                        throw new InvalidOperationException(string.Format("Could not find matching method for {0} in {1}", resolvedInterfaceMethod, @class));

                    var isInterface = resolvedFunction.DeclaringType.TypeReferenceCecil.Resolve().IsInterface;
                    if (!isInterface && resolvedFunction.MethodReference.Resolve().IsVirtual && resolvedFunction.VirtualSlot != -1)
                    {
                        // We might have found a base virtual method matching this interface method.
                        // Let's get the actual method override for this virtual slot.
                        resolvedFunction = @class.VirtualTable[resolvedFunction.VirtualSlot];
                    }

                    // If method is not found, it could be due to covariance/contravariance
                    if (resolvedFunction == null)
                        throw new InvalidOperationException("Interface method not found");

                    var methodId = GetMethodId(resolvedInterfaceMethod);
                    var imtSlotIndex = (int)(methodId % InterfaceMethodTableSize);

                    var imtSlot = interfaceMethodTable[imtSlotIndex];
                    if (imtSlot == null)
                        interfaceMethodTable[imtSlotIndex] = imtSlot = new LinkedList<InterfaceMethodTableEntry>();

                    imtSlot.AddLast(new InterfaceMethodTableEntry
                    {
                        Function = resolvedFunction,
                        MethodId = GetFunction(resolvedInterfaceMethod).GeneratedValue, // Should be a fake global, that we use as IMT key
                    });
                }
            }
            var interfaceMethodTableConstant = LLVM.ConstArray(intPtrLLVM, interfaceMethodTable.Select(imtSlot =>
            {
                if (imtSlot == null)
                {
                    // No entries: null slot
                    return LLVM.ConstNull(intPtrLLVM);
                }

                if (imtSlot.Count == 1)
                {
                    // Single entry
                    var imtEntry = imtSlot.First.Value;
                    return LLVM.ConstPointerCast(imtEntry.Function.GeneratedValue, intPtrLLVM);
                }
                else
                {
                    // Multiple entries, create IMT array with all entries
                    // TODO: Support covariance/contravariance?
                    var imtEntries = LLVM.ConstArray(imtEntryLLVM, imtSlot.Select(imtEntry =>
                    {
                        return LLVM.ConstNamedStruct(imtEntryLLVM, new[]
                        {
                            imtEntry.MethodId,                                                      // i8* functionId
                            LLVM.ConstPointerCast(imtEntry.Function.GeneratedValue, intPtrLLVM),    // i8* functionPtr
                        });
                    })
                        .Concat(Enumerable.Repeat(LLVM.ConstNull(imtEntryLLVM), 1)).ToArray()); // Append { 0, 0 } terminator
                    var imtEntryGlobal = LLVM.AddGlobal(module, LLVM.TypeOf(imtEntries), @class.Type.TypeReferenceCecil.MangledName() + ".imt");
                    LLVM.SetLinkage(imtEntryGlobal, Linkage.PrivateLinkage);
                    LLVM.SetInitializer(imtEntryGlobal, imtEntries);

                    // Add 1 to differentiate between single entry and IMT array
                    return LLVM.ConstIntToPtr(
                        LLVM.ConstAdd(
                            LLVM.ConstPtrToInt(imtEntryGlobal, nativeIntLLVM),
                            LLVM.ConstInt(nativeIntLLVM, 1, false)),
                        intPtrLLVM);
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
            var superTypeCount = LLVM.ConstInt(int32LLVM, (ulong) @class.Depth + 1, false);
            var interfacesCount = LLVM.ConstInt(int32LLVM, (ulong) @class.Interfaces.Count, false);

            var zero = LLVM.ConstInt(int32LLVM, 0, false);

            // Super types global
            var superTypesConstantGlobal = LLVM.AddGlobal(module, LLVM.ArrayType(intPtrLLVM, (uint) superTypes.Count),
                @class.Type.TypeReferenceCecil.MangledName() + ".supertypes");
            LLVM.SetLinkage(superTypesConstantGlobal, Linkage.PrivateLinkage);
            var superTypesGlobal = LLVM.ConstInBoundsGEP(superTypesConstantGlobal, new[] {zero, zero});

            // Interface map global
            var interfacesConstantGlobal = LLVM.AddGlobal(module, LLVM.ArrayType(intPtrLLVM, (uint) @class.Interfaces.Count),
                @class.Type.TypeReferenceCecil.MangledName() + ".interfaces");
            LLVM.SetLinkage(interfacesConstantGlobal, Linkage.PrivateLinkage);
            var interfacesGlobal = LLVM.ConstInBoundsGEP(interfacesConstantGlobal, new[] {zero, zero});

            // Build VTable
            var vtableConstant = LLVM.ConstNamedStruct(@class.VTableTypeLLVM, @class.VirtualTable.Select(x => x.GeneratedValue).ToArray());

            // Build RTTI
            var runtimeTypeInfoGlobal = @class.GeneratedRuntimeTypeInfoGlobalLLVM;
            var runtimeTypeInfoType = LLVM.GetElementType(LLVM.TypeOf(runtimeTypeInfoGlobal));
            var runtimeTypeInfoTypeElements = new TypeRef[LLVM.CountStructElementTypes(runtimeTypeInfoType)];
            LLVM.GetStructElementTypes(runtimeTypeInfoType, runtimeTypeInfoTypeElements);

            var staticFieldsInitializer = LLVM.ConstNamedStruct(runtimeTypeInfoTypeElements[(int)RuntimeTypeInfoFields.StaticFields], @class.StaticFields.Select(field =>
            {
                var fieldType = field.Value.Type;

                if ((field.Key.Attributes & FieldAttributes.HasFieldRVA) != 0)
                {
                    var initialValue = field.Key.InitialValue;

                    // Seems like if type size is 8, it uses int64 as backing type
                    // Maybe at some point it might be better to encode static fields in a big byte array and use casts instead?
                    if (LLVM.GetTypeKind(fieldType.DefaultTypeLLVM) == TypeKind.IntegerTypeKind)
                    {
                        unsafe
                        {
                            fixed (byte* initalValueStart = initialValue)
                            {
                                if (LLVM.GetIntTypeWidth(fieldType.DefaultTypeLLVM) == 64)
                                    return LLVM.ConstInt(fieldType.DefaultTypeLLVM, *(ulong*)initalValueStart, false);
                                if (LLVM.GetIntTypeWidth(fieldType.DefaultTypeLLVM) == 32)
                                    return LLVM.ConstInt(fieldType.DefaultTypeLLVM, *(uint*)initalValueStart, false);
                            }
                        }
                    }

                    // Otherwise, for now we assume that if there was a RVA, it was a type with custom layout (default type is i8[]),
                    // as currently generated by compiler in <PrivateImplementationDetails> class.
                    if (LLVM.GetTypeKind(fieldType.DefaultTypeLLVM) != TypeKind.ArrayTypeKind)
                        throw new NotSupportedException();

                    var arrayElementType = LLVM.Int8TypeInContext(context);
                    return LLVM.ConstArray(arrayElementType, initialValue.Select(x => LLVM.ConstInt(arrayElementType, x, false)).ToArray());
                }

                return LLVM.ConstNull(fieldType.DefaultTypeLLVM);
            }).ToArray());

            // Get array element type
            var elementTypeRef = @class.Type.TypeReferenceCecil is ArrayType ? ((ArrayType)@class.Type.TypeReferenceCecil).ElementType : null;
            var elementType = elementTypeRef != null ? GetType(elementTypeRef, TypeState.StackComplete) : null;
            var elementTypeSize = elementType != null ? LLVM.ConstIntCast(LLVM.SizeOf(elementType.DefaultTypeLLVM), int32LLVM, false) : LLVM.ConstInt(int32LLVM, 0, false);

            var runtimeTypeInfoConstant = LLVM.ConstNamedStruct(runtimeTypeInfoType, new[]
            {
                @class.BaseType != null ? @class.BaseType.GeneratedRuntimeTypeInfoGlobalLLVM : LLVM.ConstPointerNull(intPtrLLVM),
                superTypeCount,
                interfacesCount,
                superTypesGlobal,
                interfacesGlobal,
                LLVM.ConstInt(LLVM.Int1TypeInContext(context), 0, false), // Class initialized?
                LLVM.ConstIntCast(LLVM.SizeOf(@class.Type.ObjectTypeLLVM), int32LLVM, false),
                elementTypeSize,
                interfaceMethodTableConstant,
                vtableConstant,
                staticFieldsInitializer,
            });
            LLVM.SetInitializer(runtimeTypeInfoGlobal, runtimeTypeInfoConstant);

            // Build super type list (after RTTI since we need pointer to RTTI)
            var superTypesConstant = LLVM.ConstArray(intPtrLLVM,
                superTypes.Select(superType => LLVM.ConstPointerCast(superType.GeneratedRuntimeTypeInfoGlobalLLVM, intPtrLLVM))
                    .ToArray());
            LLVM.SetInitializer(superTypesConstantGlobal, superTypesConstant);

            // Build interface map
            var interfacesConstant = LLVM.ConstArray(intPtrLLVM,
                @class.Interfaces.Select(
                    @interface => LLVM.ConstPointerCast(@interface.GeneratedRuntimeTypeInfoGlobalLLVM, intPtrLLVM)).ToArray());
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
            public ValueRef MethodId;
        }
    }
}