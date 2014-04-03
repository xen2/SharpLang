using System;
using System.Collections.Generic;
using Mono.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public partial class Compiler
    {
        private void EmitStloc(List<StackValue> stack, List<StackValue> locals, int localIndex)
        {
            var value = stack.Pop();

            var local = locals[localIndex];

            var stackValue = ConvertFromStackToLocal(local.Type, value);

            LLVM.BuildStore(builder, stackValue, local.Value);
        }

        private void EmitLdloc(List<StackValue> stack, List<StackValue> locals, int operandIndex)
        {
            var local = locals[operandIndex];
            var value = LLVM.BuildLoad(builder, local.Value, string.Empty);

            value = ConvertFromLocalToStack(local, value);

            // TODO: Choose appropriate type + conversions
            stack.Add(new StackValue(local.StackType, local.Type, value));
        }

        private void EmitLdarg(List<StackValue> stack, List<StackValue> args, int operandIndex)
        {
            var arg = args[operandIndex];
            var value = ConvertFromLocalToStack(arg, arg.Value);
            stack.Add(new StackValue(arg.StackType, arg.Type, value));
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
            var stringType = CompileType(corlib.MainModule.GetType(typeof(string).FullName));
            var stringClass = CompileClass(corlib.MainModule.GetType(typeof(string).FullName));

            // Create string data global
            var stringConstantData = LLVM.ConstStringInContext(context, operand, (uint)operand.Length, true);
            var stringConstantDataGlobal = LLVM.AddGlobal(module, LLVM.TypeOf(stringConstantData), string.Empty);

            // Cast from i8-array to i8*
            LLVM.SetInitializer(stringConstantDataGlobal, stringConstantData);
            var zero = LLVM.ConstInt(LLVM.Int32TypeInContext(context), 0, false);
            stringConstantDataGlobal = LLVM.ConstInBoundsGEP(stringConstantDataGlobal, new[] { zero, zero });

            // Create string
            var stringConstant = LLVM.ConstNamedStruct(stringClass.DataType,
                new[] { LLVM.ConstInt(LLVM.Int32TypeInContext(context), (ulong)operand.Length, false), stringConstantDataGlobal });

            // Push on stack
            stack.Add(new StackValue(StackValueType.Value, stringType, stringConstant));
        }

        private void EmitI4(List<StackValue> stack, int operandIndex)
        {
            var intType = CompileType(corlib.MainModule.GetType(typeof(int).FullName));

            stack.Add(new StackValue(StackValueType.Int32, intType,
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
                args[index] = ConvertFromStackToLocal(targetMethod.ParameterTypes[index], stackItem);
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

        private void EmitBrfalse(List<StackValue> stack, BasicBlockRef targetBasicBlock, BasicBlockRef nextBasicBlock)
        {
            var value = stack.Pop();
            var zero = LLVM.ConstInt(LLVM.Int32TypeInContext(context), 0, false);
            switch (value.StackType)
            {
                case StackValueType.Int32:
                    var cmpInst = LLVM.BuildICmp(builder, IntPredicate.IntEQ, value.Value, zero, string.Empty);
                    LLVM.BuildCondBr(builder, cmpInst, targetBasicBlock, nextBasicBlock);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}