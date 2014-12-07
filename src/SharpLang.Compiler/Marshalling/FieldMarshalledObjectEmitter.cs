using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SharpLang.CompilerServices.Marshalling
{
    /// <summary>
    /// Emits IL code for fields.
    /// </summary>
    class FieldMarshalledObjectEmitter : MarshalledObjectEmitter
    {
        public FieldReference Field { get; private set; }

        public override TypeReference Type
        {
            get { return Field.FieldType; }
        }

        public FieldMarshalledObjectEmitter(FieldReference field)
        {
            Field = field;
        }

        public override void Emit(ILProcessor ilProcessor)
        {
            if (Previous.Type.Resolve().IsValueType)
                Previous.EmitAddress(ilProcessor);
            else
                Previous.Emit(ilProcessor);
            ilProcessor.Emit(OpCodes.Ldfld, Field);
        }

        public override void EmitAddress(ILProcessor ilProcessor)
        {
            if (Previous.Type.Resolve().IsValueType)
                Previous.EmitAddress(ilProcessor);
            else
                Previous.Emit(ilProcessor);
            ilProcessor.Emit(OpCodes.Ldflda, Field);
        }

        public override void StoreStart(ILProcessor ilProcessor)
        {
            if (Previous.Type.Resolve().IsValueType)
                Previous.EmitAddress(ilProcessor);
            else
                Previous.Emit(ilProcessor);
        }

        public override void StoreEnd(ILProcessor ilProcessor)
        {
            ilProcessor.Emit(OpCodes.Stfld, Field);
        }
    }
}