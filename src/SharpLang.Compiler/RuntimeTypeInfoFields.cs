namespace SharpLang.CompilerServices
{
    /// <summary>
    /// Describes the field indices in RTTI structure.
    /// </summary>
    enum RuntimeTypeInfoFields
    {
        Base = 0,
        SuperTypeCount,
        InterfacesCount,
        SuperTypes,
        InterfaceMap,
        InterfaceMethodTable,
        VirtualTable,
        StaticFields,
    }
}