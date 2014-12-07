using System;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SharpLang.CompilerServices.Marshalling
{
    /// <summary>
    /// Marshaller for <see cref="Array"/> of blittable types.
    /// </summary>
    class BlittableArrayMarshaller : Marshaller
    {
        public override void EmitConvertManagedToNative(MarshalCodeContext context)
        {
            var elementType = ((ArrayType)context.ManagedEmitters.Peek().Type).ElementType;

            var pinnedArray = new VariableDefinition(new PinnedType(new ByReferenceType(elementType)));
            context.ILProcessor.Body.Variables.Add(pinnedArray);

            context.ManagedEmitters.Peek().Emit(context.ILProcessor);
            context.ILProcessor.Emit(OpCodes.Ldc_I4_0);
            context.ILProcessor.Emit(OpCodes.Ldelema, elementType);
            context.ILProcessor.Emit(OpCodes.Stloc, pinnedArray);
            context.ILProcessor.Emit(OpCodes.Ldloc, pinnedArray);
        }

        public override void EmitConvertNativeToManaged(MarshalCodeContext context)
        {
            // TODO: Implement this
            context.ILProcessor.Emit(OpCodes.Ldnull);
        }

        public override TypeReference GetNativeType(MarshalCodeContext context)
        {
            var elementType = ((ArrayType)context.ManagedEmitters.Peek().Type).ElementType;

            return new PointerType(elementType);
        }
    }
}