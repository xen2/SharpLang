using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SharpLang.CompilerServices.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public partial class Compiler
    {
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
                }

                // Build methods slots
                // TODO: This will trigger their compilation, but maybe we might want to defer that later
                // (esp. since vtable is not built yet => recursion issues)
                CompileClassMethods(@class);

                // Get parent type RTTI
                var parentRuntimeTypeInfo = parentClass != null
                    ? parentClass.GeneratedRuntimeTypeInfoGlobal
                    : LLVM.ConstPointerNull(LLVM.PointerType(LLVM.Int8TypeInContext(context), 0));

                // Build vtable
                var vtableConstant = LLVM.ConstStructInContext(context, @class.VirtualTable.Select(x => x.GeneratedValue).ToArray(), false);

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
                var runtimeTypeInfoConstant = LLVM.ConstStructInContext(context, new[] { parentRuntimeTypeInfo, vtableConstant, staticFieldsEmpty }, false);
                var vtableGlobal = LLVM.AddGlobal(module, LLVM.TypeOf(runtimeTypeInfoConstant), string.Empty);
                LLVM.SetInitializer(vtableGlobal, runtimeTypeInfoConstant);
                LLVM.StructSetBody(boxedType, new[] { LLVM.TypeOf(vtableGlobal), dataType }, false);
                @class.GeneratedRuntimeTypeInfoGlobal = vtableGlobal;

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
    }
}