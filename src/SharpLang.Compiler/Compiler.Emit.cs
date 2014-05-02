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

            // Convert from stack to local value
            var stackValue = ConvertFromStackToLocal(local.Type, value);

            // Store value into local
            LLVM.BuildStore(builder, stackValue, local.Value);
        }

        private void EmitLdloc(List<StackValue> stack, List<StackValue> locals, int operandIndex)
        {
            var local = locals[operandIndex];

            // Load value from local
            var value = LLVM.BuildLoad(builder, local.Value, string.Empty);

            // Convert from local to stack value
            value = ConvertFromLocalToStack(local.Type, value);

            // Add value to stack
            stack.Add(new StackValue(local.StackType, local.Type, value));
        }

        private void EmitLdloca(List<StackValue> stack, List<StackValue> locals, int operandIndex)
        {
            var local = locals[operandIndex];

            // Convert from local to stack value
            var value = ConvertFromLocalToStack(local.Type, local.Value);

            // Add value to stack
            // TODO: Choose appropriate type + conversions
            stack.Add(new StackValue(StackValueType.Reference, local.Type, value));
        }

        private void EmitLdarg(List<StackValue> stack, List<StackValue> args, int operandIndex)
        {
            var arg = args[operandIndex];
            var value = ConvertFromLocalToStack(arg.Type, arg.Value);
            stack.Add(new StackValue(arg.StackType, arg.Type, value));
        }

        private void EmitInitobj(StackValue address, Type type)
        {
            var value = address.Value;
            var expectedType = LLVM.PointerType(type.GeneratedType, 0);

            // If necessary, cast to expected type
            if (LLVM.TypeOf(value) != expectedType)
            {
                value = LLVM.BuildPointerCast(builder, value, expectedType, string.Empty);
            }

            // Store null value (should be all zero)
            LLVM.BuildStore(builder, LLVM.ConstNull(type.GeneratedType), value);
        }

        private void EmitNewobj(List<StackValue> stack, Type type, Function ctor)
        {
            if (type.StackType == StackValueType.Object)
            {
                // TODO: Improve performance (better inlining, etc...)
                // Invoke malloc
                var typeSize = LLVM.BuildIntCast(builder, LLVM.SizeOf(type.GeneratedType), LLVM.Int32TypeInContext(context), string.Empty);
                var allocatedData = LLVM.BuildCall(builder, allocObjectFunction, new[] { typeSize }, string.Empty);
                var allocatedObject = LLVM.BuildPointerCast(builder, allocatedData, type.GeneratedType, string.Empty);

                // Add it to stack, right before arguments
                var ctorNumParams = ctor.ParameterTypes.Length;
                stack.Insert(stack.Count - ctorNumParams + 1, new StackValue(StackValueType.Object, type, allocatedObject));

                // Invoke ctor
                EmitCall(stack, ctor.MethodReference, ctor);

                // Add created object on the stack
                stack.Add(new StackValue(StackValueType.Object, type, allocatedObject));
            }
            else
            {
                // TODO: Support value type too
                throw new NotImplementedException();
            }
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
            var stringType = GetType(corlib.MainModule.GetType(typeof(string).FullName));

            // Create string data global
            var stringConstantData = LLVM.ConstStringInContext(context, operand, (uint)operand.Length, true);
            var stringConstantDataGlobal = LLVM.AddGlobal(module, LLVM.TypeOf(stringConstantData), string.Empty);

            // Cast from i8-array to i8*
            LLVM.SetInitializer(stringConstantDataGlobal, stringConstantData);
            var zero = LLVM.ConstInt(LLVM.Int32TypeInContext(context), 0, false);
            stringConstantDataGlobal = LLVM.ConstInBoundsGEP(stringConstantDataGlobal, new[] { zero, zero });

            // Create string
            var stringConstant = LLVM.ConstNamedStruct(stringType.GeneratedType,
                new[] { LLVM.ConstInt(LLVM.Int32TypeInContext(context), (ulong)operand.Length, false), stringConstantDataGlobal });

            // Push on stack
            stack.Add(new StackValue(StackValueType.Value, stringType, stringConstant));
        }

        private void EmitI4(List<StackValue> stack, int operandIndex)
        {
            var intType = CreateType(corlib.MainModule.GetType(typeof(int).FullName));

            // Add constant integer value to stack
            stack.Add(new StackValue(StackValueType.Int32, intType,
                LLVM.ConstInt(LLVM.Int32TypeInContext(context), (uint)operandIndex, true)));
        }

        private void EmitCall(List<StackValue> stack, MethodReference targetMethodReference, Function targetMethod)
        {
            // Build argument list
            var targetNumParams = targetMethod.ParameterTypes.Length;
            var args = new ValueRef[targetNumParams];
            for (int index = 0; index < targetNumParams; index++)
            {
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

        private void EmitBr(BasicBlockRef targetBasicBlock)
        {
            // Unconditional branch
            LLVM.BuildBr(builder, targetBasicBlock);
        }

        /// <summary>
        /// Helper function for Brfalse/Brtrue: compare stack value with zero using zeroPredicate,
        /// and accordingly jump to either target or next block.
        /// </summary>
        private void EmitBrCommon(StackValue stack, IntPredicate zeroPredicate, BasicBlockRef targetBasicBlock, BasicBlockRef nextBasicBlock)
        {
            // Zero constant
            var zero = LLVM.ConstInt(LLVM.Int32TypeInContext(context), 0, false);

            switch (stack.StackType)
            {
                case StackValueType.Int32:
                    // Compare stack value with zero, and accordingly jump to either target or next block
                    var cmpInst = LLVM.BuildICmp(builder, zeroPredicate, stack.Value, zero, string.Empty);
                    LLVM.BuildCondBr(builder, cmpInst, targetBasicBlock, nextBasicBlock);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private void EmitBrfalse(List<StackValue> stack, BasicBlockRef targetBasicBlock, BasicBlockRef nextBasicBlock)
        {
            // Stack element should be equal to zero.
            EmitBrCommon(stack.Pop(), IntPredicate.IntEQ, targetBasicBlock, nextBasicBlock);
        }

        private void EmitBrtrue(List<StackValue> stack, BasicBlockRef targetBasicBlock, BasicBlockRef nextBasicBlock)
        {
            // Stack element should be different from zero.
            EmitBrCommon(stack.Pop(), IntPredicate.IntNE, targetBasicBlock, nextBasicBlock);
        }

        private void EmitStfld(List<StackValue> stack, Field field)
        {
            var value = stack.Pop();
            var @object = stack.Pop();

            // Build indices for GEP
            var indices = new List<ValueRef>(3);
            indices.Add(LLVM.ConstInt(LLVM.Int32TypeInContext(context), 0, false));

            if (@object.StackType == StackValueType.Object)
                indices.Add(LLVM.ConstInt(LLVM.Int32TypeInContext(context), 1, false));

            // TODO: Object parent classes
            indices.Add(LLVM.ConstInt(LLVM.Int32TypeInContext(context), (uint)field.StructIndex, false));

            // Find field address using GEP
            var fieldAddress = LLVM.BuildInBoundsGEP(builder, @object.Value, indices.ToArray(), string.Empty);

            // Convert stack value to appropriate type
            var fieldValue = ConvertFromStackToLocal(field.Type, value);

            // Store value in field
            LLVM.BuildStore(builder, fieldValue, fieldAddress);
        }

        private void EmitLdfld(List<StackValue> stack, Field field)
        {
            var @object = stack.Pop();

            // Build indices for GEP
            var indices = new List<ValueRef>(3);
            indices.Add(LLVM.ConstInt(LLVM.Int32TypeInContext(context), 0, false));
            if (@object.StackType == StackValueType.Object)
                indices.Add(LLVM.ConstInt(LLVM.Int32TypeInContext(context), 1, false));

            // TODO: Object parent classes
            indices.Add(LLVM.ConstInt(LLVM.Int32TypeInContext(context), (uint)field.StructIndex, false));


            // Find field address using GEP
            var fieldAddress = LLVM.BuildInBoundsGEP(builder, @object.Value, indices.ToArray(), string.Empty);

            // Load value from field and create "fake" local
            var value = LLVM.BuildLoad(builder, fieldAddress, string.Empty);

            // Convert from local to stack value
            value = ConvertFromLocalToStack(field.Type, value);

            // Add value to stack
            stack.Add(new StackValue(field.Type.StackType, field.Type, value));
        }
    }
}