using System.Collections.Generic;

namespace System
{
    /// <summary>
    /// Comparer for <see cref="SharpLangEETypePtr"/>.
    /// This allows us for easy lookup in a pre-sorted list of all live EEType.
    /// For example, when using MakeGenericType/MakeArrayType, we want to find matching EEType if it exists.
    /// </summary>
    class SharpLangEETypeComparer : Comparer<SharpLangEETypePtr>
    {
        public static readonly SharpLangEETypeComparer Default = new SharpLangEETypeComparer();

        public unsafe override int Compare(SharpLangEETypePtr x, SharpLangEETypePtr y)
        {
            if (x.Value == y.Value)
                return 0;

            // Order by kind
            var kindDiff = x.Value->GetKind() - y.Value->GetKind();

            if (kindDiff != 0)
                return kindDiff;

            switch (x.Value->GetKind())
            {
                case SharpLangEEType.Kind.TypeDef:
                {
                    return Compare(x.Value->TypeDefinition, y.Value->TypeDefinition);
                }
                case SharpLangEEType.Kind.Generics:
                {
                    // Compare generic type definition
                    var genericTypeComparison = Compare(x.Value->TypeDefinition, y.Value->TypeDefinition);
                    if (genericTypeComparison != 0)
                        return genericTypeComparison;

                    // Compare generic argument list
                    var xGenericArgument = (SharpLangEEType**)x.Value->GetElementType();
                    var yGenericArgument = (SharpLangEEType**)y.Value->GetElementType();
                    while (*xGenericArgument != null && *yGenericArgument != null)
                    {
                        var genericArgumentComparison = Compare(*xGenericArgument++, *yGenericArgument++);
                        if (genericArgumentComparison != 0)
                            return genericArgumentComparison;
                    }

                    // If one list was longer than the other, use it
                    if (*xGenericArgument != null)
                        return 1;
                    if (*yGenericArgument != null)
                        return -1;

                    // Generic types are the same
                    return 0;
                }
                case SharpLangEEType.Kind.Array:
                case SharpLangEEType.Kind.Pointer:
                case SharpLangEEType.Kind.ByRef:
                {
                    // Compare element types
                    return Compare(x.Value->GetElementType(), y.Value->GetElementType());
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static unsafe int Compare(SharpLangEETypeDefinition x, SharpLangEETypeDefinition y)
        {
            // Compare first by module, then token ID
            // TODO: Better to use something like assembly qualified name so that we can presort at compile time instead of runtime
            if ((void*)x.ModulePointer > (void*)y.ModulePointer)
                return 1;
            if ((void*)x.ModulePointer < (void*)y.ModulePointer)
                return -1;

            return (*(uint*)&x.Handle).CompareTo(*(uint*)&y.Handle);
        }

        public static unsafe int Compare(SharpLangEETypePtr x, ref SharpLangTypeSearchKey y)
        {
            // Order by kind
            var kindDiff = x.Value->GetKind() - y.Kind;

            if (kindDiff != 0)
                return kindDiff;

            switch (x.Value->GetKind())
            {
                case SharpLangEEType.Kind.TypeDef:
                {
                    return Compare(x.Value->TypeDefinition, y.TypeDefinition);
                }
                case SharpLangEEType.Kind.Generics:
                {
                    // Compare generic type definition
                    var genericTypeComparison = Compare(x.Value->TypeDefinition, y.TypeDefinition);
                    if (genericTypeComparison != 0)
                        return genericTypeComparison;

                    // Compare generic argument list
                    var xGenericArgument = (SharpLangEEType**)x.Value->GetElementType();
                    var yGenericArgumentIndex = 0;
                    while (*xGenericArgument != null && yGenericArgumentIndex < y.GenericArguments.Length)
                    {
                        var genericArgumentComparison = Default.Compare(*xGenericArgument++, y.GenericArguments[yGenericArgumentIndex++].EEType);
                        if (genericArgumentComparison != 0)
                            return genericArgumentComparison;
                    }

                    // If one list was longer than the other, use it
                    if (*xGenericArgument != null)
                        return 1;
                    if (yGenericArgumentIndex < y.GenericArguments.Length)
                        return -1;

                    // Generic types are the same
                    return 0;
                }
                case SharpLangEEType.Kind.Array:
                case SharpLangEEType.Kind.Pointer:
                case SharpLangEEType.Kind.ByRef:
                {
                    // Compare element types
                    return Default.Compare(x.Value->GetElementType(), y.ElementType.EEType);
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static int BinarySearch(List<SharpLangEETypePtr> types, ref SharpLangTypeSearchKey key)
        {
            int start = 0;
            int end = types.Count - 1;
            while (start <= end)
            {
                int middle = start + ((end - start) >> 1);
                var compareResult = Compare(types[middle], ref key);
                
                if (compareResult == 0)
                {
                    return middle;
                }
                if (compareResult < 0)
                {
                    start = middle + 1;
                }
                else
                {
                    end = middle - 1;
                }
            }
            return ~start;
        }

        public struct SharpLangTypeSearchKey
        {
            public SharpLangEEType.Kind Kind;

            // Used for TypeDef and Generics
            public SharpLangEETypeDefinition TypeDefinition;

            // Used for Array, ByRef and Pointer
            public SharpLangType ElementType;

            // Used for Generics
            public SharpLangType[] GenericArguments;
        }
    }
}