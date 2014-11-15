using System.Collections.Generic;
using Mono.Cecil;

namespace SharpLang.CompilerServices
{
    public class MemberEqualityComparer : IEqualityComparer<TypeReference>, IEqualityComparer<MethodReference>, IEqualityComparer<FieldReference>
    {
        public static readonly MemberEqualityComparer Default = new MemberEqualityComparer();

        public bool Equals(TypeReference x, TypeReference y)
        {
            return AreSame(x, y);
        }

        public bool Equals(MethodReference x, MethodReference y)
        {
            return AreSame(x, y);
        }

        public bool Equals(FieldReference x, FieldReference y)
        {
            if (ReferenceEquals(x, y))
                return true;

            return AreSame(x, y);
        }

        public int GetHashCode(TypeReference typeReference)
        {
            unchecked
            {
                // Sometimes, IsValueType is not properly set in type references, so let's map it to Class.
                var metadataType = typeReference.MetadataType;
                if (metadataType == MetadataType.ValueType)
                    metadataType = MetadataType.Class;
                var result = ((byte)metadataType).GetHashCode();

                var typeSpecification = typeReference as TypeSpecification;
                if (typeSpecification != null)
                {
                    result ^= GetHashCode(typeSpecification.ElementType);

                    var genericInstanceType = typeSpecification as GenericInstanceType;
                    if (genericInstanceType != null)
                    {
                        foreach (var arg in genericInstanceType.GenericArguments)
                            result = result * 23 + GetHashCode(arg);
                    }
                }
                else
                {
                    // TODO: Not sure if comparing declaring type should be done for GenericParameter
                    if (typeReference.DeclaringType != null)
                        result ^= GetHashCode(typeReference.DeclaringType);

                    var genericParameter = typeReference as GenericParameter;
                    if (genericParameter != null)
                    {
                        result ^= genericParameter.Position;
                    }
                    else
                    {
                        result ^= typeReference.Name.GetHashCode();
                        result ^= typeReference.Namespace.GetHashCode();
                    }
                }

                return result;
            }
        }

        public int GetHashCode(MethodReference methodReference)
        {
            unchecked
            {
                int result;

                var genericInstanceMethod = methodReference as GenericInstanceMethod;
                if (genericInstanceMethod != null)
                {
                    result = GetHashCode(genericInstanceMethod.ElementMethod);

                    foreach (var arg in genericInstanceMethod.GenericArguments)
                        result = result * 23 + GetHashCode(arg);
                }
                else
                {
                    result = methodReference.Name.GetHashCode();
                    result ^= GetHashCode(methodReference.DeclaringType);

                    // TODO: Investigate why hashing parameter types seems to cause issues (virtual methods with no slots)
                    foreach (var parameter in methodReference.Parameters)
                        result = result * 23 + GetHashCode(parameter.ParameterType);
                }

                return result;
            }
        }

        public int GetHashCode(FieldReference fieldReference)
        {
            unchecked
            {
                var result = fieldReference.Name.GetHashCode();
                result ^= GetHashCode(fieldReference.DeclaringType);
                return result;
            }
        }

        static bool AreSame(FieldReference a, FieldReference b)
        {
            if (a.Name != b.Name)
                return false;

            if (!AreSame(a.DeclaringType, b.DeclaringType))
                return false;

            if (!AreSame(a.FieldType, b.FieldType))
                return false;

            return true;
        }

        static bool AreSame(MethodReference a, MethodReference b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (a.Name != b.Name)
                return false;

            if (!AreSame(a.DeclaringType, b.DeclaringType))
                return false;

            if (a.HasGenericParameters != b.HasGenericParameters)
                return false;

            if (a.IsGenericInstance != b.IsGenericInstance)
                return false;

            if (a.IsGenericInstance && !AreSame(((GenericInstanceMethod)a).GenericArguments, ((GenericInstanceMethod)b).GenericArguments))
                return false;

            if (a.HasGenericParameters && a.GenericParameters.Count != b.GenericParameters.Count)
                return false;

            if (!AreSame(a.ReturnType, b.ReturnType))
                return false;

            if (a.HasParameters != b.HasParameters)
                return false;

            if (a.HasParameters != b.HasParameters)
                return false;

            if (a.HasParameters && !AreSame(a.Parameters, b.Parameters))
                return false;

            return true;
        }

        static bool AreSame(Mono.Collections.Generic.Collection<TypeReference> a, Mono.Collections.Generic.Collection<TypeReference> b)
        {
            var count = a.Count;

            if (count != b.Count)
                return false;

            if (count == 0)
                return true;

            for (int i = 0; i < count; i++)
                if (!AreSame(a[i], b[i]))
                    return false;

            return true;
        }

        static bool AreSame(Mono.Collections.Generic.Collection<ParameterDefinition> a, Mono.Collections.Generic.Collection<ParameterDefinition> b)
        {
            var count = a.Count;

            if (count != b.Count)
                return false;

            if (count == 0)
                return true;

            for (int i = 0; i < count; i++)
                if (!AreSame(a[i].ParameterType, b[i].ParameterType))
                    return false;

            return true;
        }

        static bool AreSame(TypeSpecification a, TypeSpecification b)
        {
            if (!AreSame(a.ElementType, b.ElementType))
                return false;

            if (a.IsGenericInstance)
                return AreSame((GenericInstanceType)a, (GenericInstanceType)b);

            if (a.IsRequiredModifier || a.IsOptionalModifier)
                return AreSame((IModifierType)a, (IModifierType)b);

            if (a.IsArray)
                return AreSame((ArrayType)a, (ArrayType)b);

            return true;
        }

        static bool AreSame(ArrayType a, ArrayType b)
        {
            if (a.Rank != b.Rank)
                return false;

            // TODO: dimensions

            return true;
        }

        static bool AreSame(IModifierType a, IModifierType b)
        {
            return AreSame(a.ModifierType, b.ModifierType);
        }

        static bool AreSame(GenericInstanceType a, GenericInstanceType b)
        {
            if (a.GenericArguments.Count != b.GenericArguments.Count)
                return false;

            for (int i = 0; i < a.GenericArguments.Count; i++)
                if (!AreSame(a.GenericArguments[i], b.GenericArguments[i]))
                    return false;

            return true;
        }

        static bool AreSame(GenericParameter a, GenericParameter b)
        {
            return a.Position == b.Position;
        }

        static bool AreSame(TypeReference a, TypeReference b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (a == null || b == null)
                return false;

            if (a.MetadataType != b.MetadataType)
                return false;

            if (a.IsGenericParameter)
                return AreSame((GenericParameter)a, (GenericParameter)b);

            if (a is TypeSpecification)
                return AreSame((TypeSpecification)a, (TypeSpecification)b);

            if (a.Name != b.Name || a.Namespace != b.Namespace)
                return false;

            //TODO: check scope
            if (!AreSame(a.DeclaringType, b.DeclaringType))
                return false;

            if (!a.HasGenericParameters && !b.HasGenericParameters
                && !(a is GenericInstanceType) && !(b is GenericInstanceType))
                if (a.Resolve().Module != b.Resolve().Module)
                    return false;

            return true;
        }
    }
}