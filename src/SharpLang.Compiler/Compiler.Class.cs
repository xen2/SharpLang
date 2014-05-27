using System;
using System.Collections.Generic;
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
            var vtableType = LLVM.PointerType(LLVM.Int8TypeInContext(context), 0);
            LLVM.StructSetBody(boxedType, new[] { vtableType, dataType }, false);

            @class = new Class(type, typeReference, dataType, boxedType, stackType);
            classes.Add(typeReference, @class);

            if (processFields)
            {
                var typeDefinition = typeReference.Resolve();

                var fieldTypes = new List<TypeRef>(typeDefinition.Fields.Count);

                // Add parent class
                if (typeReference.MetadataType == MetadataType.Class && typeDefinition.BaseType != null)
                {
                    var parentClass = GetClass(ResolveGenericsVisitor.Process(typeReference, typeDefinition.BaseType));
                    @class.BaseType = parentClass;
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