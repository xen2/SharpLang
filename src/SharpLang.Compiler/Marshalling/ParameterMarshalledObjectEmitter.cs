using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SharpLang.CompilerServices.Marshalling
{
    /// <summary>
    /// Emits IL code for parameters.
    /// </summary>
    class ParameterMarshalledObjectEmitter : MarshalledObjectEmitter
    {
        public ParameterDefinition Parameter { get; private set; }

        public override TypeReference Type
        {
            get { return Parameter.ParameterType; }
        }

        public ParameterMarshalledObjectEmitter(ParameterDefinition parameter)
        {
            Parameter = parameter;
        }

        public override void Emit(ILProcessor ilProcessor)
        {
            ilProcessor.Emit(OpCodes.Ldarg, Parameter);
        }

        public override void EmitAddress(ILProcessor ilProcessor)
        {
            ilProcessor.Emit(OpCodes.Ldarga, Parameter);
        }

        public override void StoreStart(ILProcessor ilProcessor)
        {
            ilProcessor.Emit(OpCodes.Ldarg, Parameter);
        }

        public override void StoreEnd(ILProcessor ilProcessor)
        {
            ilProcessor.Emit(OpCodes.Stind_Ref);
        }
    }
}