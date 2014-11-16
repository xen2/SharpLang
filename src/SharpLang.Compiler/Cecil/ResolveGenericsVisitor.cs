using System;
using Mono.Cecil;

namespace SharpLang.CompilerServices.Cecil
{
    /// <summary>
    /// Transform open generic types to closed instantiation using context information.
    /// See <see cref="Process"/> for more details.
    /// </summary>
    class ResolveGenericsVisitor : TypeReferenceVisitor
    {
        private readonly GenericInstanceMethod genericContextMethod;
        private readonly GenericInstanceType genericContextType;
        private readonly bool resolveOnlyReferences;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResolveGenericsVisitor"/> class.
        /// </summary>
        /// <param name="genericContext">The generic context.</param>
        /// <param name="resolveOnlyReferences">if set to <c>true</c> replaces only GenericInstance of type !0 with their real definitions (T, U, etc...), otherwise replace them with real types.</param>
        public ResolveGenericsVisitor(MethodReference genericContext, bool resolveOnlyReferences = false)
        {
            genericContextMethod = genericContext as GenericInstanceMethod;
            genericContextType = genericContext.DeclaringType as GenericInstanceType;
            this.resolveOnlyReferences = resolveOnlyReferences;
        }

        public ResolveGenericsVisitor(TypeReference genericContext)
        {
            genericContextType = genericContext as GenericInstanceType;
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

            // Visit recursively and replace generic parameters with generic arguments from context
            var visitor = new ResolveGenericsVisitor(context);
            var result = visitor.VisitDynamic(type);

            // Make sure type is closed now
            if (result.ContainsGenericParameter)
                throw new InvalidOperationException("Unsupported generic resolution.");

            return result;
        }

        public static TypeReference Process(MethodReference context, TypeReference type, bool resolveOnlyReferences = false)
        {
            if (type == null)
                return null;

            if (context == null)
                return type;

            // Visit recursively and replace generic parameters with generic arguments from context
            var genericInstanceTypeContext = context.DeclaringType as GenericInstanceType;
            var genericInstanceMethodContext = context as GenericInstanceMethod;
            if (genericInstanceMethodContext == null && genericInstanceTypeContext == null)
                return type;

            var visitor = new ResolveGenericsVisitor(context, resolveOnlyReferences);
            var result = visitor.VisitDynamic(type);

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
        }

        public static bool ContainsGenericParameters(MethodReference method)
        {
            // Determine if method contains any open generic type.
            // TODO: Might need a more robust generic resolver/analyzer system soon.

            // First, check resolved declaring type
            if (Process(method, method.DeclaringType).ContainsGenericParameter)
                return true;

            var genericInstanceMethod = method as GenericInstanceMethod;
            if (genericInstanceMethod != null)
            {
                // Check that each generic argument is closed
                foreach (var genericArgument in genericInstanceMethod.GenericArguments)
                    if (Process(method, genericArgument).ContainsGenericParameter)
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
            if (type.Type == GenericParameterType.Method)
            {
                if (genericContextMethod != null && type.Position < genericContextMethod.GenericArguments.Count)
                {
                    // Look for generic parameter in both resolved and element method
                    var genericContext1 = genericContextMethod.ElementMethod;
                    var genericContext2 = genericContextMethod.Resolve();
                    var genericParameter1 = genericContext1.GenericParameters[type.Position];
                    var genericParameter2 = genericContext2.GenericParameters[type.Position];

                    if (resolveOnlyReferences)
                    {
                        if (genericParameter1.Name == type.Name)
                            return genericParameter2;
                    }
                    else
                    {
                        if (genericParameter1.Name == type.Name)
                            return genericContextMethod.GenericArguments[type.Position];

                        if (genericParameter2.Name == type.Name)
                            return genericContextMethod.GenericArguments[type.Position];
                    }
                }
            }
            else
            {
                if (genericContextType != null && type.Position < genericContextType.GenericArguments.Count)
                {
                    // Look for generic parameter in both resolved and element method
                    var genericContext1 = genericContextType.ElementType;
                    var genericContext2 = genericContextType.Resolve();
                    var genericParameter1 = genericContext1.GenericParameters[type.Position];
                    var genericParameter2 = genericContext2.GenericParameters[type.Position];

                    if (resolveOnlyReferences)
                    {
                        if (genericParameter1.Name == type.Name)
                            return genericParameter2;
                    }
                    else
                    {
                        if (genericParameter1.Name == type.Name)
                            return genericContextType.GenericArguments[type.Position];

                        if (genericParameter2.Name == type.Name)
                            return genericContextType.GenericArguments[type.Position];
                    }
                }
            }

            return base.Visit(type);
        }
    }
}