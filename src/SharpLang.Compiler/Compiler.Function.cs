using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SharpLang.CompilerServices.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public partial class Compiler
    {
        /// <summary>
        /// Gets the function.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <returns></returns>
        private Function GetFunction(MethodReference method)
        {
            Function function;
            if (functions.TryGetValue(method, out function))
                return function;

            return CreateFunction(method);
        }

        /// <summary>
        /// Creates the function.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <returns></returns>
        Function CreateFunction(MethodReference method)
        {
            Function function;
            if (functions.TryGetValue(method, out function))
                return function;

            var numParams = method.Parameters.Count;
            if (method.HasThis)
                numParams++;
            var parameterTypes = new Type[numParams];
            var parameterTypesLLVM = new TypeRef[numParams];
            for (int index = 0; index < numParams; index++)
            {
                TypeReference parameterTypeReference;
                if (method.HasThis && index == 0)
                {
                    parameterTypeReference = method.DeclaringType;
                }
                else
                {
                    var parameter = method.Parameters[method.HasThis ? index - 1 : index];
                    parameterTypeReference = ResolveGenericsVisitor.Process(method, parameter.ParameterType);
                }
                var parameterType = CreateType(parameterTypeReference);
                if (parameterType.DefaultType.Value == IntPtr.Zero)
                    throw new InvalidOperationException();
                parameterTypes[index] = parameterType;
                parameterTypesLLVM[index] = parameterType.DefaultType;
            }

            var returnType = CreateType(ResolveGenericsVisitor.Process(method, method.ReturnType));

            // Generate function global
            bool isExternal = method.DeclaringType.Resolve().Module.Assembly != assembly;
            var methodMangledName = Regex.Replace(method.FullName, @"(\W)", "_");
            var functionType = LLVM.FunctionType(returnType.DefaultType, parameterTypesLLVM, false);

            var hasBody = method.Resolve().HasBody;
            var functionGlobal = hasBody
                ? LLVM.AddFunction(module, methodMangledName, functionType)
                : new ValueRef(IntPtr.Zero);

            function = new Function(method, functionType, functionGlobal, returnType, parameterTypes);
            functions.Add(method, function);

            if (hasBody)
            {
                if (isExternal)
                {
                    // External weak linkage
                    LLVM.SetLinkage(functionGlobal, Linkage.ExternalWeakLinkage);
                }
                else
                {
                    // Need to compile
                    LLVM.SetLinkage(functionGlobal, Linkage.ExternalLinkage);
                    methodsToCompile.Enqueue(new KeyValuePair<MethodReference, Function>(method, function));
                }
            }

            return function;
        }

        /// <summary>
        /// Compiles the given method definition.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <param name="function">The function.</param>
        /// <exception cref="System.NotSupportedException"></exception>
        /// <exception cref="System.NotImplementedException"></exception>
        /// <exception cref="System.InvalidOperationException">Backward jump with a non-empty stack unknown target.</exception>
        private void CompileFunction(MethodReference methodReference, Function function)
        {
            var method = methodReference.Resolve();

            if (method.HasBody == false)
                return;

            var numParams = method.Parameters.Count;
            var body = method.Body;
            var functionGlobal = function.GeneratedValue;

            var basicBlock = LLVM.AppendBasicBlockInContext(context, functionGlobal, string.Empty);

            LLVM.PositionBuilderAtEnd(builder, basicBlock);

            // Create stack, locals and args
            var stack = new List<StackValue>(body.MaxStackSize);
            var locals = new List<StackValue>(body.Variables.Count);
            var args = new List<StackValue>(numParams);

            // Process locals
            foreach (var local in body.Variables)
            {
                if (local.IsPinned)
                    throw new NotSupportedException();

                var type = CreateType(local.VariableType);
                locals.Add(new StackValue(type.StackType, type, LLVM.BuildAlloca(builder, type.DefaultType, local.Name)));
            }

            // Process args
            for (int index = 0; index < function.ParameterTypes.Length; index++)
            {
                var argType = function.ParameterTypes[index];
                var arg = LLVM.GetParam(functionGlobal, (uint)index);
                args.Add(new StackValue(argType.StackType, argType, arg));
            }

            // Some wasted space due to unused offsets, but we only keep one so it should be fine.
            // TODO: Reuse same allocated instance per thread, and grow it only if necessary
            var branchTargets = new bool[body.CodeSize];
            var basicBlocks = new BasicBlockRef[body.CodeSize];
            var forwardStacks = new StackValue[body.CodeSize][];

            // Find branch targets (which will require PHI node for stack merging)
            for (int index = 0; index < body.Instructions.Count; index++)
            {
                var instruction = body.Instructions[index];

                var flowControl = instruction.OpCode.FlowControl;

                // Process branch targets
                if (flowControl == FlowControl.Cond_Branch
                    || flowControl == FlowControl.Branch)
                {
                    var target = (Instruction)instruction.Operand;

                    // Operand Target can be reached
                    branchTargets[target.Offset] = true;
                }

                // Need to enforce a block to be created for the next instruction after a conditional branch
                // TODO: Break?
                if (flowControl == FlowControl.Cond_Branch)
                {
                    if (instruction.Next != null)
                        branchTargets[instruction.Next.Offset] = true;
                }
            }

            // Create basic block
            // TODO: Could be done during previous pass
            for (int offset = 0; offset < branchTargets.Length; offset++)
            {
                // Create a basic block if this was a branch target or an instruction after a conditional branch
                if (branchTargets[offset])
                {
                    basicBlocks[offset] = LLVM.AppendBasicBlockInContext(context, functionGlobal, string.Format("L_{0:x4}", offset));
                }
            }

            // Specify if we have to manually add an unconditional branch to go to next block (flowing) or not (due to a previous explicit conditional branch)
            bool flowingToNextBlock = true;

            foreach (var instruction in body.Instructions)
            {
                var branchTarget = branchTargets[instruction.Offset];

                if (branchTarget)
                {
                    var previousBasicBlock = basicBlock;
                    basicBlock = basicBlocks[instruction.Offset];

                    var forwardStack = forwardStacks[instruction.Offset];

                    if (flowingToNextBlock)
                    {
                        // Add a jump from previous block to new block
                        LLVM.BuildBr(builder, basicBlock);

                        // Flow stack and build PHI nodes
                        MergeStack(stack, previousBasicBlock, ref forwardStack, basicBlock);
                        forwardStacks[instruction.Offset] = forwardStack;
                    }

                    // Clear stack
                    stack.Clear();

                    // Try to restore stack from previously reached forward jump
                    if (forwardStack != null)
                    {
                        // Restoring stack as it was during one of previous forward jump
                        stack.AddRange(forwardStack);
                    }
                    else
                    {
                        // TODO: Actually, need to restore stack from one of previous forward jump instruction, if any
                        // (if only backward jumps, spec says it should be empty to avoid multi-pass IL scanning,
                        // but that's something we could also support later -- Mono doesn't but MS.NET does)
                    }

                    // Position builder to write at beginning of new block
                    LLVM.PositionBuilderAtEnd(builder, basicBlock);
                }

                // Reset states
                flowingToNextBlock = true;

                switch (instruction.OpCode.Code)
                {
                    case Code.Ret:
                    {
                        EmitRet(stack, methodReference);
                        flowingToNextBlock = false;
                        break;
                    }
                    case Code.Call:
                    {
                        var targetMethodReference = (MethodReference)instruction.Operand;
                        var targetMethod = GetFunction(targetMethodReference);

                        EmitCall(stack, targetMethod);

                        break;
                    }
                    case Code.Callvirt:
                    {
                        var targetMethodReference = (MethodReference)instruction.Operand;
                        var targetMethod = GetFunction(targetMethodReference);

                        // TODO: Interface calls & virtual calls
                        if ((targetMethod.MethodReference.Resolve().Attributes & MethodAttributes.Virtual) == MethodAttributes.Virtual)
                        {
                            // Build indices for GEP
                            var indices = new[]
                            {
                                LLVM.ConstInt(int32Type, 0, false),                                 // Pointer indirection
                                LLVM.ConstInt(int32Type, (int)ObjectFields.RuntimeTypeInfo, false), // Access RTTI
                            };

                            var thisObject = stack[stack.Count - targetMethod.ParameterTypes.Length];
                            var @class = GetClass(thisObject.Type.TypeReference);

                            // TODO: Checking actual type stored in thisObject we might be able to statically resolve method?

                            // Get RTTI pointer
                            var rttiPointer = LLVM.BuildInBoundsGEP(builder, thisObject.Value, indices, string.Empty);
                            rttiPointer = LLVM.BuildLoad(builder, rttiPointer, string.Empty);

                            // Cast to expected RTTI type
                            rttiPointer = LLVM.BuildPointerCast(builder, rttiPointer, LLVM.TypeOf(@class.GeneratedRuntimeTypeInfoGlobal), string.Empty);

                            if (targetMethod.MethodReference.DeclaringType.Resolve().IsInterface)
                            {
                                // Interface call

                                // Get method stored in IMT slot
                                indices = new[]
                                {
                                    LLVM.ConstInt(int32Type, 0, false),                                                 // Pointer indirection
                                    LLVM.ConstInt(int32Type, (int)RuntimeTypeInfoFields.InterfaceMethodTable, false),   // Access IMT
                                    LLVM.ConstInt(int32Type, (ulong)targetMethod.VirtualSlot, false),                   // Access specific IMT slot
                                };

                                var imtEntry = LLVM.BuildInBoundsGEP(builder, rttiPointer, indices, string.Empty);

                                var indices2 = new[]
                                {
                                    LLVM.ConstInt(int32Type, 0, false), // Pointer indirection
                                    LLVM.ConstInt(int32Type, 0, false), // Access to method slot
                                };
                                var imtMethod = LLVM.BuildInBoundsGEP(builder, imtEntry, indices2, string.Empty);
                                var methodPointer = LLVM.BuildLoad(builder, imtMethod, string.Empty);
                                var resolvedMethod = LLVM.BuildPointerCast(builder, methodPointer, LLVM.PointerType(targetMethod.FunctionType, 0), string.Empty);

                                // TODO: Compare method ID and iterate in the linked list until the correct match is found
                                // If no match is found, it's likely due to covariance/contravariance, so we will need a fallback
                                var methodId = GetMethodId(targetMethod.MethodReference);

                                // Emit call
                                EmitCall(stack, targetMethod, resolvedMethod);
                            }
                            else
                            {
                                // Virtual table call

                                // Get method stored in vtable slot
                                indices = new[]
                                {
                                    LLVM.ConstInt(int32Type, 0, false),                                         // Pointer indirection
                                    LLVM.ConstInt(int32Type, (int)RuntimeTypeInfoFields.VirtualTable, false),   // Access vtable
                                    LLVM.ConstInt(int32Type, (ulong)targetMethod.VirtualSlot, false),           // Access specific vtable slot
                                };

                                var vtable = LLVM.BuildInBoundsGEP(builder, rttiPointer, indices, string.Empty);
                                var resolvedMethod = LLVM.BuildLoad(builder, vtable, string.Empty);

                                // Emit call
                                EmitCall(stack, targetMethod, resolvedMethod);
                            }
                        }
                        else
                        {
                            // Normal call
                            // Callvirt on non-virtual function is only done to force "this" NULL check
                            // However, that's probably a part of the .NET spec that we want to skip for performance reasons,
                            // so maybe we should keep this as is?
                            EmitCall(stack, targetMethod);
                        }

                        break;
                    }
                    case Code.Initobj:
                    {
                        var address = stack.Pop();
                        var typeReference = (TypeReference)instruction.Operand;
                        var type = GetType(typeReference);
                        EmitInitobj(address, type);
                        break;
                    }

                    case Code.Newobj:
                    {
                        var ctorReference = (MethodReference)instruction.Operand;
                        var ctor = GetFunction(ctorReference);
                        var type = GetType(ctorReference.DeclaringType);

                        EmitNewobj(stack, type, ctor);

                        break;
                    }

                    #region Box/Unbox opcodes
                    case Code.Box:
                    {
                        var typeReference = (TypeReference)instruction.Operand;
                        var @class = GetClass(typeReference);

                        var valueType = stack.Pop();

                        // Allocate object
                        var allocatedObject = AllocateObject(@class.Type);

                        var dataPointer = GetDataPointer(allocatedObject);

                        // Copy data
                        LLVM.BuildStore(builder, valueType.Value, dataPointer);

                        // Add created object on the stack
                        stack.Add(new StackValue(StackValueType.Object, @class.Type, allocatedObject));

                        break;
                    }

                    case Code.Unbox_Any:
                    {
                        var typeReference = (TypeReference)instruction.Operand;
                        var @class = GetClass(typeReference);

                        var obj = stack.Pop();

                        if (typeReference.IsValueType)
                        {
                            // TODO: check type?
                            var objCast = LLVM.BuildPointerCast(builder, obj.Value, LLVM.PointerType(@class.ObjectType, 0), string.Empty);

                            var dataPointer = GetDataPointer(objCast);

                            var data = LLVM.BuildLoad(builder, dataPointer, string.Empty);

                            stack.Add(new StackValue(StackValueType.Value, @class.Type, data));
                        }
                        else
                        {
                            // Should act as "castclass" on reference types
                            goto case Code.Castclass;
                        }

                        break;
                    }
                    #endregion

                    #region Array opcodes (Newarr, Ldlen, Stelem_Ref, etc...)
                    case Code.Newarr:
                    {
                        var elementType = GetType((TypeReference)instruction.Operand);

                        EmitNewarr(stack, elementType);
 
                        break;
                    }
                    case Code.Ldlen:
                    {
                        EmitLdlen(stack);

                        break;
                    }
                    case Code.Ldelem_Ref:
                    {
                        EmitLdelem_Ref(stack);

                        break;
                    }
                    case Code.Stelem_Ref:
                    {
                        EmitStelem_Ref(stack);

                        break;
                    }
                    #endregion

                    #region Load opcodes (Ldc, Ldstr, Ldloc, etc...)
                    // Ldc_I4
                    case Code.Ldc_I4_0:
                    case Code.Ldc_I4_1:
                    case Code.Ldc_I4_2:
                    case Code.Ldc_I4_3:
                    case Code.Ldc_I4_4:
                    case Code.Ldc_I4_5:
                    case Code.Ldc_I4_6:
                    case Code.Ldc_I4_7:
                    case Code.Ldc_I4_8:
                    {
                        var value = instruction.OpCode.Code - Code.Ldc_I4_0;
                        EmitI4(stack, value);
                        break;
                    }
                    case Code.Ldc_I4_S:
                    {
                        var value = (sbyte)instruction.Operand;
                        EmitI4(stack, value);
                        break;
                    }
                    case Code.Ldc_I4:
                    {
                        var value = (int)instruction.Operand;
                        EmitI4(stack, value);
                        break;
                    }
                    // Ldc_I8
                    case Code.Ldc_I8:
                    {
                        var value = (long)instruction.Operand;
                        EmitI8(stack, value);
                        break;
                    }
                    // Ldarg
                    case Code.Ldarg_0:
                    case Code.Ldarg_1:
                    case Code.Ldarg_2:
                    case Code.Ldarg_3:
                    {
                        var value = instruction.OpCode.Code - Code.Ldarg_0;
                        EmitLdarg(stack, args, value);
                        break;
                    }
                    case Code.Ldarg_S:
                    case Code.Ldarg:
                    {
                        var value = ((VariableDefinition)instruction.Operand).Index;
                        EmitLdarg(stack, args, value);
                        break;
                    }
                    case Code.Ldstr:
                    {
                        var operand = (string)instruction.Operand;

                        EmitLdstr(stack, operand);

                        break;
                    }

                    // Ldloc
                    case Code.Ldloc_0:
                    case Code.Ldloc_1:
                    case Code.Ldloc_2:
                    case Code.Ldloc_3:
                    {
                        var localIndex = instruction.OpCode.Code - Code.Ldloc_0;
                        EmitLdloc(stack, locals, localIndex);
                        break;
                    }
                    case Code.Ldloc:
                    case Code.Ldloc_S:
                    {
                        var localIndex = ((VariableDefinition)instruction.Operand).Index;
                        EmitLdloc(stack, locals, localIndex);
                        break;
                    }
                    case Code.Ldloca:
                    case Code.Ldloca_S:
                    {
                        var localIndex = ((VariableDefinition)instruction.Operand).Index;
                        EmitLdloca(stack, locals, localIndex);
                        break;
                    }
                    case Code.Ldfld:
                    {
                        var fieldReference = (FieldReference)instruction.Operand;

                        // Resolve class and field
                        var @class = GetClass(ResolveGenericsVisitor.Process(methodReference.DeclaringType, fieldReference.DeclaringType));
                        var field = @class.Fields[fieldReference.Resolve()];

                        EmitLdfld(stack, field);

                        break;
                    }
                    case Code.Ldsfld:
                    {
                        var fieldReference = (FieldReference)instruction.Operand;

                        // Resolve class and field
                        var @class = GetClass(ResolveGenericsVisitor.Process(methodReference.DeclaringType, fieldReference.DeclaringType));
                        var field = @class.Fields[fieldReference.Resolve()];

                        EmitLdsfld(stack, field);

                        break;
                    }
                    #endregion

                    #region Store opcodes (Stloc, etc...)
                    // Stloc
                    case Code.Stloc_0:
                    case Code.Stloc_1:
                    case Code.Stloc_2:
                    case Code.Stloc_3:
                    {
                        var localIndex = instruction.OpCode.Code - Code.Stloc_0;
                        EmitStloc(stack, locals, localIndex);
                        break;
                    }
                    case Code.Stloc:
                    case Code.Stloc_S:
                    {
                        var localIndex = ((VariableDefinition)instruction.Operand).Index;
                        EmitStloc(stack, locals, localIndex);
                        break;
                    }
                    case Code.Stfld:
                    {
                        var fieldReference = (FieldReference)instruction.Operand;

                        // Resolve class and field
                        var @class = GetClass(ResolveGenericsVisitor.Process(methodReference.DeclaringType, fieldReference.DeclaringType));
                        var field = @class.Fields[fieldReference.Resolve()];

                        EmitStfld(stack, field);

                        break;
                    }

                    case Code.Stsfld:
                    {
                        var fieldReference = (FieldReference)instruction.Operand;

                        // Resolve class and field
                        var @class = GetClass(ResolveGenericsVisitor.Process(methodReference.DeclaringType, fieldReference.DeclaringType));
                        var field = @class.Fields[fieldReference.Resolve()];

                        EmitStsfld(stack, field);
                        break;
                    }
                    #endregion

                    #region Branching (Brtrue, Brfalse, etc...)
                    case Code.Br:
                    case Code.Br_S:
                    {
                        var targetInstruction = (Instruction)instruction.Operand;
                        EmitBr(basicBlocks[targetInstruction.Offset]);
                        flowingToNextBlock = false;
                        break;
                    }
                    case Code.Brfalse:
                    case Code.Brfalse_S:
                    {
                        var targetInstruction = (Instruction)instruction.Operand;
                        EmitBrfalse(stack, basicBlocks[targetInstruction.Offset], basicBlocks[instruction.Next.Offset]);
                        flowingToNextBlock = false;
                        break;
                    }
                    case Code.Brtrue:
                    case Code.Brtrue_S:
                    {
                        var targetInstruction = (Instruction)instruction.Operand;
                        EmitBrtrue(stack, basicBlocks[targetInstruction.Offset], basicBlocks[instruction.Next.Offset]);
                        flowingToNextBlock = false;
                        break;
                    }
                    #endregion

                    #region Conversion opcodes (Conv_U, Conv_I, etc...)
                    case Code.Conv_U:
                    case Code.Conv_I:
                    case Code.Conv_U1:
                    case Code.Conv_I1:
                    case Code.Conv_U2:
                    case Code.Conv_I2:
                    case Code.Conv_U4:
                    case Code.Conv_I4:
                    case Code.Conv_U8:
                    case Code.Conv_I8:
                    {
                        var value = stack.Pop();

                        uint intermediateWidth;
                        System.Type intermediateRealType;
                        bool outputNativeInt = false;
                        bool isSigned;
                        switch (instruction.OpCode.Code)
                        {
                            case Code.Conv_U: isSigned = false; intermediateWidth = (uint)intPtrSize; intermediateRealType = typeof(UIntPtr); outputNativeInt = true; break;
                            case Code.Conv_I: isSigned = true; intermediateWidth = (uint)intPtrSize; intermediateRealType = typeof(IntPtr); outputNativeInt = true; break;
                            case Code.Conv_U1: isSigned = false; intermediateWidth = 8; intermediateRealType = typeof(byte); break;
                            case Code.Conv_I1: isSigned = true; intermediateWidth = 8; intermediateRealType = typeof(sbyte); break;
                            case Code.Conv_U2: isSigned = false; intermediateWidth = 16; intermediateRealType = typeof(ushort); break;
                            case Code.Conv_I2: isSigned = true; intermediateWidth = 16; intermediateRealType = typeof(short); break;
                            case Code.Conv_U4: isSigned = false; intermediateWidth = 32; intermediateRealType = typeof(uint); break;
                            case Code.Conv_I4: isSigned = true; intermediateWidth = 32; intermediateRealType = typeof(int); break;
                            case Code.Conv_U8: isSigned = false; intermediateWidth = 64; intermediateRealType = typeof(ulong); break;
                            case Code.Conv_I8: isSigned = true; intermediateWidth = 64; intermediateRealType = typeof(long); break;
                            default:
                                throw new InvalidOperationException();
                        }

                        var currentValue = value.Value;

                        if (value.StackType == StackValueType.NativeInt)
                        {
                            // Convert to integer
                            currentValue = LLVM.BuildPtrToInt(builder, currentValue, nativeIntType, string.Empty);
                        }
                        else if (value.StackType == StackValueType.Reference)
                        {
                            if (instruction.OpCode.Code != Code.Conv_U8 && instruction.OpCode.Code != Code.Conv_U)
                                throw new InvalidOperationException();

                            // Convert to integer
                            currentValue = LLVM.BuildPtrToInt(builder, currentValue, nativeIntType, string.Empty);
                        }
                        else if (value.StackType == StackValueType.Float)
                        {
                            // TODO: Float conversions
                            throw new NotImplementedException();
                        }

                        var inputType = LLVM.TypeOf(currentValue);
                        var inputWidth = LLVM.GetIntTypeWidth(inputType);
                        var smallestWidth = Math.Min(intermediateWidth, inputWidth);
                        var smallestType = LLVM.IntTypeInContext(context, smallestWidth);
                        var outputWidth = Math.Max(intermediateWidth, 32);

                        // Truncate (if necessary)
                        if (smallestWidth < inputWidth)
                            currentValue = LLVM.BuildTrunc(builder, currentValue, smallestType, string.Empty);

                        // Reextend to appropriate type (if necessary)
                        if (outputWidth > smallestWidth)
                        {
                            var outputIntType = LLVM.IntTypeInContext(context, outputWidth);
                            if (isSigned)
                                currentValue = LLVM.BuildSExt(builder, currentValue, outputIntType, string.Empty);
                            else
                                currentValue = LLVM.BuildZExt(builder, currentValue, outputIntType, string.Empty);
                        }

                        // Convert to native int (if necessary)
                        if (outputNativeInt)
                            currentValue = LLVM.BuildIntToPtr(builder, currentValue, intPtrType, string.Empty);

                        // Add constant integer value to stack
                        switch (instruction.OpCode.Code)
                        {
                            case Code.Conv_U:
                            case Code.Conv_I:
                                stack.Add(new StackValue(StackValueType.NativeInt, intPtr, currentValue));
                                break;
                            case Code.Conv_U1:
                            case Code.Conv_I1:
                            case Code.Conv_U2:
                            case Code.Conv_I2:
                            case Code.Conv_U4:
                            case Code.Conv_I4:
                                stack.Add(new StackValue(StackValueType.Int32, int32, currentValue));
                                break;
                            case Code.Conv_U8:
                            case Code.Conv_I8:
                                stack.Add(new StackValue(StackValueType.Int64, int64, currentValue));
                                break;
                            default:
                                throw new InvalidOperationException();
                        }

                        break;
                    }
                    #endregion

                    #region Binary operation opcodes (Add, Sub, etc...)
                    case Code.Add:
                    case Code.Add_Ovf:
                    case Code.Add_Ovf_Un:
                    case Code.Sub:
                    case Code.Sub_Ovf:
                    case Code.Sub_Ovf_Un:
                    case Code.Mul:
                    case Code.Mul_Ovf:
                    case Code.Mul_Ovf_Un:
                    case Code.Div:
                    case Code.Div_Un:
                    case Code.Rem:
                    case Code.Rem_Un:
                    case Code.Shl:
                    case Code.Shr:
                    case Code.Shr_Un:
                    case Code.Xor:
                    case Code.Or:
                    case Code.And:
                    {
                        var operand2 = stack.Pop();
                        var operand1 = stack.Pop();

                        var value1 = operand1.Value;
                        var value2 = operand2.Value;

                        StackValueType outputStackType;

                        bool isShiftOperation = false;
                        bool isIntegerOperation = false;

                        // Detect shift and integer operations
                        switch (instruction.OpCode.Code)
                        {
                            case Code.Shl:
                            case Code.Shr:
                            case Code.Shr_Un:
                                isShiftOperation = true;
                                break;
                            case Code.Xor:
                            case Code.Or:
                            case Code.And:
                            case Code.Div_Un:
                            case Code.Not:
                                isIntegerOperation = true;
                                break;
                        }

                        if (isShiftOperation) // Shift operations are specials
                        {
                            switch (operand2.StackType)
                            {
                                case StackValueType.Int32:
                                case StackValueType.NativeInt:
                                    value2 = LLVM.BuildPtrToInt(builder, value2, nativeIntType, string.Empty);
                                    break;
                                default:
                                    goto InvalidBinaryOperation;
                            }

                            // Check first operand, and convert second operand to match first one
                            switch (operand1.StackType)
                            {
                                case StackValueType.Int32:
                                    value2 = LLVM.BuildIntCast(builder, value2, int32Type, string.Empty);
                                    break;
                                case StackValueType.Int64:
                                    value2 = LLVM.BuildIntCast(builder, value2, int64Type, string.Empty);
                                    break;
                                case StackValueType.NativeInt:
                                    value1 = LLVM.BuildPtrToInt(builder, value1, nativeIntType, string.Empty);
                                    value2 = LLVM.BuildIntCast(builder, value2, nativeIntType, string.Empty);
                                    break;
                                default:
                                    goto InvalidBinaryOperation;
                            }

                            // Output type is determined by first operand
                            outputStackType = operand1.StackType;
                        }
                        else if (operand1.StackType == operand2.StackType) // Diagonal
                        {
                            // Check type
                            switch (operand1.StackType)
                            {
                                case StackValueType.Int32:
                                case StackValueType.Int64:
                                case StackValueType.Float:
                                    outputStackType = operand1.StackType;
                                    break;
                                case StackValueType.NativeInt:
                                    value1 = LLVM.BuildPtrToInt(builder, value1, nativeIntType, string.Empty);
                                    value2 = LLVM.BuildPtrToInt(builder, value2, nativeIntType, string.Empty);
                                    outputStackType = operand1.StackType;
                                    break;
                                case StackValueType.Reference:
                                    if (instruction.OpCode.Code != Code.Sub && instruction.OpCode.Code != Code.Sub_Ovf_Un)
                                        goto InvalidBinaryOperation;
                                    value1 = LLVM.BuildPtrToInt(builder, value1, nativeIntType, string.Empty);
                                    value2 = LLVM.BuildPtrToInt(builder, value2, nativeIntType, string.Empty);
                                    outputStackType = StackValueType.NativeInt;
                                    break;
                                default:
                                    throw new InvalidOperationException(string.Format("Binary operations are not allowed on {0}.", operand1.StackType));
                            }
                        }
                        else if (operand1.StackType == StackValueType.NativeInt && operand2.StackType == StackValueType.Int32)
                        {
                            value1 = LLVM.BuildPtrToInt(builder, value1, nativeIntType, string.Empty);
                            outputStackType = StackValueType.NativeInt;
                        }
                        else if (operand1.StackType == StackValueType.Int32 && operand2.StackType == StackValueType.NativeInt)
                        {
                            value2 = LLVM.BuildPtrToInt(builder, value2, nativeIntType, string.Empty);
                            outputStackType = StackValueType.NativeInt;
                        }
                        else if (!isIntegerOperation && operand1.StackType == StackValueType.Reference) // ref + [i32, nativeint]
                        {
                            switch (operand2.StackType)
                            {
                                case StackValueType.Int32:
                                    value2 = LLVM.BuildSExt(builder, value2, nativeIntType, string.Empty);
                                    break;
                                case StackValueType.NativeInt:
                                    value2 = LLVM.BuildPtrToInt(builder, value2, nativeIntType, string.Empty);
                                    break;
                                default:
                                    goto InvalidBinaryOperation;
                            }

                            if (instruction.OpCode.Code != Code.Add && instruction.OpCode.Code != Code.Add_Ovf_Un
                                && instruction.OpCode.Code != Code.Sub && instruction.OpCode.Code != Code.Sub_Ovf)
                                goto InvalidBinaryOperation;

                            outputStackType = StackValueType.Reference;
                        }
                        else if (!isIntegerOperation && operand2.StackType == StackValueType.Reference) // [i32, nativeint] + ref
                        {
                            switch (operand1.StackType)
                            {
                                case StackValueType.Int32:
                                    value1 = LLVM.BuildSExt(builder, value1, nativeIntType, string.Empty);
                                    break;
                                case StackValueType.NativeInt:
                                    value1 = LLVM.BuildPtrToInt(builder, value1, nativeIntType, string.Empty);
                                    break;
                                default:
                                    goto InvalidBinaryOperation;
                            }

                            if (instruction.OpCode.Code != Code.Add && instruction.OpCode.Code != Code.Add_Ovf_Un)
                                goto InvalidBinaryOperation;

                            outputStackType = StackValueType.Reference;
                        }
                        else
                        {
                            goto InvalidBinaryOperation;
                        }

                        ValueRef result;

                        // Perform binary operation
                        if (operand1.StackType == StackValueType.Float)
                        {
                            switch (instruction.OpCode.Code)
                            {
                                case Code.Add:          result = LLVM.BuildFAdd(builder, value1, value2, string.Empty); break;
                                case Code.Sub:          result = LLVM.BuildFSub(builder, value1, value2, string.Empty); break;
                                case Code.Mul:          result = LLVM.BuildFMul(builder, value1, value2, string.Empty); break;
                                case Code.Div:          result = LLVM.BuildFDiv(builder, value1, value2, string.Empty); break;
                                case Code.Rem:          result = LLVM.BuildFRem(builder, value1, value2, string.Empty); break;
                                default:
                                    goto InvalidBinaryOperation;
                            }
                        }
                        else
                        {
                            switch (instruction.OpCode.Code)
                            {
                                case Code.Add:          result = LLVM.BuildAdd(builder, value1, value2, string.Empty); break;
                                case Code.Add_Ovf:      result = LLVM.BuildNSWAdd(builder, value1, value2, string.Empty); break;
                                case Code.Add_Ovf_Un:   result = LLVM.BuildNUWAdd(builder, value1, value2, string.Empty); break;
                                case Code.Sub:          result = LLVM.BuildSub(builder, value1, value2, string.Empty); break;
                                case Code.Sub_Ovf:      result = LLVM.BuildNSWSub(builder, value1, value2, string.Empty); break;
                                case Code.Sub_Ovf_Un:   result = LLVM.BuildNUWSub(builder, value1, value2, string.Empty); break;
                                case Code.Mul:          result = LLVM.BuildMul(builder, value1, value2, string.Empty); break;
                                case Code.Mul_Ovf:      result = LLVM.BuildNSWMul(builder, value1, value2, string.Empty); break;
                                case Code.Mul_Ovf_Un:   result = LLVM.BuildNUWMul(builder, value1, value2, string.Empty); break;
                                case Code.Div:          result = LLVM.BuildSDiv(builder, value1, value2, string.Empty); break;
                                case Code.Div_Un:       result = LLVM.BuildUDiv(builder, value1, value2, string.Empty); break;
                                case Code.Rem:          result = LLVM.BuildSRem(builder, value1, value2, string.Empty); break;
                                case Code.Rem_Un:       result = LLVM.BuildURem(builder, value1, value2, string.Empty); break;
                                case Code.Shl:          result = LLVM.BuildShl(builder, value1, value2, string.Empty); break;
                                case Code.Shr:          result = LLVM.BuildAShr(builder, value1, value2, string.Empty); break;
                                case Code.Shr_Un:       result = LLVM.BuildLShr(builder, value1, value2, string.Empty); break;
                                case Code.And:          result = LLVM.BuildAnd(builder, value1, value2, string.Empty); break;
                                case Code.Or:           result = LLVM.BuildOr(builder, value1, value2, string.Empty); break;
                                case Code.Xor:          result = LLVM.BuildXor(builder, value1, value2, string.Empty); break;
                                default:
                                    goto InvalidBinaryOperation;
                            }

                            // TODO: Perform overflow check
                            switch (instruction.OpCode.Code)
                            {
                                case Code.Add_Ovf:
                                case Code.Add_Ovf_Un:
                                case Code.Sub_Ovf:
                                case Code.Sub_Ovf_Un:
                                case Code.Mul_Ovf:
                                case Code.Mul_Ovf_Un:
                                    throw new NotImplementedException("Binary operator with overflow check are not implemented.");
                                default:
                                    break;
                            }
                        }

                        Type outputType;

                        switch (outputStackType)
                        {
                            case StackValueType.Int32:
                            case StackValueType.Int64:
                            case StackValueType.Float:
                                // No output conversion required, as it could only have been from same input types (non-shift) or operand 1 (shift)
                                outputType = operand1.Type;
                                break;
                            case StackValueType.NativeInt:
                                outputType = intPtr;
                                break;
                            case StackValueType.Reference:
                                result = LLVM.BuildIntToPtr(builder, result, intPtrType, string.Empty);

                                // Get type from one of its operand (if output is reference type, one of the two operand must be too)
                                if (operand1.StackType == StackValueType.Reference)
                                    outputType = operand1.Type;
                                else if (operand2.StackType == StackValueType.Reference)
                                    outputType = operand2.Type;
                                else
                                    goto InvalidBinaryOperation;
                                break;
                            default:
                                goto InvalidBinaryOperation;
                        }

                        stack.Add(new StackValue(outputStackType, outputType, result));

                        break;

                    InvalidBinaryOperation:
                        throw new InvalidOperationException(string.Format("Binary operation {0} between {1} and {2} is not supported.", instruction.OpCode.Code, operand1.StackType, operand2.StackType));
                    }
                    #endregion

                    case Code.Castclass:
                    default:
                        throw new NotImplementedException(string.Format("Opcode {0} not implemented.", instruction.OpCode));
                }

                // If we do a jump, let's merge stack
                var flowControl = instruction.OpCode.FlowControl;
                if (flowControl == FlowControl.Cond_Branch
                    || flowControl == FlowControl.Branch)
                {
                    var target = (Instruction)instruction.Operand;

                    // Backward jump? Make sure stack was properly created by a previous forward jump, or empty
                    if (target.Offset < instruction.Offset)
                    {
                        var forwardStack = forwardStacks[target.Offset];
                        if (forwardStack != null && forwardStack.Length > 0)
                            throw new InvalidOperationException("Backward jump with a non-empty stack unknown target.");
                    }

                    // Merge stack (add PHI incoming)
                    MergeStack(stack, basicBlock, ref forwardStacks[target.Offset], basicBlocks[target.Offset]);
                }
            }
        }

        private ValueRef GetDataPointer(ValueRef obj)
        {
            // Get data pointer
            var indices = new[]
            {
                LLVM.ConstInt(int32Type, 0, false),                         // Pointer indirection
                LLVM.ConstInt(int32Type, (int)ObjectFields.Data, false),    // Data
            };

            var dataPointer = LLVM.BuildInBoundsGEP(builder, obj, indices, string.Empty);
            return dataPointer;
        }

        /// <summary>
        /// Merges the stack.
        /// </summary>
        /// <param name="sourceStack">The source stack.</param>
        /// <param name="sourceBasicBlock">The source basic block.</param>
        /// <param name="targetStack">The target stack.</param>
        /// <param name="targetBasicBlock">The target basic block.</param>
        private void MergeStack(List<StackValue> sourceStack, BasicBlockRef sourceBasicBlock, ref StackValue[] targetStack, BasicBlockRef targetBasicBlock)
        {
            // First time? Need to create stack and position builder
            if (targetStack == null)
            {
                targetStack = new StackValue[sourceStack.Count];
                if (LLVM.GetLastInstruction(targetBasicBlock).Value != IntPtr.Zero)
                    throw new InvalidOperationException("Target basic block should have no instruction yet.");
                LLVM.PositionBuilderAtEnd(builderPhi, targetBasicBlock);
            }

            for (int index = 0; index < sourceStack.Count; index++)
            {
                var stackValue = sourceStack[index];

                var mergedStackValue = targetStack[index];

                // First time? Need to create PHI node
                if (mergedStackValue == null)
                {
                    // TODO: Check stack type during merging?
                    mergedStackValue = new StackValue(stackValue.StackType, stackValue.Type, LLVM.BuildPhi(builderPhi, LLVM.TypeOf(stackValue.Value), string.Empty));
                    targetStack[index] = mergedStackValue;
                }

                // Add values from previous stack value
                LLVM.AddIncoming(mergedStackValue.Value, new[] { stackValue.Value }, new[] { sourceBasicBlock });
            }
        }
    }
}