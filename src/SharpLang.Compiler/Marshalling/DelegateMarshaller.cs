using System;
using System.Linq;
using System.Runtime.InteropServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SharpLang.CompilerServices.Cecil;

namespace SharpLang.CompilerServices.Marshalling
{
    /// <summary>
    /// Marshaller for <see cref="Delegate"/>.
    /// </summary>
    class DelegateMarshaller : Marshaller
    {
        private MethodDefinition delegateWrapper;

        public override void EmitConvertManagedToNative(MarshalCodeContext context)
        {
            if (delegateWrapper == null)
                delegateWrapper = GetOrCreateGenerateDelegateWrapper(context.Assembly, context.ManagedEmitters.Peek().Type);

            var corlib = context.Assembly.MainModule.Import(typeof(void)).Resolve().Module.Assembly;
            var marshalHelper = corlib.MainModule.GetType("SharpLang.Marshalling.MarshalHelper");
            var createThunk = context.Assembly.MainModule.Import(marshalHelper.Methods.First(x => x.Name == "CreateThunk"));

            // For now, output (void*)null
            context.ManagedEmitters.Peek().Emit(context.ILProcessor);
            context.ILProcessor.Emit(OpCodes.Ldftn, delegateWrapper);
            context.ILProcessor.Emit(OpCodes.Call, createThunk);
        }

        public override void EmitConvertNativeToManaged(MarshalCodeContext context)
        {
            // TODO: Implement!
            context.ILProcessor.Emit(OpCodes.Ldnull);
        }

        public override TypeReference GetNativeType(MarshalCodeContext context)
        {
            return context.Assembly.MainModule.Import(typeof(IntPtr));
        }

        private static MethodDefinition GetDelegateWrapper(ModuleDefinition module, TypeReference multicastDelegateType)
        {
            // Find DelegateWrappers type in assembly
            var delegateWrappersType = module.GetType("DelegateWrappers");
            if (delegateWrappersType == null)
                return null;

            // Find if any method match the requested type
            var multicastDelegateName = multicastDelegateType.MangledName();
            return delegateWrappersType.Methods.FirstOrDefault(x => x.Name == multicastDelegateName);
        }

        // TODO: This method is quite incomplete. It would be good to synchronize it with MarshalCodeGenerator.Process() (with Native/Managed swapped)
        public static MethodDefinition GetOrCreateGenerateDelegateWrapper(AssemblyDefinition currentAssembly, TypeReference multicastDelegateType)
        {
            var multicastDelegateTypeResolved = multicastDelegateType.Resolve();

            // Try to find method in assembly where delegate is defined
            var delegateWrapper = GetDelegateWrapper(multicastDelegateTypeResolved.Module, multicastDelegateType);
            if (delegateWrapper != null)
                return delegateWrapper;

            // Try to find method in current assembly
            delegateWrapper = GetDelegateWrapper(currentAssembly.MainModule, multicastDelegateType);
            if (delegateWrapper != null)
                return delegateWrapper;

            // Not found, let's create it in current assembly
            // First, get or create DelegateWrappers static class
            var delegateWrappersType = currentAssembly.MainModule.GetType("DelegateWrappers");
            if (delegateWrappersType == null)
            {
                delegateWrappersType = new TypeDefinition(string.Empty, "DelegateWrappers", TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Abstract);
                currentAssembly.MainModule.Types.Add(delegateWrappersType);
            }

            var invokeMethod = multicastDelegateTypeResolved.Methods.First(x => x.Name == "Invoke");
            
            //var returnMarshaller = FindMarshallerForType(context.SourceEmitters.Peek().Type, field.MarshalInfo);
            delegateWrapper = new MethodDefinition(multicastDelegateType.MangledName(), MethodAttributes.Static, currentAssembly.MainModule.Import(ResolveGenericsVisitor.Process(multicastDelegateType, invokeMethod.ReturnType)));

            // Determine calling convention
            var callingConvention = CallingConvention.Winapi; // Set default
            // If there is a UnmanagedFunctionPointerAttribute, get its value
            var unmanagedFunctionPointerAttribute = multicastDelegateTypeResolved.HasCustomAttributes ? multicastDelegateTypeResolved.CustomAttributes.FirstOrDefault(x => x.AttributeType.FullName == typeof(UnmanagedFunctionPointerAttribute).FullName) : null;
            if (unmanagedFunctionPointerAttribute != null)
                callingConvention = (CallingConvention)unmanagedFunctionPointerAttribute.ConstructorArguments[0].Value;
            // On Windows, Winapi (default) means StdCall; TODO: Other platforms
            if (callingConvention == CallingConvention.Winapi)
                callingConvention = CallingConvention.StdCall;
            delegateWrapper.CallingConvention = callingConvention.ToCecil();

            foreach (var parameter in invokeMethod.Parameters)
            {
                delegateWrapper.Parameters.Add(new ParameterDefinition(parameter.Name, parameter.Attributes, currentAssembly.MainModule.Import(parameter.ParameterType)));
            }

            // Extract delegate from thunk table
            var corlib = currentAssembly.MainModule.Import(typeof(void)).Resolve().Module.Assembly;
            var marshalHelper = corlib.MainModule.GetType("SharpLang.Marshalling.MarshalHelper");
            var getDelegate = currentAssembly.MainModule.Import(marshalHelper.Methods.First(x => x.Name == "GetDelegate"));

            // Get delegate
            var ilProcessor = delegateWrapper.Body.GetILProcessor();
            ilProcessor.Emit(OpCodes.Call, getDelegate);

            // TODO: Convert parameters
            foreach (var parameter in invokeMethod.Parameters)
            {
                ilProcessor.Emit(OpCodes.Ldarg, parameter);
            }

            // Delegate.Invoke
            ilProcessor.Emit(OpCodes.Call, currentAssembly.MainModule.Import(invokeMethod));

            ilProcessor.Emit(OpCodes.Ret);

            delegateWrapper.Body.UpdateInstructionOffsets();

            // TODO: Convert out parameters

            // Add MarshalFunctionAttribute
            var typeType = currentAssembly.MainModule.Import(typeof(System.Type));
            var marshalFunctionAttribute = corlib.MainModule.GetType("SharpLang.Marshalling.MarshalFunctionAttribute");
            var marshalFunctionAttributeCtor = currentAssembly.MainModule.Import(marshalFunctionAttribute.Methods.Single(x => x.IsConstructor));
            delegateWrapper.CustomAttributes.Add(new CustomAttribute(marshalFunctionAttributeCtor) { ConstructorArguments = { new CustomAttributeArgument(typeType, multicastDelegateType) } });

            delegateWrappersType.Methods.Add(delegateWrapper);

            return delegateWrapper;
        }
    }
}