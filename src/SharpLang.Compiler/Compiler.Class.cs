using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public partial class Compiler
    {
        /// <summary>
        /// Gets the specified class.
        /// </summary>
        /// <param name="typeDefinition">The type definition.</param>
        /// <returns></returns>
        private Class GetClass(TypeDefinition typeDefinition)
        {
            Class @class;
            if (!classes.TryGetValue(typeDefinition, out @class))
                throw new InvalidOperationException(string.Format("Could not find class {0}", typeDefinition));

            return @class;
        }

        /// <summary>
        /// Compiles the specified class.
        /// </summary>
        /// <param name="typeDefinition">The type definition.</param>
        /// <returns></returns>
        private Class CreateClass(TypeDefinition typeDefinition)
        {
            Class @class;
            if (classes.TryGetValue(typeDefinition, out @class))
                return @class;

            TypeRef dataType;
            StackValueType stackType;
            bool processFields = false;

            switch (typeDefinition.MetadataType)
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
                    // Non recursive type, get info through Type
                    var type = GetType(typeDefinition);
                    dataType = type.GeneratedType;
                    stackType = type.StackType;

                    break;
                }
                case MetadataType.ValueType:
                case MetadataType.Class:
                    // Process non-static fields
                    dataType = LLVM.StructCreateNamed(context, typeDefinition.FullName);
                    processFields = true;
                    stackType = typeDefinition.MetadataType == MetadataType.ValueType ? StackValueType.Value : StackValueType.Object;
                    break;
                default:
                    throw new NotImplementedException();
            }

            // Create class version (boxed version with VTable)
            var boxedType = LLVM.StructCreateNamed(context, typeDefinition.FullName);
            var vtableType = LLVM.PointerType(LLVM.Int8TypeInContext(context), 0);
            LLVM.StructSetBody(boxedType, new[] { vtableType, dataType }, false);

            @class = new Class(typeDefinition, dataType, boxedType, stackType);
            classes.Add(typeDefinition, @class);

            if (processFields)
            {
                var fieldTypes = new List<TypeRef>(typeDefinition.Fields.Count);

                // Add parent class
                if (typeDefinition.MetadataType == MetadataType.Class && typeDefinition.BaseType != null)
                {
                    var parentClass = CreateClass(typeDefinition.BaseType.Resolve());
                    fieldTypes.Add(parentClass.DataType);
                }

                foreach (var field in typeDefinition.Fields)
                {
                    if (field.IsStatic)
                        continue;

                    var fieldType = CreateType(assembly.MainModule.Import(field.FieldType));
                    @class.Fields.Add(field, new Field(@class, fieldType, fieldTypes.Count));
                    fieldTypes.Add(fieldType.GeneratedType);
                }

                LLVM.StructSetBody(dataType, fieldTypes.ToArray(), false);
            }

            return @class;
        }
    }
}