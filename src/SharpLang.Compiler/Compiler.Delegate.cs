using System;
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
                var invokeMethodHelper = GenerateStaticInvokeThunk(declaringClass, delegateType, methodPtrAuxField);

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

        private ValueRef GenerateMulticastInvokeThunk(Class declaringClass, TypeDefinition delegateType)
        {
            
        }

        private ValueRef GenerateStaticInvokeThunk(Class declaringClass, TypeDefinition delegateType, FieldDefinition methodPtrAuxField)
        {
            // Reuse same signature as Invoke
            var invokeMethod = declaringClass.Functions.Single(x => x.MethodReference.Name == "Invoke");

            // Create method
            var invokeMethodHelper = LLVM.AddFunction(module, LLVM.GetValueName(invokeMethod.GeneratedValue) + "_Helper",
                invokeMethod.FunctionType);
            LLVM.PositionBuilderAtEnd(builder2,
                LLVM.AppendBasicBlockInContext(context, invokeMethodHelper, string.Empty));

            // Ignore first arguments
            var helperArgs = new ValueRef[LLVM.CountParams(invokeMethodHelper) - 1];
            var helperArgTypes = new TypeRef[helperArgs.Length];
            for (int i = 0; i < helperArgs.Length; ++i)
            {
                helperArgs[i] = LLVM.GetParam(invokeMethodHelper, (uint) i + 1);
                helperArgTypes[i] = LLVM.TypeOf(helperArgs[i]);
            }
            var helperFunctionType = LLVM.FunctionType(LLVM.GetReturnType(invokeMethod.FunctionType), helperArgTypes, false);

            // 1. Load static function pointers (arg0->_methodPtrAux)
            var @this = LLVM.GetParam(invokeMethodHelper, 0);
            var indices = BuildFieldIndices(GetClass(delegateType).Fields[methodPtrAuxField], StackValueType.Object, GetType(declaringClass.Type.TypeReference));

            //    Find field address using GEP
            var fieldAddress = LLVM.BuildInBoundsGEP(builder2, @this, indices, string.Empty);

            //    Load value from field
            var methodPtrAux = LLVM.BuildLoad(builder2, fieldAddress, string.Empty);
            methodPtrAux = LLVM.BuildPointerCast(builder2, methodPtrAux, LLVM.PointerType(helperFunctionType, 0), string.Empty);

            // 2. Call method
            var methodPtrAuxCall = LLVM.BuildCall(builder2, methodPtrAux, helperArgs, string.Empty);

            // Return value
            if (LLVM.GetReturnType(invokeMethod.FunctionType) != LLVM.VoidTypeInContext(context))
                LLVM.BuildRet(builder2, methodPtrAuxCall);
            else
                LLVM.BuildRetVoid(builder2);
            return invokeMethodHelper;
        }
    }
}