// Source: http://stackoverflow.com/questions/4968755/mono-cecil-call-generic-base-class-method-from-other-assembly

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SharpLang.CompilerServices.Cecil
{
    /// <summary>
    /// Various useful extension methods for Cecil.
    /// </summary>
    static partial class CecilExtensions
    {
        public static string MangledName(this TypeReference typeReference)
        {
            var assembly = (typeReference.Resolve() ?? typeReference).Module.Assembly;
            if (assembly.Name.Name == "mscorlib")
                return typeReference.FullName;

            return assembly.Name.Name + "_" + typeReference.FullName;
        }

        public static string MangledName(this MethodReference method)
        {
            var builder = new StringBuilder();
            if (method.DeclaringType != null)
                builder.Append(method.DeclaringType.MangledName()).Append("::");
            builder.Append(method.Name);

            // Append generic arguments
            var genericInstanceMethod = method as GenericInstanceMethod;
            if (genericInstanceMethod != null)
            {
                foreach (var genericArgument in genericInstanceMethod.GenericArguments)
                {
                    builder.Append('_');
                    builder.Append(genericArgument.MangledName());
                }
            }

            if (method.Name == "op_Implicit" || method.Name == "op_Explicit")
            {
                // If it's op_Implicit or op_Explicit, we might want to use return type instead of first parameter (depending on which way the conversion is)
                builder.Append("_");
                if (MemberEqualityComparer.Default.Equals(method.Parameters[0].ParameterType, method.DeclaringType))
                {
                    builder.Append("ret_");
                    builder.Append(method.ReturnType.MangledName());
                }
                else
                {
                    builder.Append(ResolveGenericsVisitor.Process(method, method.Parameters[0].ParameterType).MangledName());
                }
            }
            else
            {
                // Method signature
                MethodSignatureMangledName(method, builder);
            }

            return builder.ToString();
        }

        public static void MethodSignatureMangledName(this MethodReference method, StringBuilder builder)
        {
            builder.Append("(");
            if (method.HasParameters)
            {
                var parameters = method.Parameters;
                for (int i = 0; i < parameters.Count; i++)
                {
                    ParameterDefinition parameter = parameters[i];
                    if (i > 0)
                    {
                        builder.Append(",");
                    }
                    if (parameter.ParameterType.IsSentinel)
                    {
                        builder.Append("...,");
                    }

                    // We are replacing GenericInstance of type !0 with their real definitions (T, U, etc...)
                    var parameterType = ResolveGenericsVisitor.Process(method, parameter.ParameterType, true);
                    builder.Append(parameterType.MangledName());
                }
            }
            builder.Append(")");
        }

        public static int UpdateInstructionOffsets(this MethodBody body)
        {
            // Ideally we would like to reuse CodeWriter.ComputeHeader() (unfortunately private)
            var offset = 0;
            foreach (var instruction in body.Instructions)
            {
                instruction.Offset = offset;
                offset += instruction.GetSize();
            }

            // TODO: Guess better (we don't care so much as our codegen doesn't depend on it being valid)
            body.MaxStackSize = 8;

            body.CodeSize = offset;

            return offset;
        }

        public static TypeReference MakeGenericType(this TypeReference self, params TypeReference[] arguments)
        {
            if (self.GenericParameters.Count != arguments.Length)
                throw new ArgumentException();

            var instance = new GenericInstanceType(self);
            foreach (var argument in arguments)
                instance.GenericArguments.Add(argument);

            return instance;
        }

        public static FieldReference MakeGeneric(this FieldReference self, params TypeReference[] arguments)
        {
            return new FieldReference(self.Name, self.FieldType, self.DeclaringType.MakeGenericType(arguments));
        }

        public static MethodReference MakeGeneric(this MethodReference self, params TypeReference[] arguments)
        {
            var reference = new MethodReference(self.Name, self.ReturnType, self.DeclaringType.MakeGenericType(arguments))
            {
                HasThis = self.HasThis,
                ExplicitThis = self.ExplicitThis,
                CallingConvention = self.CallingConvention,
            };

            foreach (var parameter in self.Parameters)
                reference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));

            foreach (var generic_parameter in self.GenericParameters)
                reference.GenericParameters.Add(new GenericParameter(generic_parameter.Name, reference));

            return reference;
        }

        public static MethodReference MakeGenericMethod(this MethodReference self, params TypeReference[] arguments)
        {
            var method = new GenericInstanceMethod(self);
            foreach(var argument in arguments)
                method.GenericArguments.Add(argument);
            return method;
        }

        public static MethodReference ChangeDeclaringType(this MethodReference self, TypeReference declaringType)
        {
            var reference = new MethodReference(self.Name, self.ReturnType, declaringType)
            {
                HasThis = self.HasThis,
                ExplicitThis = self.ExplicitThis,
                CallingConvention = self.CallingConvention,
            };

            foreach (var parameter in self.Parameters)
                reference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));

            foreach (var generic_parameter in self.GenericParameters)
                reference.GenericParameters.Add(new GenericParameter(generic_parameter.Name, reference));

            return reference;
        }

        public static GenericInstanceType ChangeGenericInstanceType(this GenericInstanceType type, TypeReference elementType, IEnumerable<TypeReference> genericArguments)
        {
            if (elementType != type.ElementType || genericArguments != type.GenericArguments)
            {
                var result = new GenericInstanceType(elementType);
                foreach (var genericArgument in genericArguments)
                    result.GenericArguments.Add(genericArgument);
                if (type.HasGenericParameters)
                    SetGenericParameters(result, type.GenericParameters);
                return result;
            }
            return type;
        }

        public static ByReferenceType ChangeByReferenceType(this ByReferenceType type, TypeReference elementType)
        {
            if (elementType != type.ElementType)
            {
                var result = new ByReferenceType(elementType);
                if (type.HasGenericParameters)
                    SetGenericParameters(result, type.GenericParameters);
                return result;
            }
            return type;
        }

        public static PinnedType ChangePinnedType(this PinnedType type, TypeReference elementType)
        {
            if (elementType != type.ElementType)
            {
                var result = new PinnedType(elementType);
                if (type.HasGenericParameters)
                    SetGenericParameters(result, type.GenericParameters);
                return result;
            }
            return type;
        }

        public static RequiredModifierType ChangeRequiredModifierType(this RequiredModifierType type, TypeReference elementType)
        {
            if (elementType != type.ElementType)
            {
                var result = new RequiredModifierType(type.ModifierType, elementType);
                if (type.HasGenericParameters)
                    SetGenericParameters(result, type.GenericParameters);
                return result;
            }
            return type;
        }

        public static PointerType ChangePointerType(this PointerType type, TypeReference elementType)
        {
            if (elementType != type.ElementType)
            {
                var result = new PointerType(elementType);
                if (type.HasGenericParameters)
                    SetGenericParameters(result, type.GenericParameters);
                return result;
            }
            return type;
        }

        public static ArrayType ChangeArrayType(this ArrayType type, TypeReference elementType, int rank)
        {
            if (elementType != type.ElementType || rank != type.Rank)
            {
                var result = new ArrayType(elementType, rank);
                if (type.HasGenericParameters)
                    SetGenericParameters(result, type.GenericParameters);
                return result;
            }
            return type;
        }

        public static TypeReference ChangeGenericParameters(this TypeReference type, IEnumerable<GenericParameter> genericParameters)
        {
            if (type.GenericParameters == genericParameters)
                return type;

            TypeReference result;
            var arrayType = type as ArrayType;
            if (arrayType != null)
            {
                result = new ArrayType(arrayType.ElementType, arrayType.Rank);
            }
            else
            {
                var genericInstanceType = type as GenericInstanceType;
                if (genericInstanceType != null)
                {
                    result = new GenericInstanceType(genericInstanceType.ElementType);
                }
                else if (type.GetType() == typeof(TypeReference))
                {
                    result = new TypeReference(type.Namespace, type.Name, type.Module, type.Scope, type.IsValueType);
                }
                else
                {
                    throw new NotSupportedException();
                }
            }

            SetGenericParameters(result, genericParameters);

            return result;
        }

        private static void SetGenericParameters(TypeReference result, IEnumerable<GenericParameter> genericParameters)
        {
            foreach (var genericParameter in genericParameters)
                result.GenericParameters.Add(genericParameter);
        }
    }
}