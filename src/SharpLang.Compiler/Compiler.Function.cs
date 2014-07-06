using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;
using SharpLang.CompilerServices.Cecil;
using SharpLLVM;
using CallSite = Mono.Cecil.CallSite;

namespace SharpLang.CompilerServices
{
    public partial class Compiler
    {
        // Additional actions that can be added on Nop instructions
        // TODO: Need a better system for longer term (might need Cecil changes?)
        private static readonly ConditionalWeakTable<Instruction, Action<List<StackValue>>> InstructionActions = new ConditionalWeakTable<Instruction, Action<List<StackValue>>>();

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

        private FunctionSignature CreateFunctionSignature(MethodReference context, CallSite callSite)
        {
            var numParams = callSite.Parameters.Count;
            if (callSite.HasThis)
                numParams++;
            var parameterTypes = new Type[numParams];
            var parameterTypesLLVM = new TypeRef[numParams];
            for (int index = 0; index < numParams; index++)
            {
                TypeReference parameterTypeReference;
                var parameter = callSite.Parameters[index];
                parameterTypeReference = ResolveGenericsVisitor.Process(context, parameter.ParameterType);
                var parameterType = CreateType(parameterTypeReference);
                if (parameterType.DefaultType.Value == IntPtr.Zero)
                    throw new InvalidOperationException();
                parameterTypes[index] = parameterType;
                parameterTypesLLVM[index] = parameterType.DefaultType;
            }

            var returnType = CreateType(ResolveGenericsVisitor.Process(context, context.ReturnType));

            return new FunctionSignature(returnType, parameterTypes);
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
            var declaringType = CreateType(ResolveGenericsVisitor.Process(method, method.DeclaringType));
            for (int index = 0; index < numParams; index++)
            {
                TypeReference parameterTypeReference;
                if (method.HasThis && index == 0)
                {
                    parameterTypeReference = declaringType.TypeReference;

                    // Value type uses ByReference type for this
                    if (parameterTypeReference.IsValueType)
                        parameterTypeReference = parameterTypeReference.MakeByReferenceType();
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

            var resolvedMethod = method.Resolve();
            var hasDefinition = resolvedMethod != null
                && (resolvedMethod.HasBody
                    || ((resolvedMethod.ImplAttributes & (MethodImplAttributes.InternalCall | MethodImplAttributes.Runtime)) != 0));
            var functionGlobal = hasDefinition
                ? LLVM.AddFunction(module, methodMangledName, functionType)
                : LLVM.ConstPointerNull(LLVM.PointerType(functionType, 0));

            function = new Function(declaringType, method, functionType, functionGlobal, returnType, parameterTypes);
            functions.Add(method, function);

            if (hasDefinition)
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

        private int UpdateOffsets(MethodBody body)
        {
            var offset = 0;
            foreach (var instruction in body.Instructions)
            {
                instruction.Offset = offset;
                offset += instruction.GetSize();
            }

            // TODO: Guess better
            body.MaxStackSize = 8;

            return offset;
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

            var body = method.Body;
            var codeSize = body != null ? body.CodeSize : 0;
            var functionGlobal = function.GeneratedValue;

            var functionContext = new FunctionCompilerContext(function);
            functionContext.BasicBlock = LLVM.AppendBasicBlockInContext(context, functionGlobal, string.Empty);
            LLVM.PositionBuilderAtEnd(builder, functionContext.BasicBlock);

            if (body == null && (method.ImplAttributes & MethodImplAttributes.Runtime) != 0)
            {
                var declaringClass = GetClass(function.DeclaringType);

                // Generate IL for various methods
                if (declaringClass.BaseType != null &&
                    declaringClass.BaseType.Type.TypeReference.FullName == typeof(MulticastDelegate).FullName)
                {
                    body = GenerateDelegateMethod(method, declaringClass);
                    if (body == null)
                        return;

                    codeSize = UpdateOffsets(body);
                }
            }

            if (body == null)
                return;

            var numParams = method.Parameters.Count;

            // Create stack, locals and args
            var stack = new List<StackValue>(body.MaxStackSize);
            var locals = new List<StackValue>(body.Variables.Count);
            var args = new List<StackValue>(numParams);
            ValueRef ehselectorSlot = new ValueRef();
            ValueRef exnSlot = new ValueRef();
            BasicBlockRef resumeExceptionBlock = new BasicBlockRef();

            functionContext.Stack = stack;

            // Process locals
            foreach (var local in body.Variables)
            {
                if (local.IsPinned)
                    throw new NotSupportedException();

                var type = CreateType(ResolveGenericsVisitor.Process(methodReference, local.VariableType));
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
            var branchTargets = new bool[codeSize];
            var basicBlocks = new BasicBlockRef[codeSize];
            var forwardStacks = new StackValue[codeSize][];

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

            // Setup exception handling
            if (body.HasExceptionHandlers)
            {
                // Add an "ehselector.slot" i32 local, and a "exn.slot" Object reference local
                ehselectorSlot = LLVM.BuildAlloca(builder, int32Type, "ehselector.slot");
                exnSlot = LLVM.BuildAlloca(builder, @object.DefaultType, "exn.slot");

                // Create resume exception block
                resumeExceptionBlock = LLVM.AppendBasicBlockInContext(context, functionGlobal, "eh.resume");
                LLVM.PositionBuilderAtEnd(builder2, resumeExceptionBlock);
                var exceptionObject = LLVM.BuildLoad(builder2, exnSlot, "exn");
                var ehselectorValue = LLVM.BuildLoad(builder2, ehselectorSlot, "sel");

                exceptionObject = LLVM.BuildPointerCast(builder2, exceptionObject, intPtrType, "exn");
                var landingPadValue = LLVM.BuildInsertValue(builder2, LLVM.GetUndef(caughtResultType), exceptionObject, 0, "lpad.val");
                landingPadValue = LLVM.BuildInsertValue(builder2, landingPadValue, ehselectorValue, 1, "lpad.val");

                LLVM.BuildResume(builder2, landingPadValue);

                // Exception handlers blocks are also branch targets
                foreach (var exceptionHandler in body.ExceptionHandlers)
                {
                    branchTargets[exceptionHandler.HandlerStart.Offset] = true;
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

            // Create catch clause stack
            if (body.HasExceptionHandlers)
            {
                // Exception handlers blocks are also branch targets
                foreach (var exceptionHandler in body.ExceptionHandlers)
                {
                    if (exceptionHandler.HandlerType != ExceptionHandlerType.Catch)
                        continue;

                    var handlerStart = exceptionHandler.HandlerStart.Offset;

                    var catchBlock = basicBlocks[handlerStart];
                    var catchClass = GetClass(ResolveGenericsVisitor.Process(methodReference, exceptionHandler.CatchType));

                    // Extract exception
                    LLVM.PositionBuilderAtEnd(builder2, catchBlock);
                    var exceptionObject = LLVM.BuildLoad(builder2, exnSlot, string.Empty);
                    exceptionObject = LLVM.BuildPointerCast(builder2, exceptionObject, catchClass.Type.DefaultType, string.Empty);

                    forwardStacks[handlerStart] = new[]
                    {
                        new StackValue(catchClass.Type.StackType, catchClass.Type, exceptionObject)
                    };
                }
            }

            // Specify if we have to manually add an unconditional branch to go to next block (flowing) or not (due to a previous explicit conditional branch)
            var flowingNextInstructionMode = FlowingNextInstructionMode.Implicit;

            var instructionFlags = InstructionFlags.None;

            var exceptionHandlers = new List<ExceptionHandlerInfo>();

            foreach (var instruction in body.Instructions)
            {
                var branchTarget = branchTargets[instruction.Offset];

                // Check if any exception handlers might have changed
                if (body.HasExceptionHandlers)
                {
                    bool exceptionHandlersChanged = false;

                    // Exit finished exception handlers
                    for (int index = exceptionHandlers.Count - 1; index >= 0; index--)
                    {
                        var exceptionHandler = exceptionHandlers[index];
                        if (instruction == exceptionHandler.Source.TryEnd)
                        {
                            exceptionHandlers.RemoveAt(index);
                            exceptionHandlersChanged = true;
                        }
                        else
                            break;
                    }

                    // Add new exception handlers
                    for (int index = body.ExceptionHandlers.Count - 1; index >= 0; index--)
                    {
                        var exceptionHandler = body.ExceptionHandlers[index];
                        if (instruction == exceptionHandler.TryStart)
                        {
                            var catchDispatchBlock = LLVM.AppendBasicBlockInContext(context, functionGlobal, "catch.dispatch");
                            LLVM.PositionBuilderAtEnd(builder2, catchDispatchBlock);

                            var catchBlock = basicBlocks[exceptionHandler.HandlerStart.Offset];
                            var catchClass = GetClass(ResolveGenericsVisitor.Process(methodReference, exceptionHandler.CatchType));

                            // Compare exception type
                            var ehselectorValue = LLVM.BuildLoad(builder2, ehselectorSlot, "sel");

                            var ehtypeIdFor = LLVM.IntrinsicGetDeclaration(module, (uint) Intrinsics.eh_typeid_for, new TypeRef[0]);
                            var ehtypeid = LLVM.BuildCall(builder2, ehtypeIdFor, new[] { LLVM.ConstBitCast(catchClass.GeneratedRuntimeTypeInfoGlobal, intPtrType) }, string.Empty);

                            // Jump to catch clause if type matches.
                            // Otherwise, go to next exception handler dispatch block (if any), or resume exception block (TODO)
                            var ehtypeComparisonResult = LLVM.BuildICmp(builder2, IntPredicate.IntEQ, ehselectorValue, ehtypeid, string.Empty);
                            LLVM.BuildCondBr(builder2, ehtypeComparisonResult, catchBlock, exceptionHandlers.Count > 0 ? exceptionHandlers.Last().CatchDispatch : resumeExceptionBlock);

                            exceptionHandlers.Add(new ExceptionHandlerInfo(exceptionHandler, catchDispatchBlock));
                            exceptionHandlersChanged = true;
                        }
                    }

                    if (exceptionHandlersChanged)
                    {
                        // Need to generate a new landing pad
                        for (int index = exceptionHandlers.Count - 1; index >= 0; index--)
                        {
                            var exceptionHandler = exceptionHandlers[index];
                            switch (exceptionHandler.Source.HandlerType)
                            {
                                case ExceptionHandlerType.Catch:
                                    break;
                            }
                        }

                        if (exceptionHandlers.Count > 0)
                        {
                            //var handlerStart = exceptionHandlers.Last().HandlerStart.Offset;
                            //functionContext.LandingPadBlock = basicBlocks[handlerStart];

                            // Prepare landing pad block
                            functionContext.LandingPadBlock = LLVM.AppendBasicBlockInContext(context, functionGlobal, "landingpad");
                            LLVM.PositionBuilderAtEnd(builder2, functionContext.LandingPadBlock);
                            var landingPad = LLVM.BuildLandingPad(builder2, caughtResultType, sharpPersonalityFunction, 1, string.Empty);

                            // Extract exception, and store it in exn.slot
                            var exceptionObject = LLVM.BuildExtractValue(builder2, landingPad, 0, string.Empty);
                            exceptionObject = LLVM.BuildPointerCast(builder2, exceptionObject, @object.Class.Type.DefaultType, string.Empty);
                            LLVM.BuildStore(builder2, exceptionObject, exnSlot);

                            // Extract selector slot, and store it in ehselector.slot
                            var exceptionType = LLVM.BuildExtractValue(builder2, landingPad, 1, string.Empty);
                            LLVM.BuildStore(builder2, exceptionType, ehselectorSlot);

                            // Add jump to catch dispatch block
                            LLVM.BuildBr(builder2, exceptionHandlers.Last().CatchDispatch);

                            // Filter exceptions type by type
                            for (int index = exceptionHandlers.Count - 1; index >= 0; index--)
                            {
                                var exceptionHandler = exceptionHandlers[index];

                                // Add landing pad type clause
                                var catchClass = GetClass(ResolveGenericsVisitor.Process(methodReference, exceptionHandler.Source.CatchType));
                                LLVM.AddClause(landingPad, LLVM.ConstBitCast(catchClass.GeneratedRuntimeTypeInfoGlobal, intPtrType));
                            }
                        }
                        else
                        {
                            functionContext.LandingPadBlock = new BasicBlockRef();
                        }
                    }
                }

                if (branchTarget)
                {
                    var previousBasicBlock = functionContext.BasicBlock;
                    functionContext.BasicBlock = basicBlocks[instruction.Offset];

                    var forwardStack = forwardStacks[instruction.Offset];

                    if (flowingNextInstructionMode == FlowingNextInstructionMode.Implicit)
                    {
                        // Add a jump from previous block to new block
                        LLVM.BuildBr(builder, functionContext.BasicBlock);
                    }

                    if (flowingNextInstructionMode != FlowingNextInstructionMode.None)
                    {
                        // If flowing either automatically or explicitely,
                        // flow stack and build PHI nodes
                        MergeStack(stack, previousBasicBlock, ref forwardStack, functionContext.BasicBlock);
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
                    LLVM.PositionBuilderAtEnd(builder, functionContext.BasicBlock);
                }

                // Reset states
                flowingNextInstructionMode = FlowingNextInstructionMode.Implicit;

                var opcode = instruction.OpCode.Code;

                switch (opcode)
                {
                    case Code.Nop:
                    {
                        // TODO: Insert nop? Debugger step?

                        // Check if there is a custom action
                        Action<List<StackValue>> instructionAction;
                        if (InstructionActions.TryGetValue(instruction, out instructionAction))
                        {
                            instructionAction(stack);
                        }
                        break;
                    }
                    case Code.Pop:
                    {
                        // Pop and discard last stack value
                        stack.Pop();
                        break;
                    }
                    case Code.Dup:
                    {
                        // Readd last stack value
                        var lastStackValue = stack[stack.Count - 1];
                        stack.Add(new StackValue(lastStackValue.StackType, lastStackValue.Type, lastStackValue.Value));
                        break;
                    }
                    case Code.Ret:
                    {
                        EmitRet(stack, methodReference);
                        flowingNextInstructionMode = FlowingNextInstructionMode.None;
                        break;
                    }
                    case Code.Call:
                    {
                        var targetMethodReference = ResolveGenericsVisitor.Process(methodReference, (MethodReference)instruction.Operand);
                        var targetMethod = GetFunction(targetMethodReference);

                        EmitCall(functionContext, new FunctionSignature(targetMethod.ReturnType, targetMethod.ParameterTypes), targetMethod.GeneratedValue);

                        break;
                    }
                    case Code.Calli:
                    {
                        var callSite = (CallSite)instruction.Operand;

                        // TODO: Unify with CreateFunction code
                        var returnType = GetType(ResolveGenericsVisitor.Process(methodReference, callSite.ReturnType)).DefaultType;
                        var parameterTypesLLVM = callSite.Parameters.Select(x => GetType(ResolveGenericsVisitor.Process(methodReference, x.ParameterType)).DefaultType).ToArray();

                        // Generate function type
                        var functionType = LLVM.FunctionType(returnType, parameterTypesLLVM, false);

                        var methodPtr = stack[stack.Count - parameterTypesLLVM.Length - 1];

                        var castedMethodPtr = LLVM.BuildPointerCast(builder, methodPtr.Value, LLVM.PointerType(functionType, 0), string.Empty);

                        var signature = CreateFunctionSignature(methodReference, callSite);

                        EmitCall(functionContext, signature, castedMethodPtr);

                        break;
                    }
                    case Code.Callvirt:
                    {
                        var targetMethodReference = ResolveGenericsVisitor.Process(methodReference, (MethodReference)instruction.Operand);
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
                            var @class = GetClass(thisObject.Type);

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

                                var methodPointer = LLVM.BuildLoad(builder, imtEntry, string.Empty);

                                // TODO: Compare method ID and iterate in the linked list until the correct match is found
                                // If no match is found, it's likely due to covariance/contravariance, so we will need a fallback
                                var methodId = GetMethodId(targetMethod.MethodReference);

                                // Resolve interface call
                                var resolvedMethod = LLVM.BuildCall(builder, resolveInterfaceCallFunction, new[]
                                {
                                    LLVM.ConstInt(int32Type, methodId, false),
                                    methodPointer,
                                }, string.Empty);
                                resolvedMethod = LLVM.BuildPointerCast(builder, resolvedMethod, LLVM.PointerType(targetMethod.FunctionType, 0), string.Empty);

                                // Emit call
                                EmitCall(functionContext, targetMethod.Signature, resolvedMethod);
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
                                EmitCall(functionContext, targetMethod.Signature, resolvedMethod);
                            }
                        }
                        else
                        {
                            // Normal call
                            // Callvirt on non-virtual function is only done to force "this" NULL check
                            // However, that's probably a part of the .NET spec that we want to skip for performance reasons,
                            // so maybe we should keep this as is?
                            EmitCall(functionContext, targetMethod.Signature, targetMethod.GeneratedValue);
                        }

                        break;
                    }

                    #region Obj opcodes (Initobj, Newobj, Stobj, Ldobj, etc...)
                    case Code.Initobj:
                    {
                        var address = stack.Pop();
                        var typeReference = ResolveGenericsVisitor.Process(methodReference, (TypeReference)instruction.Operand);
                        var type = GetType(typeReference);
                        EmitInitobj(address, type);
                        break;
                    }

                    case Code.Newobj:
                    {
                        var ctorReference = ResolveGenericsVisitor.Process(methodReference, (MethodReference)instruction.Operand);
                        var ctor = GetFunction(ctorReference);
                        var type = GetType(ctorReference.DeclaringType);

                        EmitNewobj(functionContext, type, ctor);

                        break;
                    }

                    case Code.Stobj:
                    {
                        var type = GetType(ResolveGenericsVisitor.Process(methodReference, (TypeReference)instruction.Operand));

                        instructionFlags = EmitStobj(stack, type, instructionFlags);

                        break;
                    }
                    case Code.Ldobj:
                    {
                        var type = GetType(ResolveGenericsVisitor.Process(methodReference, (TypeReference)instruction.Operand));

                        instructionFlags = EmitLdobj(stack, type, instructionFlags);

                        break;
                    }
                    #endregion

                    case Code.Isinst:
                    {
                        var typeReference = ResolveGenericsVisitor.Process(methodReference, (TypeReference)instruction.Operand);
                        var @class = GetClass(typeReference);

                        var obj = stack.Pop();

                        // Get RTTI pointer
                        var indices = new[]
                        {
                            LLVM.ConstInt(int32Type, 0, false),                                 // Pointer indirection
                            LLVM.ConstInt(int32Type, (int)ObjectFields.RuntimeTypeInfo, false), // Access RTTI
                        };

                        var rttiPointer = LLVM.BuildInBoundsGEP(builder, obj.Value, indices, string.Empty);
                        rttiPointer = LLVM.BuildLoad(builder, rttiPointer, string.Empty);

                        // castedPointerObject is valid only from typeCheckBlock
                        var castedPointerType = LLVM.PointerType(@class.Type.ObjectType, 0);
                        ValueRef castedPointerObject;

                        // Prepare basic blocks (for PHI instruction)
                        var typeNotMatchBlock = LLVM.AppendBasicBlockInContext(context, functionGlobal, string.Empty);
                        var typeCheckDoneBlock = LLVM.AppendBasicBlockInContext(context, functionGlobal, string.Empty);
                        BasicBlockRef typeCheckBlock;

                        if (@class.Type.TypeReference.Resolve().IsInterface)
                        {
                            // Cast as appropriate pointer type (for next PHI incoming if success)
                            castedPointerObject = LLVM.BuildPointerCast(builder, obj.Value, castedPointerType, string.Empty);

                            var inlineRuntimeTypeInfoType = LLVM.TypeOf(LLVM.GetParam(isInstInterfaceFunction, 0));
                            var isInstInterfaceResult = LLVM.BuildCall(builder, isInstInterfaceFunction, new[]
                            {
                                LLVM.BuildPointerCast(builder, rttiPointer, inlineRuntimeTypeInfoType, string.Empty),
                                LLVM.BuildPointerCast(builder, @class.GeneratedRuntimeTypeInfoGlobal, inlineRuntimeTypeInfoType, string.Empty),
                            }, string.Empty);

                            LLVM.BuildCondBr(builder, isInstInterfaceResult, typeCheckDoneBlock, typeNotMatchBlock);

                            typeCheckBlock = LLVM.GetInsertBlock(builder);
                        }
                        else
                        {
                            // TODO: Probably better to rewrite this in C, but need to make sure depth will be inlined as constant
                            // Get super type count
                            // Get method stored in IMT slot
                            indices = new[]
                            {
                                LLVM.ConstInt(int32Type, 0, false),                                                 // Pointer indirection
                                LLVM.ConstInt(int32Type, (int)RuntimeTypeInfoFields.SuperTypeCount, false),         // Super type count
                            };
    
                            typeCheckBlock = LLVM.AppendBasicBlockInContext(context, functionGlobal, string.Empty);
    
                            var superTypeCount = LLVM.BuildInBoundsGEP(builder, rttiPointer, indices, string.Empty);
                            superTypeCount = LLVM.BuildLoad(builder, superTypeCount, string.Empty);
                            
                            var depthCompareResult = LLVM.BuildICmp(builder, IntPredicate.IntSGE, superTypeCount, LLVM.ConstInt(int32Type, (ulong)@class.Depth, false), string.Empty);
                            LLVM.BuildCondBr(builder, depthCompareResult, typeCheckBlock, typeNotMatchBlock);
    
                            // Start new typeCheckBlock
                            LLVM.PositionBuilderAtEnd(builder, typeCheckBlock);
    
                            // Get super types
                            indices = new[]
                            {
                                LLVM.ConstInt(int32Type, 0, false),                                                 // Pointer indirection
                                LLVM.ConstInt(int32Type, (int)RuntimeTypeInfoFields.SuperTypes, false),             // Super types
                            };
    
                            var superTypes = LLVM.BuildInBoundsGEP(builder, rttiPointer, indices, string.Empty);
                            superTypes = LLVM.BuildLoad(builder, superTypes, string.Empty);
    
                            // Get actual super type
                            indices = new[]
                            {
                                LLVM.ConstInt(int32Type, (ulong)@class.Depth, false),                                 // Pointer indirection
                            };
                            var superType = LLVM.BuildGEP(builder, superTypes, indices, string.Empty);
                            superType = LLVM.BuildLoad(builder, superType, string.Empty);

                            // Cast as appropriate pointer type (for next PHI incoming if success)
                            castedPointerObject = LLVM.BuildPointerCast(builder, obj.Value, castedPointerType, string.Empty);
    
                            // Compare super type in array at given depth with expected one
                            var typeCompareResult = LLVM.BuildICmp(builder, IntPredicate.IntEQ, superType, LLVM.ConstPointerCast(@class.GeneratedRuntimeTypeInfoGlobal, intPtrType), string.Empty);
                            LLVM.BuildCondBr(builder, typeCompareResult, typeCheckDoneBlock, typeNotMatchBlock);
                        }

                        // Start new typeNotMatchBlock: set object to null and jump to typeCheckDoneBlock
                        LLVM.PositionBuilderAtEnd(builder, typeNotMatchBlock);
                        LLVM.BuildBr(builder, typeCheckDoneBlock);

                        // Start new typeCheckDoneBlock
                        LLVM.PositionBuilderAtEnd(builder, typeCheckDoneBlock);

                        // Put back with appropriate type at end of stack
                        var mergedVariable = LLVM.BuildPhi(builder, castedPointerType, string.Empty);
                        LLVM.AddIncoming(mergedVariable, new[] { castedPointerObject, LLVM.ConstPointerNull(castedPointerType) }, new[] { typeCheckBlock, typeNotMatchBlock });
                        stack.Add(new StackValue(obj.StackType, @class.Type, mergedVariable));

                        break;
                    }

                    case Code.Ldftn:
                    {
                        var targetMethodReference = ResolveGenericsVisitor.Process(methodReference, (MethodReference)instruction.Operand);
                        var targetMethod = GetFunction(targetMethodReference);

                        stack.Add(new StackValue(StackValueType.NativeInt, intPtr, LLVM.BuildPointerCast(builder, targetMethod.GeneratedValue, intPtrType, string.Empty)));

                        break;
                    }

                    #region Box/Unbox opcodes
                    case Code.Box:
                    {
                        var typeReference = ResolveGenericsVisitor.Process(methodReference, (TypeReference)instruction.Operand);
                        var @class = GetClass(typeReference);

                        // Only value types need to be boxed
                        if (@class.Type.TypeReference.IsValueType)
                        {
                            var valueType = stack.Pop();

                            // Allocate object
                            var allocatedObject = AllocateObject(@class.Type);

                            var dataPointer = GetDataPointer(allocatedObject);

                            // Copy data
                            LLVM.BuildStore(builder, valueType.Value, dataPointer);

                            // Add created object on the stack
                            stack.Add(new StackValue(StackValueType.Object, @class.Type, allocatedObject));
                        }

                        break;
                    }

                    case Code.Unbox_Any:
                    {
                        var typeReference = ResolveGenericsVisitor.Process(methodReference, (TypeReference)instruction.Operand);
                        var @class = GetClass(typeReference);

                        var obj = stack.Pop();

                        if (typeReference.IsValueType)
                        {
                            // TODO: check type?
                            var objCast = LLVM.BuildPointerCast(builder, obj.Value, LLVM.PointerType(@class.Type.ObjectType, 0), string.Empty);

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
                        var elementType = GetType(ResolveGenericsVisitor.Process(methodReference, (TypeReference)instruction.Operand));

                        EmitNewarr(stack, elementType);
 
                        break;
                    }
                    case Code.Ldlen:
                    {
                        EmitLdlen(stack);

                        break;
                    }
                    case Code.Ldelema:
                    {
                        var type = GetType(ResolveGenericsVisitor.Process(methodReference, (TypeReference)instruction.Operand));

                        EmitLdelema(stack, type);

                        break;
                    }
                    case Code.Ldelem_I1:
                    case Code.Ldelem_I2:
                    case Code.Ldelem_I4:
                    case Code.Ldelem_I8:
                    case Code.Ldelem_U1:
                    case Code.Ldelem_U2:
                    case Code.Ldelem_U4:
                    case Code.Ldelem_R4:
                    case Code.Ldelem_R8:
                    case Code.Ldelem_Any:
                    case Code.Ldelem_Ref:
                    {
                        // TODO: Properly use opcode for type conversion
                        EmitLdelem(stack);

                        break;
                    }
                    case Code.Stelem_I1:
                    case Code.Stelem_I2:
                    case Code.Stelem_I4:
                    case Code.Stelem_I8:
                    case Code.Stelem_R4:
                    case Code.Stelem_R8:
                    case Code.Stelem_Any:
                    case Code.Stelem_Ref:
                    {
                        // TODO: Properly use opcode for type conversion
                        EmitStelem(stack);

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
                        var value = opcode - Code.Ldc_I4_0;
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
                    case Code.Ldc_R4:
                    {
                        var value = (float)instruction.Operand;
                        EmitR4(stack, value);
                        break;
                    }
                    case Code.Ldc_R8:
                    {
                        var value = (double)instruction.Operand;
                        EmitR8(stack, value);
                        break;
                    }
                    // Ldarg
                    case Code.Ldarg_0:
                    case Code.Ldarg_1:
                    case Code.Ldarg_2:
                    case Code.Ldarg_3:
                    {
                        var value = opcode - Code.Ldarg_0;
                        EmitLdarg(stack, args, value);
                        break;
                    }
                    case Code.Ldarg_S:
                    case Code.Ldarg:
                    {
                        var value = ((ParameterDefinition)instruction.Operand).Index + (method.HasThis ? 1 : 0);
                        EmitLdarg(stack, args, value);
                        break;
                    }
                    case Code.Ldstr:
                    {
                        var operand = (string)instruction.Operand;

                        EmitLdstr(stack, operand);

                        break;
                    }
                    case Code.Ldnull:
                    {
                        EmitLdnull(stack);
                        break;
                    }

                    // Ldloc
                    case Code.Ldloc_0:
                    case Code.Ldloc_1:
                    case Code.Ldloc_2:
                    case Code.Ldloc_3:
                    {
                        var localIndex = opcode - Code.Ldloc_0;
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
                        var @class = GetClass(ResolveGenericsVisitor.Process(methodReference, fieldReference.DeclaringType));
                        var field = @class.Fields[fieldReference.Resolve()];

                        EmitLdfld(stack, field, instructionFlags);
                        instructionFlags = InstructionFlags.None;

                        break;
                    }
                    case Code.Ldsfld:
                    {
                        var fieldReference = (FieldReference)instruction.Operand;

                        // Resolve class and field
                        var @class = GetClass(ResolveGenericsVisitor.Process(methodReference, fieldReference.DeclaringType));
                        var field = @class.Fields[fieldReference.Resolve()];

                        EmitLdsfld(stack, field, instructionFlags);
                        instructionFlags = InstructionFlags.None;

                        break;
                    }
                    #endregion

                    #region Indirect opcodes (Stind, Ldind, etc...)
                    case Code.Stind_I:
                    case Code.Stind_I1:
                    case Code.Stind_I2:
                    case Code.Stind_I4:
                    case Code.Stind_I8:
                    case Code.Stind_R4:
                    case Code.Stind_R8:
                    case Code.Stind_Ref:
                    {
                        var value = stack.Pop();
                        var address = stack.Pop();

                        // Determine type
                        Type type;
                        switch (opcode)
                        {
                            case Code.Stind_I: type = intPtr; break;
                            case Code.Stind_I1: type = int8; break;
                            case Code.Stind_I2: type = int16; break;
                            case Code.Stind_I4: type = int32; break;
                            case Code.Stind_I8: type = int64; break;
                            case Code.Stind_R4: type = @float; break;
                            case Code.Stind_R8: type = @double; break;
                            case Code.Stind_Ref:
                                type = value.Type;
                                break;
                            default:
                                throw new ArgumentException("opcode");
                        }

                        // Convert to local type
                        var sourceValue = ConvertFromStackToLocal(type, value);

                        // Store value at address
                        var pointerCast = LLVM.BuildPointerCast(builder, address.Value, LLVM.PointerType(type.TypeOnStack, 0), string.Empty);
                        var storeInst = LLVM.BuildStore(builder, sourceValue, pointerCast);
                        SetInstructionFlags(storeInst, instructionFlags);
                        instructionFlags = InstructionFlags.None;
                        
                        break;
                    }

                    case Code.Ldind_I:
                    case Code.Ldind_I1:
                    case Code.Ldind_I2:
                    case Code.Ldind_I4:
                    case Code.Ldind_I8:
                    case Code.Ldind_U1:
                    case Code.Ldind_U2:
                    case Code.Ldind_U4:
                    case Code.Ldind_R4:
                    case Code.Ldind_R8:
                    case Code.Ldind_Ref:
                    {
                        var address = stack.Pop();

                        // Determine type
                        Type type;
                        switch (opcode)
                        {
                            case Code.Ldind_I:      type = intPtr; break;
                            case Code.Ldind_I1:     type = int8; break;
                            case Code.Ldind_I2:     type = int16; break;
                            case Code.Ldind_I4:     type = int32; break;
                            case Code.Ldind_I8:     type = int64; break;
                            case Code.Ldind_U1:     type = int8; break;
                            case Code.Ldind_U2:     type = int16; break;
                            case Code.Ldind_U4:     type = int32; break;
                            case Code.Ldind_R4:     type = @float; break;
                            case Code.Ldind_R8:     type = @double; break;
                            case Code.Ldind_Ref:
                                type = GetType(((ByReferenceType)address.Type.TypeReference).ElementType);
                                break;
                            default:
                                throw new ArgumentException("opcode");
                        }

                        // Load value at address
                        var pointerCast = LLVM.BuildPointerCast(builder, address.Value, LLVM.PointerType(type.TypeOnStack, 0), string.Empty);
                        var loadInst = LLVM.BuildLoad(builder, pointerCast, string.Empty);
                        SetInstructionFlags(loadInst, instructionFlags);
                        instructionFlags = InstructionFlags.None;

                        // Convert to stack type
                        var value = ConvertFromLocalToStack(type, loadInst);

                        // Add to stack
                        stack.Add(new StackValue(type.StackType, type, value));
                        
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
                        var localIndex = opcode - Code.Stloc_0;
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
                        var @class = GetClass(ResolveGenericsVisitor.Process(methodReference, fieldReference.DeclaringType));
                        var field = @class.Fields[fieldReference.Resolve()];

                        EmitStfld(stack, field, instructionFlags);
                        instructionFlags = InstructionFlags.None;

                        break;
                    }

                    case Code.Stsfld:
                    {
                        var fieldReference = (FieldReference)instruction.Operand;

                        // Resolve class and field
                        var @class = GetClass(ResolveGenericsVisitor.Process(methodReference, fieldReference.DeclaringType));
                        var field = @class.Fields[fieldReference.Resolve()];

                        EmitStsfld(stack, field, instructionFlags);
                        instructionFlags = InstructionFlags.None;

                        break;
                    }
                    #endregion

                    #region Branching (Brtrue, Brfalse, etc...)
                    case Code.Br:
                    case Code.Br_S:
                    {
                        var targetInstruction = (Instruction)instruction.Operand;
                        EmitBr(basicBlocks[targetInstruction.Offset]);
                        flowingNextInstructionMode = FlowingNextInstructionMode.None;
                        break;
                    }
                    case Code.Brfalse:
                    case Code.Brfalse_S:
                    {
                        var targetInstruction = (Instruction)instruction.Operand;
                        EmitBrfalse(stack, basicBlocks[targetInstruction.Offset], basicBlocks[instruction.Next.Offset]);
                        flowingNextInstructionMode = FlowingNextInstructionMode.Explicit;
                        break;
                    }
                    case Code.Brtrue:
                    case Code.Brtrue_S:
                    {
                        var targetInstruction = (Instruction)instruction.Operand;
                        EmitBrtrue(stack, basicBlocks[targetInstruction.Offset], basicBlocks[instruction.Next.Offset]);
                        flowingNextInstructionMode = FlowingNextInstructionMode.Explicit;
                        break;
                    }
                    #endregion

                    #region Conditional branching (Beq, Bgt, etc...)
                    case Code.Beq:
                    case Code.Beq_S:
                    case Code.Bge:
                    case Code.Bge_S:
                    case Code.Bgt:
                    case Code.Bgt_S:
                    case Code.Ble:
                    case Code.Ble_S:
                    case Code.Blt:
                    case Code.Blt_S:
                    case Code.Bne_Un:
                    case Code.Bne_Un_S:
                    case Code.Bge_Un:
                    case Code.Bge_Un_S:
                    case Code.Bgt_Un:
                    case Code.Bgt_Un_S:
                    case Code.Ble_Un:
                    case Code.Ble_Un_S:
                    case Code.Blt_Un:
                    case Code.Blt_Un_S:
                    {
                        var targetInstruction = (Instruction)instruction.Operand;

                        var operand2 = stack.Pop();
                        var operand1 = stack.Pop();

                        var value1 = operand1.Value;
                        var value2 = operand2.Value;

                        if ((operand1.StackType == StackValueType.NativeInt && operand2.StackType != StackValueType.NativeInt)
                            || (operand1.StackType != StackValueType.NativeInt && operand2.StackType == StackValueType.NativeInt))
                            throw new NotImplementedException("Comparison between native int and int types.");

                        if (operand1.StackType != operand2.StackType
                            || operand1.Type != operand2.Type)
                            throw new InvalidOperationException("Comparison between operands of different types.");

                        ValueRef compareResult;
                        if (operand1.StackType == StackValueType.Float)
                        {
                            RealPredicate predicate;
                            switch (opcode)
                            {
                                case Code.Beq:
                                case Code.Beq_S:    predicate = RealPredicate.RealOEQ; break;
                                case Code.Bge:
                                case Code.Bge_S:    predicate = RealPredicate.RealOGE; break;
                                case Code.Bgt:
                                case Code.Bgt_S:    predicate = RealPredicate.RealOGT; break;
                                case Code.Ble:
                                case Code.Ble_S:    predicate = RealPredicate.RealOLE; break;
                                case Code.Blt:
                                case Code.Blt_S:    predicate = RealPredicate.RealOLT; break;
                                case Code.Bne_Un:
                                case Code.Bne_Un_S: predicate = RealPredicate.RealUNE; break;
                                case Code.Bge_Un:
                                case Code.Bge_Un_S: predicate = RealPredicate.RealUGE; break;
                                case Code.Bgt_Un:
                                case Code.Bgt_Un_S: predicate = RealPredicate.RealUGT; break;
                                case Code.Ble_Un:
                                case Code.Ble_Un_S: predicate = RealPredicate.RealULE; break;
                                case Code.Blt_Un:
                                case Code.Blt_Un_S: predicate = RealPredicate.RealULT; break;
                                default:
                                    throw new NotSupportedException();
                            }
                            compareResult = LLVM.BuildFCmp(builder, predicate, value1, value2, string.Empty);
                        }
                        else
                        {
                            IntPredicate predicate;
                            switch (opcode)
                            {
                                case Code.Beq:
                                case Code.Beq_S:    predicate = IntPredicate.IntEQ; break;
                                case Code.Bge:
                                case Code.Bge_S:    predicate = IntPredicate.IntSGE; break;
                                case Code.Bgt:
                                case Code.Bgt_S:    predicate = IntPredicate.IntSGT; break;
                                case Code.Ble:
                                case Code.Ble_S:    predicate = IntPredicate.IntSLE; break;
                                case Code.Blt:
                                case Code.Blt_S:    predicate = IntPredicate.IntSLT; break;
                                case Code.Bne_Un:
                                case Code.Bne_Un_S: predicate = IntPredicate.IntNE; break;
                                case Code.Bge_Un:
                                case Code.Bge_Un_S: predicate = IntPredicate.IntUGE; break;
                                case Code.Bgt_Un:
                                case Code.Bgt_Un_S: predicate = IntPredicate.IntUGT; break;
                                case Code.Ble_Un:
                                case Code.Ble_Un_S: predicate = IntPredicate.IntULE; break;
                                case Code.Blt_Un:
                                case Code.Blt_Un_S: predicate = IntPredicate.IntULT; break;
                                default:
                                    throw new NotSupportedException();
                            }
                            compareResult = LLVM.BuildICmp(builder, predicate, value1, value2, string.Empty);
                        }

                        // Branch depending on previous test
                        LLVM.BuildCondBr(builder, compareResult, basicBlocks[targetInstruction.Offset], basicBlocks[instruction.Next.Offset]);
                        
                        flowingNextInstructionMode = FlowingNextInstructionMode.Explicit;

                        break;
                    }
                    #endregion

                    #region Comparison opcodes (Ceq, Cgt, etc...)
                    case Code.Ceq:
                    case Code.Cgt:
                    case Code.Cgt_Un:
                    case Code.Clt:
                    case Code.Clt_Un:
                    {
                        var operand2 = stack.Pop();
                        var operand1 = stack.Pop();

                        var value1 = operand1.Value;
                        var value2 = operand2.Value;

                        // Downcast objects to typeof(object) so that they are comparables
                        if (operand1.StackType == StackValueType.Object)
                            value1 = ConvertFromStackToLocal(@object, operand1);
                        if (operand2.StackType == StackValueType.Object)
                            value2 = ConvertFromStackToLocal(@object, operand2);

                        if ((operand1.StackType == StackValueType.NativeInt && operand2.StackType != StackValueType.NativeInt)
                            || (operand1.StackType != StackValueType.NativeInt && operand2.StackType == StackValueType.NativeInt))
                            throw new NotImplementedException("Comparison between native int and int types.");

                        if (operand1.StackType != operand2.StackType
                            || LLVM.TypeOf(value1) != LLVM.TypeOf(value2))
                            throw new InvalidOperationException("Comparison between operands of different types.");

                        ValueRef compareResult;
                        if (operand1.StackType == StackValueType.Float)
                        {
                            RealPredicate predicate;
                            switch (opcode)
                            {
                                case Code.Ceq:      predicate = RealPredicate.RealOEQ; break;
                                case Code.Cgt:      predicate = RealPredicate.RealOGT; break;
                                case Code.Cgt_Un:   predicate = RealPredicate.RealUGT; break;
                                case Code.Clt:      predicate = RealPredicate.RealOLT; break;
                                case Code.Clt_Un:   predicate = RealPredicate.RealULT; break;
                                default:
                                    throw new NotSupportedException();
                            }
                            compareResult = LLVM.BuildFCmp(builder, predicate, value1, value2, string.Empty);
                        }
                        else
                        {
                            IntPredicate predicate;
                            switch (opcode)
                            {
                                case Code.Ceq:      predicate = IntPredicate.IntEQ; break;
                                case Code.Cgt:      predicate = IntPredicate.IntSGT; break;
                                case Code.Cgt_Un:   predicate = IntPredicate.IntUGT; break;
                                case Code.Clt:      predicate = IntPredicate.IntSLT; break;
                                case Code.Clt_Un:   predicate = IntPredicate.IntULT; break;
                                default:
                                    throw new NotSupportedException();
                            }
                            compareResult = LLVM.BuildICmp(builder, predicate, value1, value2, string.Empty);
                        }

                        // Extends to int32
                        compareResult = LLVM.BuildZExt(builder, compareResult, int32Type, string.Empty);

                        // Push result back on the stack
                        stack.Add(new StackValue(StackValueType.Int32, int32, compareResult));

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
                        switch (opcode)
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
                            if (opcode != Code.Conv_U8 && opcode != Code.Conv_U)
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
                        switch (opcode)
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

                    #region Unary operation opcodes (Neg, Not, etc...)
                    case Code.Neg:
                    case Code.Not:
                    {
                        var operand1 = stack.Pop();

                        var value1 = operand1.Value;

                        // Check stack type (and convert if necessary)
                        switch (operand1.StackType)
                        {
                            case StackValueType.Float:
                                if (opcode == Code.Not)
                                    throw new InvalidOperationException("Not opcode doesn't work with float");
                                break;
                            case StackValueType.NativeInt:
                                value1 = LLVM.BuildPtrToInt(builder, value1, nativeIntType, string.Empty);
                                break;
                            case StackValueType.Int32:
                            case StackValueType.Int64:
                                break;
                            default:
                                throw new InvalidOperationException(string.Format("Opcode {0} not supported with stack type {1}", opcode, operand1.StackType));
                        }

                        // Perform neg or not operation
                        switch (opcode)
                        {
                            case Code.Neg:
                                if (operand1.StackType == StackValueType.Float)
                                    value1 = LLVM.BuildFNeg(builder, value1, string.Empty);
                                else
                                    value1 = LLVM.BuildNeg(builder, value1, string.Empty);
                                break;
                            case Code.Not:
                                value1 = LLVM.BuildNot(builder, value1, string.Empty);
                                break;
                        }

                        if (operand1.StackType == StackValueType.NativeInt)
                            value1 = LLVM.BuildIntToPtr(builder, value1, intPtrType, string.Empty);

                        // Add back to stack (with same type as before)
                        stack.Add(new StackValue(operand1.StackType, operand1.Type, value1));

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
                        switch (opcode)
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
                                    if (opcode != Code.Sub && opcode != Code.Sub_Ovf_Un)
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
                        else if (!isIntegerOperation
                            && (operand1.StackType == StackValueType.Reference || operand2.StackType == StackValueType.Reference)) // ref + [i32, nativeint] or [i32, nativeint] + ref
                        {
                            StackValue operandRef, operandInt;
                            ValueRef valueRef, valueInt;

                            if (operand2.StackType == StackValueType.Reference)
                            {
                                operandRef = operand2;
                                operandInt = operand1;
                                valueRef = value2;
                                valueInt = value1;

                            }
                            else
                            {
                                operandRef = operand1;
                                operandInt = operand2;
                                valueRef = value1;
                                valueInt = value2;
                            }

                            switch (operandInt.StackType)
                            {
                                case StackValueType.Int32:
                                    break;
                                case StackValueType.NativeInt:
                                    valueInt = LLVM.BuildPtrToInt(builder, valueInt, nativeIntType, string.Empty);
                                    break;
                                default:
                                    goto InvalidBinaryOperation;
                            }

                            switch (opcode)
                            {
                                case Code.Add:
                                case Code.Add_Ovf_Un:
                                    break;
                                case Code.Sub:
                                case Code.Sub_Ovf:
                                    if (operand2.StackType == StackValueType.Reference)
                                        goto InvalidBinaryOperation;

                                    valueInt = LLVM.BuildNeg(builder, valueInt, string.Empty);
                                    break;
                                default:
                                    goto InvalidBinaryOperation;
                            }

                            // If necessary, cast to i8*
                            var valueRefType = LLVM.TypeOf(valueRef);
                            if (valueRefType != intPtrType)
                                valueRef = LLVM.BuildPointerCast(builder, valueRef, intPtrType, string.Empty);

                            valueRef = LLVM.BuildGEP(builder, valueRef, new[] { valueInt }, string.Empty);

                            // Cast back to original type
                            if (valueRefType != intPtrType)
                                valueRef = LLVM.BuildPointerCast(builder, valueRef, valueRefType, string.Empty);

                            stack.Add(new StackValue(StackValueType.Reference, operandRef.Type, valueRef));

                            // Early exit
                            break;
                        }
                        else
                        {
                            goto InvalidBinaryOperation;
                        }

                        ValueRef result;

                        // Perform binary operation
                        if (operand1.StackType == StackValueType.Float)
                        {
                            switch (opcode)
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
                            switch (opcode)
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
                            switch (opcode)
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
                        throw new InvalidOperationException(string.Format("Binary operation {0} between {1} and {2} is not supported.", opcode, operand1.StackType, operand2.StackType));
                    }
                    #endregion

                    #region Exception handling opcodes (Leave, Endfinally, etc...)
                    case Code.Throw:
                    {
                        var exceptionObject = stack.Pop();

                        GenerateInvoke(functionContext, throwExceptionFunction, new ValueRef[] { LLVM.BuildPointerCast(builder, exceptionObject.Value, LLVM.TypeOf(LLVM.GetParam(throwExceptionFunction, 0)), string.Empty) });
                        LLVM.BuildUnreachable(builder);

                        flowingNextInstructionMode = FlowingNextInstructionMode.None;
                        break;
                    }
                    case Code.Leave:
                    case Code.Leave_S:
                    {
                        // TODO: Exception handling. For now, fallback to Br.
                        goto case Code.Br;
                    }
                    case Code.Endfinally:
                    {
                        break;
                    }
                    #endregion

                    #region Instruction flags (Unaligned, Volatile)
                    case Code.Volatile:
                        instructionFlags |= InstructionFlags.Volatile;
                        break;
                    case Code.Unaligned:
                        instructionFlags |= InstructionFlags.Unaligned;
                        break;
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
                    MergeStack(stack, functionContext.BasicBlock, ref forwardStacks[target.Offset], basicBlocks[target.Offset]);
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
                if (LLVM.GetLastInstruction(targetBasicBlock).Value != IntPtr.Zero && sourceStack.Count != 0)
                    throw new InvalidOperationException("Target basic block should have no instruction yet, or stack should be empty.");
                LLVM.PositionBuilderAtEnd(builder2, targetBasicBlock);
            }

            for (int index = 0; index < sourceStack.Count; index++)
            {
                var stackValue = sourceStack[index];

                var mergedStackValue = targetStack[index];

                // First time? Need to create PHI node
                if (mergedStackValue == null)
                {
                    // TODO: Check stack type during merging?
                    mergedStackValue = new StackValue(stackValue.StackType, stackValue.Type, LLVM.BuildPhi(builder2, LLVM.TypeOf(stackValue.Value), string.Empty));
                    targetStack[index] = mergedStackValue;
                }

                // Add values from previous stack value
                LLVM.AddIncoming(mergedStackValue.Value, new[] { stackValue.Value }, new[] { sourceBasicBlock });
            }
        }
    }
}