using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SharpLang.CompilerServices.Marshalling
{
    /// <summary>
    /// Emits IL code for local variables.
    /// </summary>
    class VariableMarshalledObjectEmitter : MarshalledObjectEmitter
    {
        public VariableDefinition Variable { get; private set; }

        public override TypeReference Type
        {
            get { return Variable.VariableType; }
        }

        public VariableMarshalledObjectEmitter(VariableDefinition variable)
        {
            Variable = variable;
        }

        public override void Emit(ILProcessor ilProcessor)
        {
            ilProcessor.Emit(OpCodes.Ldloc, Variable);
        }

        public override void EmitAddress(ILProcessor ilProcessor)
        {
            ilProcessor.Emit(OpCodes.Ldloca, Variable);
        }

        public override void StoreEnd(ILProcessor ilProcessor)
        {
            ilProcessor.Emit(OpCodes.Stloc, Variable);
        }
    }
}