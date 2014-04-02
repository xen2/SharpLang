using System;
using System.Collections.Generic;
using Mono.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public partial class Compiler
    {
        private void EmitStloc(List<StackValue> stack, List<ValueRef> locals, int localIndex)
        {
            var value = stack.Pop();
            LLVM.BuildStore(builder, value.Value, locals[localIndex]);
        }

        private void EmitLdloc(List<StackValue> stack, List<ValueRef> locals, int operandIndex)
        {
            var loadInst = LLVM.BuildLoad(builder, locals[operandIndex], string.Empty);

            // TODO: Choose appropriate type + conversions
            stack.Add(new StackValue(StackValueType.Value, loadInst));
        }

        private void EmitRet(MethodDefinition method)
        {
            if (method.ReturnType.MetadataType == MetadataType.Void)
                LLVM.BuildRetVoid(builder);
            else
                throw new NotImplementedException("Opcode not implemented.");
        }

        private void EmitLdstr(List<StackValue> stack, string operand)
        {
            var stringType = CompileClass(corlib.MainModule.GetType(typeof(string).FullName));

            // Create string data global
            var stringConstantData = LLVM.ConstStringInContext(context, operand, (uint)operand.Length, true);
            var stringConstantDataGlobal = LLVM.AddGlobal(module, LLVM.TypeOf(stringConstantData), string.Empty);

            // Cast from i8-array to i8*
            LLVM.SetInitializer(stringConstantDataGlobal, stringConstantData);
            var zero = LLVM.ConstInt(LLVM.Int32TypeInContext(context), 0, false);
            stringConstantDataGlobal = LLVM.ConstInBoundsGEP(stringConstantDataGlobal, new[] { zero, zero });

            // Create string
            var stringConstant = LLVM.ConstNamedStruct(stringType.DataType,
                new[] { LLVM.ConstInt(LLVM.Int32TypeInContext(context), (ulong)operand.Length, false), stringConstantDataGlobal });

            // Push on stack
            stack.Add(new StackValue(StackValueType.Value, stringConstant));
        }

        private void EmitI4(List<StackValue> stack, int operandIndex)
        {
            stack.Add(new StackValue(StackValueType.Int32,
                LLVM.ConstInt(LLVM.Int32TypeInContext(context), (uint)operandIndex, true)));
        }

        private void EmitCall(List<StackValue> stack, MethodReference targetMethodReference, Function targetMethod)
        {
            // Build argument list
            var targetNumParams = targetMethodReference.Parameters.Count;
            var args = new ValueRef[targetNumParams];
            for (int index = 0; index < targetNumParams; index++)
            {
                var parameter = targetMethodReference.Parameters[index];

                // TODO: Casting/implicit conversion?
                var stackItem = stack[stack.Count - targetNumParams + index];

                args[index] = stackItem.Value;
            }

            // Remove arguments from stack
            stack.RemoveRange(stack.Count - targetNumParams, targetNumParams);

            // Invoke method
            LLVM.BuildCall(builder, targetMethod.GeneratedValue, args, string.Empty);

            // Mark method as needed
            LLVM.SetLinkage(targetMethod.GeneratedValue, Linkage.ExternalLinkage);

            // Push return result on stack
            if (targetMethodReference.ReturnType.MetadataType != MetadataType.Void)
            {
                throw new NotImplementedException();
            }
        }
    }
}