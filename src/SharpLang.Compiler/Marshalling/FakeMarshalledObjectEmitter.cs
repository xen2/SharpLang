using System;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SharpLang.CompilerServices.Marshalling
{
    /// <summary>
    /// Helper class to determine native type of marshalled objects.
    /// </summary>
    class FakeMarshalledObjectEmitter : MarshalledObjectEmitter
    {
        private readonly TypeReference type;

        public override TypeReference Type { get { return type; } }

        public FakeMarshalledObjectEmitter(TypeReference type)
        {
            this.type = type;
        }

        public override void Emit(ILProcessor ilProcessor)
        {
            throw new NotImplementedException();
        }

        public override void EmitAddress(ILProcessor ilProcessor)
        {
            throw new NotImplementedException();
        }

        public override void StoreEnd(ILProcessor ilProcessor)
        {
            throw new NotImplementedException();
        }
    }
}