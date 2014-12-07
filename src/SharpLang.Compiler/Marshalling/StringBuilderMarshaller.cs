using System;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SharpLang.CompilerServices.Marshalling
{
    /// <summary>
    /// Marshaller for <see cref="StringBuilder"/>.
    /// </summary>
    class StringBuilderMarshaller : Marshaller
    {
        public override void EmitConvertManagedToNative(MarshalCodeContext context)
        {
            // TODO: Currently unsupported
            // For now, output (void*)null
            context.ILProcessor.Emit(OpCodes.Ldc_I4_0);
            context.ILProcessor.Emit(OpCodes.Conv_I);
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