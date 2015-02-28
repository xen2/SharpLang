namespace SharpLang.CompilerServices
{
    /// <summary>
    /// Describes the field indices in RTTI structure.
    /// </summary>
    enum RuntimeTypeInfoFields
    {
        Base = 0,

        // Interface & generic type def don't have real type info (they exist just for Ldtoken/typeof())
        // There is no info past "Type".
        IsConcreteType,

        // Metadata/Reflection
        // TypeDef or GenericType
        TypeDefinition,
        // GenericType: generic arguments (null terminated); Array/Pointer/ByRef: element type
        ExtraTypeInfo,

        // Cached RuntimeType (lazy initialized)
        Type,

        // Fields
        GarbageCollectableFieldCount, // First fields in FieldDescriptions will be the one interesting for GC: instance fields of referencable types
        FieldCount,
        FieldDescriptions,

        // This part is valid only if it's a concrete type (no interface, no generic type def)
        SuperTypeCount,
        InterfacesCount,
        SuperTypes,
        InterfaceMap,
        TypeInitialized,
        ObjectSize,
        ElementSize,
        
        // IMT, where interface methods are stored in a hash table
        InterfaceMethodTable,

        // Virtual methods (including inherited), followed by non-virtual methods (only for current type)
        VirtualTableSize,
        VirtualTable,

        // Static fields of this type
        StaticFields,
    }
}