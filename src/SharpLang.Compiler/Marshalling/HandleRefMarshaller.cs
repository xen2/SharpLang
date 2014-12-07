using System;
using System.Linq;
using System.Runtime.InteropServices;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SharpLang.CompilerServices.Marshalling
{
    /// <summary>
    /// Marshaller for <see cref="HandleRef"/>.
    /// </summary>
    class HandleRefMarshaller : Marshaller
    {
        public override void EmitConvertManagedToNative(MarshalCodeContext context)
        {
            // Load argument as is
            context.ManagedEmitters.Peek().EmitAddress(context.ILProcessor);

            var corlib = context.Assembly.MainModule.Import(typeof(void)).Resolve().Module.Assembly;
            var handleRefClass = corlib.MainModule.GetType(typeof(HandleRef).FullName);
            var handleRefGetHandle = context.Assembly.MainModule.Import(handleRefClass.Properties.First(x => x.Name == "Handle").GetMethod);
            
            // Extract its Handle property
            context.ILProcessor.Emit(OpCodes.Call, handleRefGetHandle);

            // TODO: After the call (cleanup), do a GC.KeepAlive(handleRef.Wrapper);
        }

        public override void EmitConvertNativeToManaged(MarshalCodeContext context)
        {
            throw new NotImplementedException();
        }

        public override TypeReference GetNativeType(MarshalCodeContext context)
        {
            return context.Assembly.MainModule.Import(typeof(IntPtr));
        }
    }
}