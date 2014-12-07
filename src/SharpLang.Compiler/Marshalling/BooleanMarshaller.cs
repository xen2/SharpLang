using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SharpLang.CompilerServices.Marshalling
{
    /// <summary>
    /// Marshaller for <see cref="bool"/>.
    /// </summary>
    class BooleanMarshaller : Marshaller
    {
        public override void EmitConvertManagedToNative(MarshalCodeContext context)
        {
            // Check if item is equal to 0
            context.ManagedEmitters.Peek().Emit(context.ILProcessor);
        }

        public override void EmitConvertNativeToManaged(MarshalCodeContext context)
        {
            context.NativeEmitters.Peek().Emit(context.ILProcessor);
            context.ILProcessor.Emit(OpCodes.Ldc_I4_0);
            context.ILProcessor.Emit(OpCodes.Ceq);

            // Compare comparison result with 0 (transform "equal to 0" into "inequal to 0")
            context.ILProcessor.Emit(OpCodes.Ldc_I4_0);
            context.ILProcessor.Emit(OpCodes.Ceq);
        }
        
        public override TypeReference GetNativeType(MarshalCodeContext context)
        {
            return context.Assembly.MainModule.Import(typeof(int));
        }
    }
}