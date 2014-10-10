using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public partial class Compiler
    {
        private MethodBody GenerateDelegateMethod(MethodDefinition method, Class declaringClass)
        {
            // Delegate type
            var delegateType = corlib.MainModule.GetType(typeof(Delegate).FullName);

            // Delegate fields
            var targetField = delegateType.Fields.First(x => x.Name == "_target");
            var methodPtrField = delegateType.Fields.First(x => x.Name == "_methodPtr");
            var methodPtrAuxField = delegateType.Fields.First(x => x.Name == "_methodPtrAux");

            var body = new MethodBody(method);
            var il = body.GetILProcessor();

            if (method.Name == ".ctor")
            {
                // Mark
                //GenerateMulticastInvokeThunk(declaringClass);

                // Two main cases:
                // - Instance method:
                //    this._methodPtr = fnptr;
                //    this._target = target;
                //    Result: this._methodPtr(this._target, arg1, ..) will directly work
                // - Static method:
                //    this._target = this;
                //    this._methodPtrAux = fnptr;
                //    this._methodPtr = (delegate, arg1, ...) => { delegate->_methodPtrAux(arg1, ...); }
                //    Result: this._methodPtr(this._target, arg1, ...) will call thunk,
                //            which will call fnptr (from this._target._methodPtr) without the first argument
                var target = Instruction.Create(OpCodes.Ldarg_0);

                // if (target == null)
                // {
                il.Append(Instruction.Create(OpCodes.Ldarg, method.Parameters[0]));
                il.Append(Instruction.Create(OpCodes.Brtrue, target));

                //     Generate thunk (for now, done using direct LLVM, not sure weither LLVM or IL is better)
                //      this._methodPtr = (delegate, arg1, ...) => { delegate->_methodPtrAux(arg1, ...); }
                var invokeMethodHelper = GenerateStaticInvokeThunk(declaringClass);

                //      Fake Nop to push this thunk on stack (TODO: Better way to do this? i.e. store it in some static field?)
                il.Append(Instruction.Create(OpCodes.Ldarg_0));
                var loadFunctionPointerInstruction = Instruction.Create(OpCodes.Nop);
                InstructionActions.Add(loadFunctionPointerInstruction, (stack) =>
                {
                    // Push the generated method pointer on the stack
                    stack.Add(new StackValue(StackValueType.NativeInt, intPtr,
                        LLVM.BuildPointerCast(builder, invokeMethodHelper, intPtrType, string.Empty)));
                });
                il.Append(loadFunctionPointerInstruction);
                il.Append(Instruction.Create(OpCodes.Stfld, methodPtrField));

                //     this._methodPtrAux = method;
                il.Append(Instruction.Create(OpCodes.Ldarg_0));
                il.Append(Instruction.Create(OpCodes.Ldarg, method.Parameters[1]));
                il.Append(Instruction.Create(OpCodes.Stfld, methodPtrAuxField));

                //     this._target = this;
                il.Append(Instruction.Create(OpCodes.Ldarg_0));
                il.Append(Instruction.Create(OpCodes.Ldarg_0));
                il.Append(Instruction.Create(OpCodes.Stfld, targetField));

                //     return;
                // }
                il.Append(Instruction.Create(OpCodes.Ret));


                // this._target = target;
                il.Append(target);
                il.Append(Instruction.Create(OpCodes.Ldarg, method.Parameters[0]));
                il.Append(Instruction.Create(OpCodes.Stfld, targetField));

                // this._methodPtr = method;
                il.Append(Instruction.Create(OpCodes.Ldarg_0));
                il.Append(Instruction.Create(OpCodes.Ldarg, method.Parameters[1]));
                il.Append(Instruction.Create(OpCodes.Stfld, methodPtrField));

                // return;
                il.Append(Instruction.Create(OpCodes.Ret));
            }
            else if (method.Name == "GetMulticastDispatchMethod")
            {
                var invokeMethodHelper = GenerateMulticastInvokeThunk(declaringClass);

                var loadFunctionPointerInstruction = Instruction.Create(OpCodes.Nop);
                InstructionActions.Add(loadFunctionPointerInstruction, (stack) =>
                {
                    // Push the generated method pointer on the stack
                    stack.Add(new StackValue(StackValueType.NativeInt, intPtr,
                        LLVM.BuildPointerCast(builder, invokeMethodHelper, intPtrType, string.Empty)));
                });
                il.Append(loadFunctionPointerInstruction);

                il.Append(Instruction.Create(OpCodes.Ret));
            }
            else if (method.Name == "Invoke")
            {
                // For now, generate IL
                // Note that we could probably optimize at callsite too,
                // but probably not necessary if LLVM and sealed class are optimized/inlined well enough

                // ldarg_0
                // ldfld _methodPtr
                il.Append(Instruction.Create(OpCodes.Ldarg_0));
                il.Append(Instruction.Create(OpCodes.Ldfld, methodPtrField));

                // ldarg_0
                // ldfld _target
                il.Append(Instruction.Create(OpCodes.Ldarg_0));
                il.Append(Instruction.Create(OpCodes.Ldfld, targetField));

                var callsite = new CallSite(method.ReturnType);
                callsite.Parameters.Add(new ParameterDefinition(targetField.FieldType));

                foreach (var parameter in method.Parameters)
                {
                    callsite.Parameters.Add(new ParameterDefinition(parameter.Name, parameter.Attributes, parameter.ParameterType));

                    // ldarg
                    il.Append(Instruction.Create(OpCodes.Ldarg, parameter));
                }

                // calli
                il.Append(Instruction.Create(OpCodes.Calli, callsite));

                // ret
                il.Append(Instruction.Create(OpCodes.Ret));
            }
            else
            {
                LLVM.BuildUnreachable(builder);
                return null;
            }
            return body;
        }

        private ValueRef GenerateMulticastInvokeThunk(Class declaringClass)
        {
            // Reuse same signature as Invoke
            var delegateType = corlib.MainModule.GetType(typeof(Delegate).FullName);
            var invokeMethod = declaringClass.Functions.Single(x => x.MethodReference.Name == "Invoke");

            var invokeMethodHelper = LLVM.AddFunction(module, LLVM.GetValueName(invokeMethod.GeneratedValue) + "_MulticastHelper", invokeMethod.FunctionType);
            LLVM.PositionBuilderAtEnd(builder, LLVM.AppendBasicBlockInContext(context, invokeMethodHelper, string.Empty));

            var invokeFunctionType = LLVM.GetElementType(LLVM.TypeOf(invokeMethodHelper));
            bool hasRetValue = LLVM.GetReturnType(invokeFunctionType) != LLVM.VoidTypeInContext(context);
            var delegateArrayType = GetType(new ArrayType(delegateType), TypeState.TypeComplete);

            // Prepare basic blocks
            var forCodeBlock = LLVM.AppendBasicBlockInContext(context, invokeMethodHelper, string.Empty);
            var exitBlock = LLVM.AppendBasicBlockInContext(context, invokeMethodHelper, string.Empty);

            var stack = new List<StackValue>();

            // Load first argument and cast as Delegate[]
            var @this = LLVM.GetParam(invokeMethodHelper, 0);
            @this = LLVM.BuildPointerCast(builder, @this, delegateArrayType.DefaultType, string.Empty);

            // Create index (i = 0)
            var locals = new List<StackValue>();
            locals.Add(new StackValue(StackValueType.Int32, int32, LLVM.BuildAlloca(builder, int32.DefaultType, "i")));
            EmitI4(stack, 0);
            EmitStloc(stack, locals, 0);

            // length = invocationList.Length
            var delegateArray = new StackValue(StackValueType.Object, delegateArrayType, @this);
            stack.Add(delegateArray);
            EmitLdlen(stack);
            EmitConv(stack, Code.Conv_I4);
            var invocationCount = stack.Pop();

            // Iterate over each element in array
            LLVM.BuildBr(builder, forCodeBlock);
            LLVM.PositionBuilderAtEnd(builder, forCodeBlock);

            // Get delegateArray[i]
            stack.Add(delegateArray);
            EmitLdloc(stack, locals, 0);
            EmitLdelem(stack);

            // Call
            var helperArgs = new ValueRef[LLVM.CountParams(invokeMethodHelper)];
            helperArgs[0] = LLVM.BuildPointerCast(builder, stack.Pop().Value, declaringClass.Type.DefaultType, string.Empty);
            for (int i = 1; i < helperArgs.Length; ++i)
            {
                helperArgs[i] = LLVM.GetParam(invokeMethodHelper, (uint)i);
            }
            var retValue = LLVM.BuildCall(builder, invokeMethod.GeneratedValue, helperArgs, string.Empty);

            // i++
            EmitLdloc(stack, locals, 0);
            var lastStack = stack[stack.Count - 1];
            var incrementedValue = LLVM.BuildAdd(builder, lastStack.Value, LLVM.ConstInt(int32Type, 1, false), string.Empty);
            lastStack = new StackValue(StackValueType.Int32, int32, incrementedValue);
            stack[stack.Count - 1] = lastStack;
            EmitStloc(stack, locals, 0);

            // if (i < length)
            //     goto forCodeBlock
            // else
            //     return lastReturnValue;
            EmitLdloc(stack, locals, 0);
            stack.Add(invocationCount);
            EmitConditionalBranch(stack, forCodeBlock, exitBlock, Code.Blt_S);

            LLVM.PositionBuilderAtEnd(builder, exitBlock);

            // Return value
            if (hasRetValue)
                LLVM.BuildRet(builder, retValue);
            else
                LLVM.BuildRetVoid(builder);

            if (LLVM.VerifyFunction(invokeMethodHelper, VerifierFailureAction.PrintMessageAction))
            {
                throw new InvalidOperationException(string.Format("Verification failed for function {0}", invokeMethodHelper));
            }

            return invokeMethodHelper;
        }

        private ValueRef GenerateStaticInvokeThunk(Class declaringClass)
        {
            var invokeMethodHelper = CreateInvokeMethodHelper(declaringClass, "_StaticHelper");

            EmitStaticInvokeCall(invokeMethodHelper);

            return invokeMethodHelper;
        }

        private ValueRef CreateInvokeMethodHelper(Class declaringClass, string nameSuffix)
        {
            // Reuse same signature as Invoke
            var invokeMethod = declaringClass.Functions.Single(x => x.MethodReference.Name == "Invoke");

            // Create method
            var invokeMethodHelper = LLVM.AddFunction(module, LLVM.GetValueName(invokeMethod.GeneratedValue) + nameSuffix, invokeMethod.FunctionType);
            LLVM.PositionBuilderAtEnd(builder2, LLVM.AppendBasicBlockInContext(context, invokeMethodHelper, string.Empty));

            return invokeMethodHelper;
        }

        private void EmitStaticInvokeCall(ValueRef invokeMethodHelper)
        {
            // Get Delegate type and _methodPtrAux field
            var delegateType = corlib.MainModule.GetType(typeof(Delegate).FullName);
            var methodPtrAuxField = delegateType.Fields.First(x => x.Name == "_methodPtrAux");

            var invokeFunctionType = LLVM.GetElementType(LLVM.TypeOf(invokeMethodHelper));

            // Ignore first arguments
            var helperArgs = new ValueRef[LLVM.CountParams(invokeMethodHelper) - 1];
            var helperArgTypes = new TypeRef[helperArgs.Length];
            for (int i = 0; i < helperArgs.Length; ++i)
            {
                helperArgs[i] = LLVM.GetParam(invokeMethodHelper, (uint)i + 1);
                helperArgTypes[i] = LLVM.TypeOf(helperArgs[i]);
            }
            var helperFunctionType = LLVM.FunctionType(LLVM.GetReturnType(invokeFunctionType), helperArgTypes, false);

            // 1. Load static function pointers (arg0->_methodPtrAux)
            var @this = LLVM.GetParam(invokeMethodHelper, 0);

            // Compute field address
            var fieldAddress = ComputeFieldAddress(builder2, GetType(delegateType, TypeState.TypeComplete).Fields[methodPtrAuxField], StackValueType.Object, @this);

            //    Load value from field
            var methodPtrAux = LLVM.BuildLoad(builder2, fieldAddress, string.Empty);
            methodPtrAux = LLVM.BuildPointerCast(builder2, methodPtrAux, LLVM.PointerType(helperFunctionType, 0), string.Empty);

            // 2. Call method
            var methodPtrAuxCall = LLVM.BuildCall(builder2, methodPtrAux, helperArgs, string.Empty);

            // Return value
            if (LLVM.GetReturnType(invokeFunctionType) != LLVM.VoidTypeInContext(context))
                LLVM.BuildRet(builder2, methodPtrAuxCall);
            else
                LLVM.BuildRetVoid(builder2);
        }
    }
}