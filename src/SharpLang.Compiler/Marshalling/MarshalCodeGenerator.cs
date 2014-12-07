// Copyright (c) 2014 SharpLang - Virgile Bello

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SharpLang.CompilerServices.Cecil;

// TODO: Register marshallers so that they can be accessed through Marshal.StructureToPtr(), Marshal.PtrToStructure() and Marshal.SizeOf()
namespace SharpLang.CompilerServices.Marshalling
{
    /// <summary>
    /// 
    /// </summary>
    public class MarshalCodeGenerator
    {
        private AssemblyDefinition assemblyDefinition;

        public AssemblyDefinition AssemblyDefinition
        {
            get { return assemblyDefinition; }
        }

        public MarshalCodeGenerator(string inputFile)
        {
            var assemblyResolver = new CustomAssemblyResolver();

            assemblyDefinition = AssemblyDefinition.ReadAssembly(inputFile,
                new ReaderParameters { AssemblyResolver = assemblyResolver, ReadSymbols = true });

            // TODO: Remove hardcoded relative paths
            assemblyResolver.Register(assemblyDefinition);
            assemblyResolver.AddSearchDirectory(Path.GetDirectoryName(assemblyDefinition.MainModule.FullyQualifiedName));
            assemblyResolver.AddSearchDirectory(@"..\..\..\..\src\mcs\class\lib\net_4_5");
        }

        public MarshalCodeGenerator(AssemblyDefinition assemblyDefinition)
        {
            this.assemblyDefinition = assemblyDefinition;
        }

        static MarshalledParameter CreateMarshalledParameter(ParameterDefinition parameter)
        {
            var result = new MarshalledParameter { Parameter = parameter };
            result.Marshaller = Marshaller.FindMarshallerForType(result.ParameterType, parameter.HasMarshalInfo ? parameter.MarshalInfo : null);
            return result;
        }

