using System;
using System.Collections.Generic;
using Mono.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices.Cecil
{
    /// <summary>
    /// Visit Cecil types recursively, and replace them if requested.
    /// </summary>
    public class TypeReferenceVisitor
    {
        protected IList<TypeReference> VisitDynamicList<T>(IList<T> list) where T : TypeReference
        {
            var result = new List<TypeReference>(list);
            for (int i = 0; i < list.Count; i++)
            {
                var item = result[i];

                var newNode = VisitDynamic(item);

                if (newNode == null)
                {
                    result.RemoveAt(i);
                    i--;
                }
                else if (!ReferenceEquals(newNode, item))
                {
                    result[i] = newNode;
                }
            }
            return result;
        }

        public virtual TypeReference VisitDynamic(TypeReference type)
        {
            var byrefType = type as ByReferenceType;
            if (byrefType != null)
                return Visit(byrefType);

            var requiredModifierType = type as RequiredModifierType;
            if (requiredModifierType != null)
                return Visit(requiredModifierType);

            var pointerType = type as PointerType;
            if (pointerType != null)
                return Visit(pointerType);

            var arrayType = type as ArrayType;
            if (arrayType != null)
                return Visit(arrayType);

            var genericInstanceType = type as GenericInstanceType;
            if (genericInstanceType != null)
                return Visit(genericInstanceType);

            var genericParameter = type as GenericParameter;
            if (genericParameter != null)
                return Visit(genericParameter);

            if (type.GetType() != typeof(TypeReference) && type.GetType() != typeof(TypeDefinition))
                throw new NotSupportedException();

            return Visit(type);
        }

        public virtual TypeReference Visit(GenericParameter type)
        {
            return type;
        }

        public virtual IEnumerable<GenericParameter> Visit(IEnumerable<GenericParameter> genericParameters)
        {
            return genericParameters;
        }

        public virtual TypeReference Visit(TypeReference type)
        {
            return type.ChangeGenericParameters(Visit(type.GenericParameters));
        }

        public virtual TypeReference Visit(ArrayType type)
        {
            type = type.ChangeArrayType(VisitDynamic(type.ElementType), type.Rank);
            return type.ChangeGenericParameters(Visit(type.GenericParameters));
        }

        public virtual TypeReference Visit(ByReferenceType type)
        {
            type = type.ChangeByReferenceType(VisitDynamic(type.ElementType));
            return type.ChangeGenericParameters(Visit(type.GenericParameters));
        }

        public virtual TypeReference Visit(PointerType type)
        {
            type = type.ChangePointerType(VisitDynamic(type.ElementType));
            return type.ChangeGenericParameters(Visit(type.GenericParameters));
        }

        public virtual TypeReference Visit(RequiredModifierType type)
        {
            type = type.ChangeRequiredModifierType(VisitDynamic(type.ElementType));
            type.ModifierType = VisitDynamic(type.ModifierType);
            return type.ChangeGenericParameters(Visit(type.GenericParameters));
        }

        public virtual TypeReference Visit(GenericInstanceType type)
        {
            type = type.ChangeGenericInstanceType(VisitDynamic(type.ElementType), VisitDynamicList(type.GenericArguments));
            return type.ChangeGenericParameters(Visit(type.GenericParameters));
        }
    }
}