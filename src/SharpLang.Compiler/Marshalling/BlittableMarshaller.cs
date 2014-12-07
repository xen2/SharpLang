namespace SharpLang.CompilerServices.Marshalling
{
    /// <summary>
    /// Marshaller for blittable types (don't require any conversion).
    /// </summary>
    class BlittableMarshaller : Marshaller
    {
        public override void EmitConvertManagedToNative(MarshalCodeContext context)
        {
            // Load argument as is
            context.ManagedEmitters.Peek().Emit(context.ILProcessor);
        }

        public override void EmitConvertNativeToManaged(MarshalCodeContext context)
        {
            // Load argument as is
            context.NativeEmitters.Peek().Emit(context.ILProcessor);
        }
    }
}