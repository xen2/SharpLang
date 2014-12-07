using System;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SharpLang.CompilerServices.Marshalling
{
    /// <summary>
    /// Marshaller for <see cref="Array"/> of non-blittable types.
    /// </summary>
    class ArrayMarshaller : Marshaller
    {
        private readonly Marshaller elementMarshaller;
        private VariableDefinition result;

        public ArrayMarshaller(Marshaller elementMarshaller)
        {
            this.elementMarshaller = elementMarshaller;
        }

        public override bool ContainsLoops
        {
            get { return true; }
        }

        public override void EmitConvertManagedToNative(MarshalCodeContext context)
        {
            // uint loopCounter
            var uint32 = context.Assembly.MainModule.Import(typeof(uint));
            var loopCounter = new VariableDefinition(uint32);
            context.Method.Body.Variables.Add(loopCounter);

            // TODO: Handle null? Allocate array?
            var resultType = GetNativeType(context);
            result = new VariableDefinition(resultType);
            context.Method.Body.Variables.Add(result);

            // Block starts
            var loopCounterComparisonStart = context.ILProcessor.Create(OpCodes.Ldloc, loopCounter);
            var loopBodyStart = context.ILProcessor.Create(OpCodes.Nop);

            // loopCounter = 0;
            context.ILProcessor.Emit(OpCodes.Ldc_I4_0);
            context.ILProcessor.Emit(OpCodes.Stloc, loopCounter);
            context.ILProcessor.Emit(OpCodes.Br, loopCounterComparisonStart);

            // loop body
            context.ILProcessor.Append(loopBodyStart);

            // loopCounter++;
            context.ILProcessor.Emit(OpCodes.Ldloc, loopCounter);
            context.ILProcessor.Emit(OpCodes.Ldc_I4_1);
            context.ILProcessor.Emit(OpCodes.Add);
            context.ILProcessor.Emit(OpCodes.Stloc, loopCounter);

            // loopCounter < array.Length
            context.ILProcessor.Append(loopCounterComparisonStart);
            context.ILProcessor.Emit(OpCodes.Conv_U);
            context.ManagedEmitters.Peek().Emit(context.ILProcessor);
            context.ILProcessor.Emit(OpCodes.Ldlen);
            context.ILProcessor.Emit(OpCodes.Clt);
            context.ILProcessor.Emit(OpCodes.Brtrue, loopBodyStart);

            // Push result on the stack
            context.ILProcessor.Emit(OpCodes.Ldloc, result);
        }

        public override void EmitConvertNativeToManaged(MarshalCodeContext context)
        {
            throw new NotImplementedException();
        }

        public override TypeReference GetNativeType(MarshalCodeContext context)
        {
            // TODO: Push native emitter?
            return new PointerType(elementMarshaller.GetNativeType(context));
        }
    }
}