// Copyright (c) 2014 SharpLang - Virgile Bello

using System.Collections.Generic;
using Mono.Cecil;
using SharpLang.CompilerServices.Cecil;

namespace SharpLang.CompilerServices
{
    public partial class Compiler
    {
        /// <summary> List of all referenced assemblies (including indirect ones). </summary>
        private HashSet<AssemblyDefinition> referencedAssemblies = new HashSet<AssemblyDefinition>();
        private HashSet<TypeReference> referencedTypes = new HashSet<TypeReference>(MemberEqualityComparer.Default);
        private HashSet<MethodReference> referencedMethods = new HashSet<MethodReference>(MemberEqualityComparer.Default);

        private List<TypeReference> markedTypes = new List<TypeReference>();
        private List<MethodReference> markedMethods = new List<MethodReference>();

        private void RegisterExternalTypes()
        {
            // Load all dependent assemblies (except ourself)
            CollectAssemblyReferences(referencedAssemblies, assembly);
            referencedAssemblies.Remove(assembly);

            // Iterate over each external assembly, and register all types
            foreach (var referencedAssembly in referencedAssemblies)
            {
                foreach (var type in referencedAssembly.MainModule.Types)
                {
                    MarkExternalType(type);
                }
                for (uint i = 1; ; ++i)
                {
                    var type = (TypeReference)referencedAssembly.MainModule.LookupToken(new MetadataToken(TokenType.TypeSpec, i));
                    if (type == null)
                        break;

                    MarkExternalType(type);
                }
                foreach (var type in referencedAssembly.MainModule.GetTypeReferences())
                {
                    MarkExternalType(type);
                }
            }

            // Process marked until nothing left
            while (markedTypes.Count > 0 || markedMethods.Count > 0)
            {
                // Process types
                while (markedTypes.Count != 0)
                {
                    var markedType = markedTypes[markedTypes.Count - 1];
                    markedTypes.RemoveAt(markedTypes.Count - 1);
                    RegisterExternalType(markedType);
                }

                // Process methods
                while (markedMethods.Count != 0)
                {
                    var markedMethod = markedMethods[markedMethods.Count - 1];
                    markedMethods.RemoveAt(markedMethods.Count - 1);
                    RegisterExternalMethod(markedMethod);
                }
            }
        }

        private void MarkExternalType(TypeReference typeReference)
        {
            // Ignore open types
            if (typeReference.ContainsGenericParameter)
                return;

            // Handle circular assembly references
            if (typeReference.Module == assembly.MainModule)
                return;

            var typeDefinition = typeReference.Resolve();
            if (typeDefinition.Module == assembly.MainModule)
                return;

            // Register type
            if (referencedTypes.Add(typeReference))
                markedTypes.Add(typeReference);
        }

        private void MarkExternalMethod(MethodReference methodReference)
        {
            // If a method contains generic parameters, skip it
            if (ResolveGenericsVisitor.ContainsGenericParameters(methodReference))
                return;

            // Handle circular assembly references
            if (methodReference.Module == assembly.MainModule)
                return;

            var methodDefinition = methodReference.Resolve();
            if (methodDefinition == null || methodDefinition.Module == assembly.MainModule)
                return;

            // Register method
            if (referencedMethods.Add(methodReference))
                markedMethods.Add(methodReference);
        }

        private void RegisterExternalType(TypeReference typeReference)
        {
            var typeDefinition = typeReference.Resolve();
            if (typeDefinition.Module == assembly.MainModule)
                return;

            // Process its base type
            if (typeDefinition.BaseType != null)
                MarkExternalType(ResolveGenericsVisitor.Process(typeReference, typeDefinition.BaseType));

            // Process nested types
            if (typeDefinition.HasNestedTypes)
            {
                foreach (var nestedType in typeDefinition.NestedTypes)
                    MarkExternalType(ResolveGenericsVisitor.Process(typeReference, nestedType));
            }

            // Process its fields
            if (typeDefinition.HasFields)
            {
                foreach (var field in typeDefinition.Fields)
                    MarkExternalType(ResolveGenericsVisitor.Process(typeReference, field.FieldType));
            }

            // Process its properties
            if (typeDefinition.HasProperties)
            {
                foreach (var property in typeDefinition.Properties)
                    MarkExternalType(ResolveGenericsVisitor.Process(typeReference, property.PropertyType));
            }

            // Process its methods
            if (typeDefinition.HasMethods)
            {
                foreach (var method in typeDefinition.Methods)
                {
                    var methodReference = ResolveGenericMethod(typeReference, method);

                    MarkExternalMethod(methodReference);
                }
            }
        }

        private void RegisterExternalMethod(MethodReference methodReference)
        {
            var methodDefinition = methodReference.Resolve();
            if (methodDefinition == null || methodDefinition.Module == assembly.MainModule)
                return;

            // Process method parameters
            foreach (var parameter in methodDefinition.Parameters)
                MarkExternalType(ResolveGenericsVisitor.Process(methodReference, parameter.ParameterType));

            if (methodDefinition.HasBody)
            {
                // Process method variables and locals
                foreach (var variable in methodDefinition.Body.Variables)
                    MarkExternalType(ResolveGenericsVisitor.Process(methodReference, variable.VariableType));

                // Process method opcodes
                foreach (var instruction in methodDefinition.Body.Instructions)
                {
                    var typeToken = instruction.Operand as TypeReference;
                    if (typeToken != null)
                        MarkExternalType(ResolveGenericsVisitor.Process(methodReference, typeToken));

                    var methodToken = instruction.Operand as MethodReference;
                    if (methodToken != null)
                        MarkExternalMethod(ResolveGenericsVisitor.Process(methodReference, methodToken));
                }
            }
        }

        private static void CollectAssemblyReferences(HashSet<AssemblyDefinition> assemblies, AssemblyDefinition assemblyDefinition)
        {
            if (!assemblies.Add(assemblyDefinition))
                return;

            foreach (var assemblyReference in assemblyDefinition.MainModule.AssemblyReferences)
            {
                // Resolve
                var resolvedAssembly = assemblyDefinition.MainModule.AssemblyResolver.Resolve(assemblyReference);

                if (resolvedAssembly != null)
                {
                    // If new, load its references
                    CollectAssemblyReferences(assemblies, resolvedAssembly);
                }
            }
        }
    }
}