        public void Process(MethodDefinition methodDefinition)
        {
            var parameters = methodDefinition.Parameters.Select(CreateMarshalledParameter).ToArray();

            // If everything ended up being a BlittableMarshaller, that means we don't need to do any Marshalling
            if (parameters.All(x => x.Marshaller is BlittableMarshaller))
                return;

            var pinvokeMethod = new MethodDefinition(methodDefinition.Name, methodDefinition.Attributes, methodDefinition.ReturnType);

            // Move PInvokeInfo to underlying native method
            pinvokeMethod.PInvokeInfo = methodDefinition.PInvokeInfo;
            pinvokeMethod.ImplAttributes = methodDefinition.ImplAttributes;
            methodDefinition.PInvokeInfo = null;
            methodDefinition.IsPInvokeImpl = false;
            methodDefinition.ImplAttributes = MethodImplAttributes.IL;

            var context = new MarshalCodeContext(assemblyDefinition, methodDefinition, true);

            // Build method signature
            foreach (var parameter in parameters)
            {
                // Push context
                context.ManagedEmitters.Push(new ParameterMarshalledObjectEmitter(parameter.Parameter));
                if (parameter.IsByReference)
                    context.ManagedEmitters.Push(new ByReferenceMarshalledObjectEmitter());

                // Compute native type
                var nativeType = parameter.Marshaller.GetNativeType(context);
                if (parameter.IsByReference)
                    nativeType = new ByReferenceType(nativeType);

                // Add native parameter to pinvoke method
                parameter.NativeParameter = new ParameterDefinition(parameter.Parameter.Name, parameter.Parameter.Attributes, context.Assembly.MainModule.Import(nativeType));
                pinvokeMethod.Parameters.Add(parameter.NativeParameter);

                // Pop context
                if (parameter.IsByReference)
                    context.ManagedEmitters.Pop();
                context.ManagedEmitters.Pop();
            }

            // First, process marshallers which expect an empty stack (due to loop) and make sure they are stored in a local variable
            foreach (var parameter in parameters.Where(x => x.Marshaller.ContainsLoops || (!(x.Marshaller is BlittableMarshaller) && x.IsByReference)))
            {
                // Store in a local (if we didn't do that, we could end up having loops with things on the stack)
                var variableType = parameter.NativeParameterType;
                parameter.Variable = new VariableDefinition(variableType);
                methodDefinition.Body.Variables.Add(parameter.Variable);

                // Out-only parameter? Nothing to do...
                if (parameter.Parameter.IsOut && !parameter.Parameter.IsIn)
                    continue;

                // Push context
                context.ManagedEmitters.Push(new ParameterMarshalledObjectEmitter(parameter.Parameter));
                if (parameter.IsByReference)
                    context.ManagedEmitters.Push(new ByReferenceMarshalledObjectEmitter());
                context.NativeEmitters.Push(new VariableMarshalledObjectEmitter(parameter.Variable));

                // Convert parameter
                parameter.Marshaller.EmitStoreManagedToNative(context);

                // Pop context
                context.NativeEmitters.Pop();
                if (parameter.IsByReference)
                    context.ManagedEmitters.Pop();
                context.ManagedEmitters.Pop();
            }

            // Each marshaller is responsible for pushing one parameter to the stack
            foreach (var parameter in parameters)
            {
                if (parameter.Variable != null)
                {
                    // Already processed before and stored in a variable
                    context.ILProcessor.Emit(parameter.IsByReference ? OpCodes.Ldloca : OpCodes.Ldloc, parameter.Variable);
                }
                else
                {
                    // Just process and keep it on the stack
                    context.ManagedEmitters.Push(new ParameterMarshalledObjectEmitter(parameter.Parameter));
                    parameter.Marshaller.EmitStoreManagedToNative(context);
                    context.ManagedEmitters.Pop();
                }
            }

            // Emit call
            context.ILProcessor.Emit(OpCodes.Call, pinvokeMethod);

            VariableDefinition returnValue = null;
            Marshaller returnMarshaller = null;
            if (methodDefinition.ReturnType.MetadataType != MetadataType.Void)
            {
                returnMarshaller = Marshaller.FindMarshallerForType(methodDefinition.ReturnType, null);

                // Find native return type
                context.ManagedEmitters.Push(new FakeMarshalledObjectEmitter(methodDefinition.ReturnType));
                var nativeReturnType = returnMarshaller.GetNativeType(context);
                context.ManagedEmitters.Pop();

                // Change return type to native one
                pinvokeMethod.ReturnType = nativeReturnType;

                // Store return value in local variable
                returnValue = new VariableDefinition("returnValue", nativeReturnType);
                methodDefinition.Body.Variables.Add(returnValue);

                context.ILProcessor.Emit(OpCodes.Stloc, returnValue);
            }

            // Emit setup
            // TODO: Force Out parameter to have local variables? (source)
            foreach (var parameter in parameters)
            {
                if (parameter.Parameter.IsOut && parameter.Variable != null)
                {
                    context.NativeEmitters.Push(new VariableMarshalledObjectEmitter(parameter.Variable));
                    context.ManagedEmitters.Push(new ParameterMarshalledObjectEmitter(parameter.Parameter));
                    parameter.Marshaller.EmitStoreNativeToManaged(context);
                    context.ManagedEmitters.Pop();
                    context.NativeEmitters.Pop();
                }
            }

            // TODO: Cleanup

            // Emit return value
            if (returnValue != null)
            {
                // Convert return value to managed type
                context.NativeEmitters.Push(new VariableMarshalledObjectEmitter(returnValue));
                returnMarshaller.EmitStoreNativeToManaged(context);
                context.NativeEmitters.Pop();
            }
            context.ILProcessor.Emit(OpCodes.Ret);

            // Add method to type
            methodDefinition.DeclaringType.Methods.Add(pinvokeMethod);

            methodDefinition.Body.UpdateInstructionOffsets();
        }

        public void Generate()
        {
            foreach (var type in assemblyDefinition.MainModule.Types.ToArray())
            {
                GenerateType(type);
            }
        }

        private void GenerateType(TypeDefinition type)
        {
            // Generate nested types recursively
            foreach (var nestedType in type.NestedTypes)
            {
                GenerateType(nestedType);
            }

            // Delegate tagged with UnmanagedFunctionPointerAttribute should have delegate wrapper generated for them (even if not used in any PInvoke method directly)
            if (type.HasCustomAttributes && type.CustomAttributes.Any(x => x.AttributeType.FullName == typeof(UnmanagedFunctionPointerAttribute).FullName))
                DelegateMarshaller.GetOrCreateGenerateDelegateWrapper(assemblyDefinition, type);

            foreach (var method in type.Methods.ToArray())
            {
                if (method.HasPInvokeInfo)
                {
                    Process(method);
                }
            }
        }

        /// <summary>
        /// Represents a marshalled parameter.
        /// </summary>
        class MarshalledParameter
        {
            public ParameterDefinition Parameter { get; set; }

            public TypeReference ParameterType
            {
                get
                {
                    var result = Parameter.ParameterType;
                    if (result.IsByReference)
                        result = ((ByReferenceType)result).ElementType;
                    return result;
                }
            }

            public ParameterDefinition NativeParameter { get; set; }

            public TypeReference NativeParameterType
            {
                get
                {
                    var result = NativeParameter.ParameterType;
                    if (result.IsByReference)
                        result = ((ByReferenceType)result).ElementType;
                    return result;
                }
            }

            public VariableDefinition Variable { get; set; }

            public Marshaller Marshaller { get; set; }

            public bool IsByReference { get { return Parameter.ParameterType.IsByReference; } }
        }
    }
}