using System;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SharpLang.CompilerServices.Marshalling
{
    /// <summary>
    /// Removes the extra ref/out indirection.
    /// </summary>
    class ByReferenceMarshalledObjectEmitter : MarshalledObjectEmitter
    {
        public override TypeReference Type
        {
            get { return ((ByReferenceType)Previous.Type).ElementType; }
        }

        public override void Emit(ILProcessor ilProcessor)
        {
            Previous.Emit(ilProcessor);

            var byRefType = (ByReferenceType)Previous.Type;
            if (byRefType.ElementType.Resolve().IsValueType)
                ilProcessor.Emit(OpCodes.Ldobj, byRefType.ElementType);
            else
                ilProcessor.Emit(OpCodes.Ldind_Ref);
        }

        public override void EmitAddress(ILProcessor ilProcessor)
        {
            Previous.Emit(ilProcessor);
        }

        public override void StoreEnd(ILProcessor ilProcessor)
        {
            throw new NotImplementedException();
        }
    }
}