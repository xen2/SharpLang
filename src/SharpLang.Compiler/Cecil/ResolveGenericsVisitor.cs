using System;
using System.Collections.Generic;
using Mono.Cecil;

namespace SharpLang.CompilerServices.Cecil
{
    /// <summary>
    /// Transform open generic types to closed instantiation using context information.
    /// See <see cref="Process"/> for more details.
    /// </summary>
    class ResolveGenericsVisitor : TypeReferenceVisitor
    {
        private Dictionary<TypeReference, TypeReference> genericTypeMapping;

        public ResolveGenericsVisitor(Dictionary<TypeReference, TypeReference> genericTypeMapping)
        {
            this.genericTypeMapping = genericTypeMapping;
        }

        /// <summary>
        /// Transform open generic types to closed instantiation using context information.
        /// As an example, if B{T} inherits from A{T}, running it with B{C} as context and A{B.T} as type, ti will return A{C}.
        /// </summary>
        public static TypeReference Process(TypeReference context, TypeReference type)
        {
            if (type == null)
                return null;

            var genericInstanceTypeContext = context as GenericInstanceType;
            if (genericInstanceTypeContext == null)
                return type;

            // Build dictionary that will map generic type to their real implementation type
            var resolvedType = genericInstanceTypeContext.Resolve();
            var genericTypeMapping = new Dictionary<TypeReference, TypeReference>();
            for (int i = 0; i < resolvedType.GenericParameters.Count; ++i)
            {
                var genericParameter = resolvedType.GenericParameters[i];
                genericTypeMapping.Add(genericParameter, genericInstanceTypeContext.GenericArguments[i]);
            }

            var visitor = new ResolveGenericsVisitor(genericTypeMapping);
            var result = visitor.VisitDynamic(type);

            // Make sure type is closed now
            if (result.ContainsGenericParameter())
                throw new InvalidOperationException("Unsupported generic resolution.");

            return result;
        }

        public static TypeReference Process(MethodReference context, TypeReference type)
        {
            if (type == null)
                return null;

            if (context == null)
                return type;

            var genericInstanceTypeContext = context.DeclaringType as GenericInstanceType;
            var genericInstanceMethodContext = context as GenericInstanceMethod;
            if (genericInstanceMethodContext == null && genericInstanceTypeContext == null)
                return type;

            // Build dictionary that will map generic type to their real implementation type
            var genericTypeMapping = new Dictionary<TypeReference, TypeReference>(TypeReferenceComparer.Default);
            if (genericInstanceTypeContext != null)
            {
                var resolvedType = genericInstanceTypeContext.ElementType;
                for (int i = 0; i < resolvedType.GenericParameters.Count; ++i)
                {
                    var genericParameter = genericInstanceTypeContext.ElementType.GenericParameters[i];
                    genericTypeMapping.Add(genericParameter, genericInstanceTypeContext.GenericArguments[i]);
                }
            }

            if (genericInstanceMethodContext != null)
            {
                var elementMethod = genericInstanceMethodContext.ElementMethod;
                var resolvedMethod = genericInstanceMethodContext.Resolve();
                for (int i = 0; i < elementMethod.GenericParameters.Count; ++i)
                {
                    var genericParameter = elementMethod.GenericParameters[i];
                    genericTypeMapping.Add(genericParameter, genericInstanceMethodContext.GenericArguments[i]);

                    var genericParameter2 = resolvedMethod.GenericParameters[i];
                    if (genericParameter != genericParameter2)
                        genericTypeMapping.Add(genericParameter2, genericInstanceMethodContext.GenericArguments[i]);
                }
            }

            var visitor = new ResolveGenericsVisitor(genericTypeMapping);
            var result = visitor.VisitDynamic(type);

            // Make sure type is closed now
            //if (result.ContainsGenericParameter())
            //    throw new InvalidOperationException("Unsupported generic resolution.");

            return result;
        }

        public static MethodReference Process(MethodReference context, MethodReference method)
        {
            var genericInstanceMethod = method as GenericInstanceMethod;
            if (genericInstanceMethod == null)
            {
                // Resolve declaring type
                var declaringType = method.DeclaringType;
                var newDeclaringType = Process(context, declaringType);
                if (newDeclaringType != declaringType)
                {
                    var result1 = new MethodReference(method.Name, method.ReturnType, newDeclaringType)
                    {
                        HasThis = method.HasThis,
                        ExplicitThis = method.ExplicitThis,
                        CallingConvention = method.CallingConvention,
                    };
                
                    foreach (var parameter in method.Parameters)
                        result1.Parameters.Add(new ParameterDefinition(parameter.ParameterType));
                
                    foreach (var generic_parameter in method.GenericParameters)
                        result1.GenericParameters.Add(new GenericParameter(generic_parameter.Name, result1));

                    return result1;
                }

                return method;
            }

            var result2 = new GenericInstanceMethod(Process(context, Process(context, genericInstanceMethod.ElementMethod)));

            foreach (var genericArgument in genericInstanceMethod.GenericArguments)
                result2.GenericArguments.Add(Process(context, genericArgument));

            return result2;
            //else
            //{
            //    result = new MethodReference(method.Name, Process(context, method.ReturnType), Process(context, method.DeclaringType))
            //    {
            //        HasThis = method.HasThis,
            //        ExplicitThis = method.ExplicitThis,
            //        CallingConvention = method.CallingConvention,
            //    };
            //
            //    foreach (var parameter in method.Parameters)
            //        result.Parameters.Add(new ParameterDefinition(Process(context, parameter.ParameterType)));
            //}
            //
            //foreach (var generic_parameter in method.GenericParameters)
            //    result.GenericParameters.Add(new GenericParameter(generic_parameter.Name, result));
        }

        public static bool ContainsGenericParameters(MethodReference method)
        {
            // Determine if method contains any open generic type.
            // TODO: Might need a more robust generic resolver/analyzer system soon.

            // First, check resolved declaring type
            if (Process(method, method.DeclaringType).ContainsGenericParameter())
                return true;

            var genericInstanceMethod = method as GenericInstanceMethod;
            if (genericInstanceMethod != null)
            {
                // Check that each generic argument is closed
                foreach (var genericArgument in genericInstanceMethod.GenericArguments)
                    if (Process(method, genericArgument).ContainsGenericParameter())
                        return true;

                return false;
            }
            else
            {
                // If it's not a GenericInstanceMethod, it shouldn't have any generic parameters
                return method.HasGenericParameters;
            }
        }

        public override TypeReference Visit(GenericParameter type)
        {
            TypeReference result;
            if (genericTypeMapping.TryGetValue(type, out result))
                return result;

            return base.Visit(type);
        }

        class TypeReferenceComparer : EqualityComparer<TypeReference>
        {
            public static readonly EqualityComparer<TypeReference> Default =  new TypeReferenceComparer();

            public override bool Equals(TypeReference x, TypeReference y)
            {
                return x.FullName == y.FullName;
            }

            public override int GetHashCode(TypeReference obj)
            {
                return obj.FullName.GetHashCode();
            }
        }
    }
}