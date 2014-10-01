using System.Collections.Generic;
using Mono.Cecil;

namespace SharpLang.CompilerServices
{
    public class MemberEqualityComparer : IEqualityComparer<MemberReference>
    {
        public static readonly MemberEqualityComparer Default = new MemberEqualityComparer();

        public bool Equals(MemberReference x, MemberReference y)
        {
            if (ReferenceEquals(x, y))
                return true;

            // Fast path
            if (x.FullName == y.FullName)
                return true;

            return AreSame(x, y);
        }

        public int GetHashCode(MemberReference obj)
        {
            return ComputeHashCode(obj);
        }

        static int ComputeHashCode(MemberReference obj)
        {
            if (obj == null)
                return 0;

            unchecked
            {
                var result = obj.Name.GetHashCode();

                var typeReference = obj as TypeReference;
                if (typeReference != null)
                {
                    result ^= typeReference.Namespace.GetHashCode();
                }
                else
                {
                    var methodReference = obj as MethodReference;
                    if (methodReference != null)
                    {
                        result ^= ComputeHashCode(methodReference.DeclaringType);
                    }
                }

                return result;
            }
        }

        static bool AreSame(MemberReference a, MemberReference b)
        {
            if (a is TypeReference && b is TypeReference)
                return AreSame((TypeReference)a, (TypeReference)b);

            if (a is MethodReference && b is MethodReference)
                return AreSame((MethodReference)a, (MethodReference)b);

            return false;
        }


        static bool AreSame(MethodReference a, MethodReference b)
        {
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

            return AreSame(a.DeclaringType, b.DeclaringType);
        }
    }
}