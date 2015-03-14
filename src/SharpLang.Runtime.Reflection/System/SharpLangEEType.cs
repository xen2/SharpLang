using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;

namespace System
{
    /// <summary>
    /// EEType stands for Execution Engine Type. It is the structure used by the runtime
    /// to store various type information, and stored in a pointer at offset 0 of every reference object.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct SharpLangEEType
    {
        public SharpLangEEType* Base;

        public byte IsConcreteType;
        public CorElementType CorElementType;

        // Metadata
        public SharpLangEETypeDefinition TypeDefinition;
        public IntPtr ExtraTypeInfo;
        public IntPtr CachedTypeField;

        // Field infos
        public ushort GarbageCollectableFieldCount; // First entries in FieldDescriptions will be for the GC: instance fields of referencable types
        public ushort FieldCount;

        public SharpLangFieldDescription* FieldDescriptions;

        // Concrete type info
        public uint SuperTypeCount;
        public uint InterfacesCount;
        public SharpLangEEType** SuperTypes;
        public SharpLangEEType** InterfaceMap;
        public byte Initialized;
        public uint ObjectSize;
        public uint ElementSize;

        // Then we have IMT
        // We would like to use fixed [19], but it doesn't work with IntPtr
        public IntPtr IMT1;
        public IntPtr IMT2;
        public IntPtr IMT3;
        public IntPtr IMT4;
        public IntPtr IMT5;
        public IntPtr IMT6;
        public IntPtr IMT7;
        public IntPtr IMT8;
        public IntPtr IMT9;
        public IntPtr IMT10;
        public IntPtr IMT11;
        public IntPtr IMT12;
        public IntPtr IMT13;
        public IntPtr IMT14;
        public IntPtr IMT15;
        public IntPtr IMT16;
        public IntPtr IMT17;
        public IntPtr IMT18;
        public IntPtr IMT19;

        // And the VTable
        public uint VirtualTableSize;
        public IntPtr VirtualTable;

        // Encoded in low bits of ExtraTypeInfo
        public enum Kind
        {
            TypeDef = -1,
            Generics = 0,
            Array = 1,
            Pointer = 2,
            ByRef = 3,
        }

        public Kind GetKind()
        {
            if (ExtraTypeInfo != IntPtr.Zero)
            {
                return (Kind)(ExtraTypeInfo.ToInt32() & 3);
            }

            return Kind.TypeDef;
        }

        public SharpLangEEType* GetElementType()
        {
            var kind = GetKind();
            if (kind == Kind.TypeDef)
                return null;

            return (SharpLangEEType*)(ExtraTypeInfo - (int)kind);
        }

        // Used by mscorlib binder
        internal unsafe SharpLangFieldDescription* FindField(byte* name)
        {
            var module = TypeDefinition.Module;
            var stringComparer = module.MetadataReader.StringComparer;

            var fieldCount = FieldCount;
            for (int fieldIndex = 0; fieldIndex < fieldCount; ++fieldIndex)
            {
                var fieldDesc = &FieldDescriptions[fieldIndex];
                var field = module.MetadataReader.GetFieldDefinition(fieldDesc->FieldDefinitionHandle);
                if (stringComparer.Equals(field.Name, name))
                {
                    return fieldDesc;
                }
            }

            return null;
        }
    }
}