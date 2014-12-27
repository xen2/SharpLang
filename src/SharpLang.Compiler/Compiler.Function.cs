using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
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
                var parameterType = GetType(parameterTypeReference, TypeState.StackComplete);
                if (parameterType.DefaultTypeLLVM.Value == IntPtr.Zero)
                    throw new InvalidOperationException();
                parameterTypes[index] = parameterType;
                parameterTypesLLVM[index] = parameterType.DefaultTypeLLVM;
            }

            var returnType = GetType(ResolveGenericsVisitor.Process(context, context.ReturnType), TypeState.StackComplete);

            return new FunctionSignature(returnType, parameterTypes, callSite.CallingConvention, null);
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

            var resolvedMethod = method.Resolve();
            var declaringType = GetType(ResolveGenericsVisitor.Process(method, method.DeclaringType), TypeState.Opaque);

            // Check if method is only defined in a parent class (can happen in some rare case, i.e. PCL TypeInfo.get_Assembly()).
            bool hasMatch = MetadataResolver.GetMethod(declaringType.TypeDefinitionCecil.Methods, method.GetElementMethod()) != null;
            if (resolvedMethod != null && !hasMatch)
            {
                var parentType = declaringType.TypeDefinitionCecil.BaseType != null ? ResolveGenericsVisitor.Process(declaringType.TypeReferenceCecil, declaringType.TypeDefinitionCecil.BaseType) : null;
                if (parentType == null)
                    throw new InvalidOperationException(string.Format("Could not find a matching method in any of the type or its parent for {0}", method));

                // Create function with parent type
                // TODO: Maybe we need to replace generic context with parent type?
                var parentMethod = method.ChangeDeclaringType(parentType);
                function = CreateFunction(parentMethod);

                // Register it so that it can be cached
                functions.Add(method, function);
                return function;
            }

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
                    parameterTypeReference = declaringType.TypeReferenceCecil;

                    // Value type uses ByReference type for this
                    if (declaringType.TypeDefinitionCecil.IsValueType)
                        parameterTypeReference = parameterTypeReference.MakeByReferenceType();
                }
                else
                {
                    var parameter = method.Parameters[method.HasThis ? index - 1 : index];
                    parameterTypeReference = ResolveGenericsVisitor.Process(method, parameter.ParameterType);
                }

                var parameterType = GetType(parameterTypeReference, TypeState.StackComplete);
                if (parameterType.DefaultTypeLLVM.Value == IntPtr.Zero)
                    throw new InvalidOperationException();

                parameterTypes[index] = parameterType;

                if (resolvedMethod != null && resolvedMethod.HasPInvokeInfo)
                {
                    if (parameterTypeReference.FullName == typeof(string).FullName)
                    {
                        //if (!resolvedMethod.PInvokeInfo.IsCharSetUnicode)
                        //    throw new NotImplementedException("Only Unicode string are supported in PInvoke");

                        parameterType = GetType(new PointerType(@char.TypeDefinitionCecil), TypeState.StackComplete);
                    }
                }

                parameterTypesLLVM[index] = parameterType.DefaultTypeLLVM;
            }

            var returnType = GetType(ResolveGenericsVisitor.Process(method, method.ReturnType), TypeState.StackComplete);
            var functionType = LLVM.FunctionType(returnType.DefaultTypeLLVM, parameterTypesLLVM, false);

            // If we have an external with generic parameters, let's try to do some generic sharing (we can write only one in C++)
            bool isInternal = resolvedMethod != null && ((resolvedMethod.ImplAttributes & MethodImplAttributes.InternalCall) != 0);
            if (isInternal && resolvedMethod.HasGenericParameters && resolvedMethod.GenericParameters.All(x => x.HasReferenceTypeConstraint))
            {
                // Check if this isn't the shareable method (in which case we should do normal processing)
                if (!((GenericInstanceMethod)method).GenericArguments.All(x => MemberEqualityComparer.Default.Equals(x, @object.TypeReferenceCecil)))
                {
                    // Let's share it with default method
                    var sharedGenericInstance = new GenericInstanceMethod(resolvedMethod);
                    foreach (var genericParameter in resolvedMethod.GenericParameters)
                    {
                        sharedGenericInstance.GenericArguments.Add(@object.TypeReferenceCecil);
                    }

                    var sharedMethod = GetFunction(sharedGenericInstance);

                    // Cast shared function to appropriate pointer type
                    var sharedFunctionGlobal = LLVM.ConstPointerCast(sharedMethod.GeneratedValue, LLVM.PointerType(functionType, 0));
                    function = new Function(declaringType, method, functionType, sharedFunctionGlobal, new FunctionSignature(returnType, parameterTypes, sharedMethod.Signature.CallingConvention, sharedMethod.Signature.PInvokeInfo));
                    functions.Add(method, function);

                    return function;
                }
            }

            // Determine if type and function is local, and linkage type
            bool isLocal;
            var linkageType = GetLinkageType(method.DeclaringType, out isLocal);
            if (isInternal)
            {
                // Should be switched to non-weak when we have complete implementation of every internal calls
                linkageType = Linkage.ExternalWeakLinkage;
            }
            else if (resolvedMethod != null && resolvedMethod.HasGenericParameters)
            {
                isLocal = true;
                linkageType = Linkage.LinkOnceAnyLinkage;
            }

            bool isRuntime = resolvedMethod != null && ((resolvedMethod.ImplAttributes & MethodImplAttributes.Runtime) != 0);
            bool isInterfaceMethod = declaringType.TypeDefinitionCecil.IsInterface;
            var hasDefinition = resolvedMethod != null && (resolvedMethod.HasBody || isInternal || isRuntime);

            var methodMangledName = Regex.Replace(method.MangledName(), @"(\W)", "_");
            var functionGlobal = hasDefinition
                ? LLVM.AddFunction(module, methodMangledName, functionType)
                : LLVM.ConstPointerNull(LLVM.PointerType(functionType, 0));

            // Interface method uses a global so that we can have a unique pointer to use as IMT key
            if (isInterfaceMethod)
            {
                // For test code only: Use linkonce instead of linkageType so that we know if type was forced
                if (TestMode)
                {
                    isLocal = true;
                    linkageType = Linkage.LinkOnceAnyLinkage;
                }

                functionGlobal = LLVM.AddGlobal(module, LLVM.Int8TypeInContext(context), methodMangledName);
                if (isLocal)
                    LLVM.SetInitializer(functionGlobal, LLVM.ConstNull(LLVM.Int8TypeInContext(context)));
                LLVM.SetLinkage(functionGlobal, linkageType);
            }

            // Find calling convention
            var callingConvention = method.CallingConvention;
            PInvokeInfo pinvokeInfo = null;
            if (resolvedMethod != null && resolvedMethod.HasPInvokeInfo)
            {
                pinvokeInfo = resolvedMethod.PInvokeInfo;
                if (resolvedMethod.PInvokeInfo.IsCallConvStdCall || resolvedMethod.PInvokeInfo.IsCallConvWinapi)
                    callingConvention = MethodCallingConvention.StdCall;
                else if (resolvedMethod.PInvokeInfo.IsCallConvFastcall)
                    callingConvention = MethodCallingConvention.FastCall;
            }

            function = new Function(declaringType, method, functionType, functionGlobal, new FunctionSignature(returnType, parameterTypes, callingConvention, pinvokeInfo));
            functions.Add(method, function);

            if (hasDefinition)
            {
                switch (callingConvention)
                {
                    case MethodCallingConvention.StdCall:
                        LLVM.SetFunctionCallConv(functionGlobal, (uint)CallConv.X86StdcallCallConv);
                        break;
                    case MethodCallingConvention.FastCall:
                        LLVM.SetFunctionCallConv(functionGlobal, (uint)CallConv.X86FastcallCallConv);
                        break;
                }

                if (isLocal && !isInternal)
                {
                    // Need to compile
                    EmitFunction(function);
                }

                // Apply linkage
                LLVM.SetLinkage(functionGlobal, linkageType);
            }

            return function;
        }

        private void EmitFunction(Function function)
        {
            if (function.IsLocal)
                return;

            function.IsLocal = true;
            LLVM.SetLinkage(function.GeneratedValue, Linkage.ExternalLinkage);
            methodsToCompile.Enqueue(new KeyValuePair<MethodReference, Function>(function.MethodReference, function));
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

            if (methodReference.DeclaringType.Name == "EncodingHelper" && methodReference.Name == "LoadGetStringPlatform")
            {
                body = new MethodBody(method);
                body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
                body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            }

            if ((method.ImplAttributes & MethodImplAttributes.Runtime) != 0)
            {
                var declaringClass = GetClass(function.DeclaringType);

                // Generate IL for various methods
                if (declaringClass.BaseType != null &&
                    declaringClass.BaseType.Type.TypeReferenceCecil.FullName == typeof(MulticastDelegate).FullName)
                {
                    body = GenerateDelegateMethod(method, declaringClass);
                    if (body == null)
                        return;

                    codeSize = body.UpdateInstructionOffsets();
                }

                // Reposition builder at end, in case it was used
                LLVM.PositionBuilderAtEnd(builder, functionContext.BasicBlock);
            }

            if (body == null)
                return;

            functionContext.Body = body;

            var numParams = method.Parameters.Count;

            // Create stack, locals and args
            var stack = new List<StackValue>(body.MaxStackSize);
            var locals = new List<StackValue>(body.Variables.Count);
            var args = new List<StackValue>(numParams);
            var exceptionHandlers = new List<ExceptionHandlerInfo>();
            var activeTryHandlers = new List<ExceptionHandlerInfo>();

            functionContext.Stack = stack;
            functionContext.Locals = locals;
            functionContext.Arguments = args;
            functionContext.Scopes = new List<Scope>();
            functionContext.ExceptionHandlers = exceptionHandlers;
            functionContext.ActiveTryHandlers = activeTryHandlers;

            // Process locals
            foreach (var local in body.Variables)
            {
                // TODO: Anything to do on pinned objects?
                //if (local.IsPinned)
                //    throw new NotSupportedException();

                var type = GetType(ResolveGenericsVisitor.Process(methodReference, local.VariableType), TypeState.StackComplete);
                locals.Add(new StackValue(type.StackType, type, LLVM.BuildAlloca(builder, type.DefaultTypeLLVM, local.Name)));

                // Force value types to be emitted right away
                if (type.TypeDefinitionCecil.IsValueType)
                    GetClass(type);
            }

            // Process args
            for (int index = 0; index < function.ParameterTypes.Length; index++)
            {
                var argType = function.ParameterTypes[index];
                var arg = LLVM.GetParam(functionGlobal, (uint)index);

                // Force value types to be emitted right away
                if (argType.TypeDefinitionCecil.IsValueType)
                    GetClass(argType);

                var parameterIndex = index - (functionContext.Method.HasThis ? 1 : 0);
                var parameterName = parameterIndex == -1 ? "this" : method.Parameters[parameterIndex].Name;

                // Copy argument on stack
                var storage = LLVM.BuildAlloca(builder, argType.DefaultTypeLLVM, parameterName);
                LLVM.BuildStore(builder, arg, storage);

                args.Add(new StackValue(argType.StackType, argType, storage));
            }

            // Some wasted space due to unused offsets, but we only keep one so it should be fine.
            // TODO: Reuse same allocated instance per thread, and grow it only if necessary
            var branchTargets = new bool[codeSize];
            var basicBlocks = new BasicBlockRef[codeSize];
            var forwardStacks = new StackValue[codeSize][];

            functionContext.BasicBlocks = basicBlocks;
            functionContext.ForwardStacks = forwardStacks;

            // Find branch targets (which will require PHI node for stack merging)
            for (int index = 0; index < body.Instructions.Count; index++)
            {
                var instruction = body.Instructions[index];

                var flowControl = instruction.OpCode.FlowControl;

                // Process branch targets
                if (flowControl == FlowControl.Cond_Branch
                    || flowControl == FlowControl.Branch)
                {
                    var targets = instruction.Operand is Instruction[] ? (Instruction[])instruction.Operand : new[] { (Instruction)instruction.Operand };

                    foreach (var target in targets)
                    {
                        // Operand Target can be reached
                        branchTargets[target.Offset] = true;
                    }
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
                functionContext.ExceptionHandlerSelectorSlot = LLVM.BuildAlloca(builder, int32LLVM, "ehselector.slot");
                functionContext.ExceptionSlot = LLVM.BuildAlloca(builder, @object.DefaultTypeLLVM, "exn.slot");
                functionContext.EndfinallyJumpTarget = LLVM.BuildAlloca(builder, int32LLVM, "endfinally.jumptarget");

                // Create resume exception block
                functionContext.ResumeExceptionBlock = LLVM.AppendBasicBlockInContext(context, functionGlobal, "eh.resume");
                LLVM.PositionBuilderAtEnd(builder2, functionContext.ResumeExceptionBlock);
                var exceptionObject = LLVM.BuildLoad(builder2, functionContext.ExceptionSlot, "exn");
                var ehselectorValue = LLVM.BuildLoad(builder2, functionContext.ExceptionHandlerSelectorSlot, "sel");

                exceptionObject = LLVM.BuildPointerCast(builder2, exceptionObject, intPtrLLVM, "exn");
                var landingPadValue = LLVM.BuildInsertValue(builder2, LLVM.GetUndef(caughtResultLLVM), exceptionObject, 0, "lpad.val");
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
                // Exception catch handlers blocks are also branch targets
                foreach (var exceptionHandler in body.ExceptionHandlers)
                {
                    exceptionHandlers.Add(new ExceptionHandlerInfo(exceptionHandler));

                    if (exceptionHandler.HandlerType != ExceptionHandlerType.Catch)
                        continue;

                    var handlerStart = exceptionHandler.HandlerStart.Offset;

                    var catchBlock = basicBlocks[handlerStart];
                    var catchClass = GetClass(ResolveGenericsVisitor.Process(methodReference, exceptionHandler.CatchType));

                    // Extract exception
                    LLVM.PositionBuilderAtEnd(builder2, catchBlock);
                    var exceptionObject = LLVM.BuildLoad(builder2, functionContext.ExceptionSlot, string.Empty);
                    exceptionObject = LLVM.BuildPointerCast(builder2, exceptionObject, catchClass.Type.DefaultTypeLLVM, string.Empty);

                    // Erase exception from exn.slot (it has been handled)
                    LLVM.BuildStore(builder2, LLVM.ConstNull(@object.DefaultTypeLLVM), functionContext.ExceptionSlot);
                    LLVM.BuildStore(builder2, LLVM.ConstInt(int32LLVM, 0, false), functionContext.ExceptionHandlerSelectorSlot);

                    forwardStacks[handlerStart] = new[]
                    {
                        new StackValue(catchClass.Type.StackType, catchClass.Type, exceptionObject)
                    };
                }
            }

            PrepareScopes(functionContext, function);

            foreach (var instruction in body.Instructions)
            {
                try
                {
                    // Check if any exception handlers might have changed
                    if (body.HasExceptionHandlers)
                        UpdateExceptionHandlers(functionContext, instruction);

                    if (branchTargets[instruction.Offset])
                        UpdateBranching(functionContext, instruction);

                    ProcessScopes(functionContext, instruction);

                    // Reset states
                    functionContext.FlowingNextInstructionMode = FlowingNextInstructionMode.Implicit;

                    EmitInstruction(functionContext, instruction);

                    // If we do a jump, let's merge stack
                    var flowControl = instruction.OpCode.FlowControl;
                    if (flowControl == FlowControl.Cond_Branch
                        || flowControl == FlowControl.Branch)
                        MergeStacks(functionContext, instruction);
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException(string.Format("Error while processing instruction {0} of method {1}", instruction, methodReference), e);
                }
            }

            if (body.HasExceptionHandlers)
            {
                // Place eh.resume block at the very end
                LLVM.MoveBasicBlockAfter(functionContext.ResumeExceptionBlock, LLVM.GetLastBasicBlock(functionGlobal));
            }

            if (LLVM.VerifyFunction(functionGlobal, VerifierFailureAction.PrintMessageAction))
            { 
                throw new InvalidOperationException(string.Format("Verification failed for function {0}", function.MethodReference));
            }
        }

        /// <summary>
        /// Update branching before emitting instruction.
        /// </summary>
        /// <param name="functionContext"></param>
        /// <param name="instruction"></param>
        private void UpdateBranching(FunctionCompilerContext functionContext, Instruction instruction)
        {
            var previousBasicBlock = functionContext.BasicBlock;
            var stack = functionContext.Stack;
            var basicBlocks = functionContext.BasicBlocks;
            var forwardStacks = functionContext.ForwardStacks;

            functionContext.BasicBlock = basicBlocks[instruction.Offset];

            var forwardStack = forwardStacks[instruction.Offset];

            if (functionContext.FlowingNextInstructionMode == FlowingNextInstructionMode.Implicit)
            {
                // Add a jump from previous block to new block
                LLVM.BuildBr(builder, functionContext.BasicBlock);
            }

            if (functionContext.FlowingNextInstructionMode != FlowingNextInstructionMode.None)
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

        private void UpdateExceptionHandlers(FunctionCompilerContext functionContext, Instruction instruction)
        {
            var functionGlobal = functionContext.FunctionGlobal;
            var exceptionHandlers = functionContext.ExceptionHandlers;
            var activeTryHandlers = functionContext.ActiveTryHandlers;
            var methodReference = functionContext.MethodReference;
            bool exceptionHandlersChanged = false;

            // Exit finished exception handlers
            for (int index = activeTryHandlers.Count - 1; index >= 0; index--)
            {
                var exceptionHandler = activeTryHandlers[index];
                if (instruction == exceptionHandler.Source.TryEnd)
                {
                    activeTryHandlers.RemoveAt(index);
                    exceptionHandlersChanged = true;
                }
                else
                    break;
            }

            // Add new exception handlers
            for (int index = exceptionHandlers.Count - 1; index >= 0; index--)
            {
                var exceptionHandler = exceptionHandlers[index];
                if (instruction == exceptionHandler.Source.TryStart)
                {
                    var catchDispatchBlock = new BasicBlockRef();

                    if (exceptionHandler.Source.HandlerType == ExceptionHandlerType.Catch
                        || exceptionHandler.Source.HandlerType == ExceptionHandlerType.Fault)
                    {
                        catchDispatchBlock = LLVM.AppendBasicBlockInContext(context, functionGlobal, "catch.dispatch");
                        LLVM.PositionBuilderAtEnd(builder2, catchDispatchBlock);

                        var catchBlock = functionContext.BasicBlocks[exceptionHandler.Source.HandlerStart.Offset];
                        if (exceptionHandler.Source.HandlerType == ExceptionHandlerType.Catch)
                        {
                            var catchClass = GetClass(ResolveGenericsVisitor.Process(methodReference, exceptionHandler.Source.CatchType));

                            // Compare exception type
                            var ehselectorValue = LLVM.BuildLoad(builder2, functionContext.ExceptionHandlerSelectorSlot, "sel");

                            var ehtypeIdFor = LLVM.IntrinsicGetDeclaration(module, (uint)Intrinsics.eh_typeid_for, new TypeRef[0]);
                            var ehtypeid = LLVM.BuildCall(builder2, ehtypeIdFor, new[] {LLVM.ConstBitCast(catchClass.GeneratedEETypeRuntimeLLVM, intPtrLLVM)}, string.Empty);

                            // Jump to catch clause if type matches.
                            // Otherwise, go to next exception handler dispatch block (if any), or resume exception block (TODO)
                            var ehtypeComparisonResult = LLVM.BuildICmp(builder2, IntPredicate.IntEQ, ehselectorValue, ehtypeid, string.Empty);

                            LLVM.BuildCondBr(builder2, ehtypeComparisonResult, catchBlock, activeTryHandlers.Count > 0 ? activeTryHandlers.Last().CatchDispatch : functionContext.ResumeExceptionBlock);
                        }
                        else
                        {
                            // Fault: Jump without checking type
                            LLVM.BuildBr(builder2, catchBlock);
                        }

                        // Move this catch dispatch block just before its actual catch block
                        LLVM.MoveBasicBlockBefore(catchDispatchBlock, catchBlock);
                    }
                    else if (exceptionHandler.Source.HandlerType == ExceptionHandlerType.Finally)
                    {
                        catchDispatchBlock = functionContext.BasicBlocks[exceptionHandler.Source.HandlerStart.Offset];
                    }

                    exceptionHandler.CatchDispatch = catchDispatchBlock;
                    activeTryHandlers.Add(exceptionHandler);
                    exceptionHandlersChanged = true;
                }
            }

            if (exceptionHandlersChanged)
            {
                // Need to generate a new landing pad
                for (int index = activeTryHandlers.Count - 1; index >= 0; index--)
                {
                    var exceptionHandler = activeTryHandlers[index];
                    switch (exceptionHandler.Source.HandlerType)
                    {
                        case ExceptionHandlerType.Catch:
                        case ExceptionHandlerType.Fault:
                            break;
                    }
                }

                if (activeTryHandlers.Count > 0)
                {
                    //var handlerStart = exceptionHandlers.Last().HandlerStart.Offset;
                    //functionContext.LandingPadBlock = basicBlocks[handlerStart];

                    // Prepare landing pad block
                    functionContext.LandingPadBlock = LLVM.AppendBasicBlockInContext(context, functionGlobal, "landingpad");
                    LLVM.PositionBuilderAtEnd(builder2, functionContext.LandingPadBlock);
                    var landingPad = LLVM.BuildLandingPad(builder2, caughtResultLLVM, sharpPersonalityFunctionLLVM, 1, string.Empty);

                    // Extract exception, and store it in exn.slot
                    var exceptionObject = LLVM.BuildExtractValue(builder2, landingPad, 0, string.Empty);
                    exceptionObject = LLVM.BuildPointerCast(builder2, exceptionObject, @object.Class.Type.DefaultTypeLLVM, string.Empty);
                    LLVM.BuildStore(builder2, exceptionObject, functionContext.ExceptionSlot);

                    // Extract selector slot, and store it in ehselector.slot
                    var exceptionType = LLVM.BuildExtractValue(builder2, landingPad, 1, string.Empty);
                    LLVM.BuildStore(builder2, exceptionType, functionContext.ExceptionHandlerSelectorSlot);

                    // Let future finally clause know that we need to propage exception after they are executed
                    // A future Leave instruction should clear that if necessary
                    LLVM.BuildStore(builder2, LLVM.ConstInt(int32LLVM, unchecked((ulong)-1), true), functionContext.EndfinallyJumpTarget);

                    // Add jump to catch dispatch block or finally block
                    var lastActiveTryHandler = activeTryHandlers.Last();
                    LLVM.BuildBr(builder2, lastActiveTryHandler.CatchDispatch);

                    // Move landingpad block just before its catch.dispatch block
                    LLVM.MoveBasicBlockBefore(functionContext.LandingPadBlock, lastActiveTryHandler.CatchDispatch);

                    // Filter exceptions type by type
                    for (int index = activeTryHandlers.Count - 1; index >= 0; index--)
                    {
                        var exceptionHandler = activeTryHandlers[index];

                        // Add landing pad type clause
                        if (exceptionHandler.Source.HandlerType == ExceptionHandlerType.Catch)
                        {
                            var catchClass = GetClass(ResolveGenericsVisitor.Process(methodReference, exceptionHandler.Source.CatchType));
                            LLVM.AddClause(landingPad, LLVM.ConstBitCast(catchClass.GeneratedEETypeRuntimeLLVM, intPtrLLVM));
                        }
                        else if (exceptionHandler.Source.HandlerType == ExceptionHandlerType.Finally
                            || exceptionHandler.Source.HandlerType == ExceptionHandlerType.Fault)
                        {
                            LLVM.SetCleanup(landingPad, true);
                        }
                    }
                }
                else
                {
                    functionContext.LandingPadBlock = new BasicBlockRef();
                }
            }
        }

        private void EmitInstruction(FunctionCompilerContext functionContext, Instruction instruction)
        {
            var methodReference = functionContext.MethodReference;
            var stack = functionContext.Stack;
            var args = functionContext.Arguments;
            var locals = functionContext.Locals;
            var exceptionHandlers = functionContext.ExceptionHandlers;

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
                    functionContext.FlowingNextInstructionMode = FlowingNextInstructionMode.None;
                    break;
                }
                case Code.Call:
                {
                    var targetMethodReference = ResolveGenericsVisitor.Process(methodReference, (MethodReference)instruction.Operand);
                    var targetMethod = GetFunction(targetMethodReference);

                    // If calling a static method, make sure .cctor has been called
                    if (!targetMethodReference.HasThis)
                        EnsureClassInitialized(functionContext, GetClass(targetMethod.DeclaringType));

                    var overrideMethod = targetMethod.GeneratedValue;

                    // PInvoke: go through ResolveVirtualMethod
                    var resolvedMethod = targetMethodReference.Resolve();
                    if (resolvedMethod != null && resolvedMethod.HasPInvokeInfo)
                    {
                        StackValue thisObject = null;
                        overrideMethod = ResolveVirtualMethod(functionContext, ref targetMethod, ref thisObject);
                    }

                    EmitCall(functionContext, targetMethod.Signature, overrideMethod);

                    break;
                }
                case Code.Calli:
                {
                    var callSite = (CallSite)instruction.Operand;

                    // TODO: Unify with CreateFunction code
                    var returnType = GetType(ResolveGenericsVisitor.Process(methodReference, callSite.ReturnType), TypeState.StackComplete).DefaultTypeLLVM;
                    var parameterTypesLLVM = callSite.Parameters.Select(x => GetType(ResolveGenericsVisitor.Process(methodReference, x.ParameterType), TypeState.StackComplete).DefaultTypeLLVM).ToArray();

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

                    var thisObject = stack[stack.Count - targetMethod.ParameterTypes.Length];

                    var resolvedMethod = ResolveVirtualMethod(functionContext, ref targetMethod, ref thisObject);

                    stack[stack.Count - targetMethod.ParameterTypes.Length] = thisObject;

                    // Emit call
                    EmitCall(functionContext, targetMethod.Signature, resolvedMethod);

                    break;
                }
                case Code.Constrained:
                {
                    var typeReference = ResolveGenericsVisitor.Process(methodReference, (TypeReference)instruction.Operand);
                    functionContext.ConstrainedClass = GetClass(typeReference);

                    break;
                }
                case Code.Readonly:
                {
                    break;
                }

                #region Obj opcodes (Initobj, Newobj, Stobj, Ldobj, etc...)
                case Code.Initobj:
                {
                    var address = stack.Pop();
                    var typeReference = ResolveGenericsVisitor.Process(methodReference, (TypeReference)instruction.Operand);
                    var type = GetType(typeReference, TypeState.StackComplete);
                    EmitInitobj(address, type);
                    break;
                }

                case Code.Newobj:
                {
                    var ctorReference = ResolveGenericsVisitor.Process(methodReference, (MethodReference)instruction.Operand);
                    var ctor = GetFunction(ctorReference);
                    var type = GetType(ctorReference.DeclaringType, TypeState.TypeComplete);

                    EmitNewobj(functionContext, type, ctor);

                    break;
                }

                case Code.Stobj:
                {
                    var type = GetType(ResolveGenericsVisitor.Process(methodReference, (TypeReference)instruction.Operand), TypeState.StackComplete);

                    EmitStobj(stack, type, functionContext.InstructionFlags);
                    functionContext.InstructionFlags = InstructionFlags.None;

                    break;
                }
                case Code.Ldobj:
                {
                    var type = GetType(ResolveGenericsVisitor.Process(methodReference, (TypeReference)instruction.Operand), TypeState.StackComplete);

                    EmitLdobj(stack, type, functionContext.InstructionFlags);
                    functionContext.InstructionFlags = InstructionFlags.None;

                    break;
                }
                #endregion

                case Code.Sizeof:
                {
                    var typeReference = ResolveGenericsVisitor.Process(methodReference, (TypeReference)instruction.Operand);
                    var type = GetType(typeReference, TypeState.StackComplete);

                    // Use type because @class might be null (i.e. void*)
                    var objectSize = LLVM.SizeOf(type.DefaultTypeLLVM);
                    objectSize = LLVM.BuildIntCast(builder, objectSize, int32LLVM, string.Empty);

                    stack.Add(new StackValue(StackValueType.Int32, int32.Class.Type, objectSize));

                    break;
                }

                case Code.Localloc:
                {
                    EmitLocalloc(stack);
                    break;
                }

                case Code.Castclass:
                case Code.Isinst:
                {
                    var typeReference = ResolveGenericsVisitor.Process(methodReference, (TypeReference)instruction.Operand);
                    var @class = GetClass(typeReference);

                    EmitIsOrCastclass(functionContext, stack, @class, opcode, instruction.Offset);

                    break;
                }

                case Code.Ldtoken:
                {
                    var token = instruction.Operand;

                    Class runtimeHandleClass;
                    var runtimeHandleValue = ValueRef.Empty;

                    if (token is TypeReference)
                    {
                        var type = GetType(ResolveGenericsVisitor.Process(methodReference, (TypeReference)token), TypeState.VTableEmitted);

                        runtimeHandleClass = GetClass(corlib.MainModule.GetType(typeof(RuntimeTypeHandle).FullName));

                        if (type != null && type.Class != null)
                        {
                            runtimeHandleValue = LLVM.ConstNull(runtimeHandleClass.Type.DataTypeLLVM);
                            runtimeHandleValue = LLVM.BuildInsertValue(builder, runtimeHandleValue, LLVM.ConstPointerCast(type.Class.GeneratedEETypeTokenLLVM, intPtrLLVM), 0, string.Empty);
                        }
                        else
                        {
                            // TODO: Support generic open types and special types such as void
                            // We should issue a warning
                        }
                    }
                    else if (token is FieldReference)
                    {
                        runtimeHandleClass = GetClass(corlib.MainModule.GetType(typeof(RuntimeFieldHandle).FullName));

                        var fieldReference = (FieldReference)token;
                        var fieldDefinition = fieldReference.Resolve();

                        // HACK: Temporary hack so that RuntimeHelpers.InitialiseArray can properly initialize array constants from static struct with initial data.
                        // For now we just pass the field address (instead of a real RuntimeFieldHandle.
                        if (fieldDefinition.IsStatic && (fieldDefinition.Attributes & FieldAttributes.HasFieldRVA) != 0)
                        {
                            // Resolve class and field
                            var @class = GetClass(ResolveGenericsVisitor.Process(methodReference, fieldReference.DeclaringType));
                            var field = @class.StaticFields[fieldDefinition];

                            EmitLdsflda(stack, field);
                            var fieldAddress = stack.Pop().Value;
                            fieldAddress = LLVM.BuildPointerCast(builder, fieldAddress, intPtrLLVM, string.Empty);
                            runtimeHandleValue = LLVM.ConstNull(runtimeHandleClass.Type.DataTypeLLVM);
                            runtimeHandleValue = LLVM.BuildInsertValue(builder, runtimeHandleValue, fieldAddress, 0, string.Empty);
                        }
                    }
                    else if (token is MethodReference)
                    {
                        runtimeHandleClass = GetClass(corlib.MainModule.GetType(typeof(RuntimeMethodHandle).FullName));
                    }
                    else
                    {
                        throw new NotSupportedException("Invalid ldtoken operand.");
                    }

                    // Setup default value
                    if (runtimeHandleValue == ValueRef.Empty)
                        runtimeHandleValue = LLVM.ConstNull(runtimeHandleClass.Type.DataTypeLLVM);

                    // TODO: Actually transform type to RTTI token.
                    stack.Add(new StackValue(StackValueType.Value, runtimeHandleClass.Type, runtimeHandleValue));

                    break;
                }

                case Code.Ldftn:
                {
                    var targetMethodReference = ResolveGenericsVisitor.Process(methodReference, (MethodReference)instruction.Operand);
                    var targetMethod = GetFunction(targetMethodReference);

                    stack.Add(new StackValue(StackValueType.NativeInt, intPtr, LLVM.BuildPointerCast(builder, targetMethod.GeneratedValue, intPtrLLVM, string.Empty)));

                    break;
                }
                case Code.Ldvirtftn:
                {
                    var targetMethodReference = ResolveGenericsVisitor.Process(methodReference, (MethodReference)instruction.Operand);
                    var targetMethod = GetFunction(targetMethodReference);

                    var thisObject = stack.Pop();

                    var resolvedMethod = ResolveVirtualMethod(functionContext, ref targetMethod, ref thisObject);

                    stack.Add(new StackValue(StackValueType.NativeInt, intPtr, LLVM.BuildPointerCast(builder, resolvedMethod, intPtrLLVM, string.Empty)));

                    break;
                }

                #region Box/Unbox opcodes
                case Code.Box:
                {
                    var typeReference = ResolveGenericsVisitor.Process(methodReference, (TypeReference)instruction.Operand);
                    var type = GetType(typeReference, TypeState.VTableEmitted);

                    // Only value types need to be boxed
                    if (type.TypeDefinitionCecil.IsValueType)
                    {
                        EmitBoxValueType(stack, type);
                    }

                    break;
                }

                case Code.Unbox_Any:
                {
                    var typeReference = ResolveGenericsVisitor.Process(methodReference, (TypeReference)instruction.Operand);
                    var type = GetType(typeReference, TypeState.VTableEmitted);

                    if (type.TypeDefinitionCecil.IsValueType)
                    {
                        EmitUnboxAnyValueType(stack, type);
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
                    var elementType = GetType(ResolveGenericsVisitor.Process(methodReference, (TypeReference)instruction.Operand), TypeState.StackComplete);

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
                    var type = GetType(ResolveGenericsVisitor.Process(methodReference, (TypeReference)instruction.Operand), TypeState.Opaque);

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

                #region Argument opcodes (Ldarg, Ldarga, Starg, etc...)
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
                    var value = ((ParameterDefinition)instruction.Operand).Index + (functionContext.Method.HasThis ? 1 : 0);
                    EmitLdarg(stack, args, value);
                    break;
                }
                case Code.Ldarga:
                case Code.Ldarga_S:
                {
                    var value = ((ParameterDefinition)instruction.Operand).Index + (functionContext.Method.HasThis ? 1 : 0);
                    EmitLdarga(stack, args, value);
                    break;
                }
                case Code.Starg:
                case Code.Starg_S:
                {
                    var value = ((ParameterDefinition)instruction.Operand).Index + (functionContext.Method.HasThis ? 1 : 0);
                    EmitStarg(stack, args, value);
                    break;
                }
                case Code.Arglist:
                {
                    // TODO: Implement this opcode
                    //var value = LLVM.BuildVAArg(builder, , , string.Empty);
                    var runtimeHandleType = GetType(corlib.MainModule.GetType(typeof(RuntimeArgumentHandle).FullName), TypeState.StackComplete);
                    stack.Add(new StackValue(StackValueType.Value, runtimeHandleType, LLVM.ConstNull(runtimeHandleType.DataTypeLLVM)));
                    break;
                }
                #endregion

                #region Load opcodes (Ldc, Ldstr, Ldloc, etc...)
                // Ldc_I4
                case Code.Ldc_I4_M1:
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
                case Code.Ldflda:
                {
                    var fieldReference = (FieldReference)instruction.Operand;

                    // Resolve type and field
                    var type = GetType(ResolveGenericsVisitor.Process(methodReference, fieldReference.DeclaringType), TypeState.TypeComplete);
                    var field = type.Fields[fieldReference.Resolve()];

                    if (opcode == Code.Ldflda)
                    {
                        EmitLdflda(stack, field);
                    }
                    else
                    {
                        EmitLdfld(stack, field, functionContext.InstructionFlags);
                        functionContext.InstructionFlags = InstructionFlags.None;
                    }

                    break;
                }
                case Code.Ldsfld:
                case Code.Ldsflda:
                {
                    var fieldReference = (FieldReference)instruction.Operand;

                    // Resolve class and field
                    var @class = GetClass(ResolveGenericsVisitor.Process(methodReference, fieldReference.DeclaringType));
                    var field = @class.StaticFields[fieldReference.Resolve()];

                    EnsureClassInitialized(functionContext, @class);

                    if (opcode == Code.Ldsflda)
                    {
                        EmitLdsflda(stack, field);
                    }
                    else
                    {
                        EmitLdsfld(stack, field, functionContext.InstructionFlags);
                        functionContext.InstructionFlags = InstructionFlags.None;
                    }

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
                    EmitStind(functionContext, stack, opcode);

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
                    EmitLdind(functionContext, stack, opcode);

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

                    // Resolve type and field
                    var type = GetType(ResolveGenericsVisitor.Process(methodReference, fieldReference.DeclaringType), TypeState.TypeComplete);
                    var field = type.Fields[fieldReference.Resolve()];

                    EmitStfld(stack, field, functionContext.InstructionFlags);
                    functionContext.InstructionFlags = InstructionFlags.None;

                    break;
                }

                case Code.Stsfld:
                {
                    var fieldReference = (FieldReference)instruction.Operand;

                    // Resolve class and field
                    var @class = GetClass(ResolveGenericsVisitor.Process(methodReference, fieldReference.DeclaringType));
                    var field = @class.StaticFields[fieldReference.Resolve()];

                    EnsureClassInitialized(functionContext, @class);

                    EmitStsfld(stack, field, functionContext.InstructionFlags);
                    functionContext.InstructionFlags = InstructionFlags.None;

                    break;
                }
                #endregion

                #region Branching (Brtrue, Brfalse, Switch, etc...)
                case Code.Br:
                case Code.Br_S:
                {
                    var targetInstruction = (Instruction)instruction.Operand;
                    EmitBr(functionContext.BasicBlocks[targetInstruction.Offset]);
                    functionContext.FlowingNextInstructionMode = FlowingNextInstructionMode.None;
                    break;
                }
                case Code.Brfalse:
                case Code.Brfalse_S:
                {
                    var targetInstruction = (Instruction)instruction.Operand;
                    EmitBrfalse(stack, functionContext.BasicBlocks[targetInstruction.Offset], functionContext.BasicBlocks[instruction.Next.Offset]);
                    functionContext.FlowingNextInstructionMode = FlowingNextInstructionMode.Explicit;
                    break;
                }
                case Code.Brtrue:
                case Code.Brtrue_S:
                {
                    var targetInstruction = (Instruction)instruction.Operand;
                    EmitBrtrue(stack, functionContext.BasicBlocks[targetInstruction.Offset], functionContext.BasicBlocks[instruction.Next.Offset]);
                    functionContext.FlowingNextInstructionMode = FlowingNextInstructionMode.Explicit;
                    break;
                }
                case Code.Switch:
                {
                    var targets = (Instruction[])instruction.Operand;
                    var operand = stack.Pop();
                    var @switch = LLVM.BuildSwitch(builder, operand.Value, functionContext.BasicBlocks[instruction.Next.Offset], (uint)targets.Length);
                    for (int i = 0; i < targets.Length; ++i)
                    {
                        var target = targets[i];
                        LLVM.AddCase(@switch, LLVM.ConstInt(int32LLVM, (ulong)i, false), functionContext.BasicBlocks[target.Offset]);
                    }
                    functionContext.FlowingNextInstructionMode = FlowingNextInstructionMode.Explicit;
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

                    EmitConditionalBranch(stack, functionContext.BasicBlocks[targetInstruction.Offset], functionContext.BasicBlocks[instruction.Next.Offset], opcode);
                    functionContext.FlowingNextInstructionMode = FlowingNextInstructionMode.Explicit;

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
                    EmitComparison(stack, opcode);

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
                case Code.Conv_R4:
                case Code.Conv_R8:
                case Code.Conv_R_Un:
                case Code.Conv_Ovf_U:
                case Code.Conv_Ovf_I:
                case Code.Conv_Ovf_U1:
                case Code.Conv_Ovf_I1:
                case Code.Conv_Ovf_U2:
                case Code.Conv_Ovf_I2:
                case Code.Conv_Ovf_U4:
                case Code.Conv_Ovf_I4:
                case Code.Conv_Ovf_U8:
                case Code.Conv_Ovf_I8:
                case Code.Conv_Ovf_U_Un:
                case Code.Conv_Ovf_I_Un:
                case Code.Conv_Ovf_U1_Un:
                case Code.Conv_Ovf_I1_Un:
                case Code.Conv_Ovf_U2_Un:
                case Code.Conv_Ovf_I2_Un:
                case Code.Conv_Ovf_U4_Un:
                case Code.Conv_Ovf_I4_Un:
                case Code.Conv_Ovf_U8_Un:
                case Code.Conv_Ovf_I8_Un:
                {
                    EmitConv(stack, opcode);

                    break;
                }
                #endregion

                #region Unary operation opcodes (Neg, Not, etc...)
                case Code.Neg:
                case Code.Not:
                {
                    EmitUnaryOperation(stack, opcode);

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
                    EmitBinaryOperation(functionContext, stack, opcode);
                    break;
                }
                #endregion

                #region Exception handling opcodes (Leave, Endfinally, etc...)
                case Code.Throw:
                {
                    var exceptionObject = stack.Pop();

                    // Throw exception
                    // TODO: Update callstack
                    GenerateInvoke(functionContext, throwExceptionFunctionLLVM, new ValueRef[] { LLVM.BuildPointerCast(builder, exceptionObject.Value, LLVM.TypeOf(LLVM.GetParam(throwExceptionFunctionLLVM, 0)), string.Empty) });
                    LLVM.BuildUnreachable(builder);

                    functionContext.FlowingNextInstructionMode = FlowingNextInstructionMode.None;
                    break;
                }
                case Code.Rethrow:
                {
                    // Find exception that was on stack at beginning of this catch clause
                    var currentCatchClause = GetCurrentExceptionHandler(exceptionHandlers, instruction.Offset);

                    if (currentCatchClause == null || currentCatchClause.Source.HandlerType != ExceptionHandlerType.Catch)
                        throw new InvalidOperationException("Can't find catch clause matching this rethrow instruction.");

                    var catchClauseStack = functionContext.ForwardStacks[currentCatchClause.Source.HandlerStart.Offset];
                    var exceptionObject = catchClauseStack[0];

                    // Rethrow exception
                    GenerateInvoke(functionContext, throwExceptionFunctionLLVM, new ValueRef[] { LLVM.BuildPointerCast(builder, exceptionObject.Value, LLVM.TypeOf(LLVM.GetParam(throwExceptionFunctionLLVM, 0)), string.Empty) });
                    LLVM.BuildUnreachable(builder);

                    functionContext.FlowingNextInstructionMode = FlowingNextInstructionMode.None;
                    break;
                }
                case Code.Leave:
                case Code.Leave_S:
                {
                    // Evaluation stack is cleared
                    stack.Clear();

                    // Default target (if we jump inside the exception clause)
                    var targetInstruction = (Instruction)instruction.Operand;

                    GenerateLeave(functionContext.ActiveTryHandlers, targetInstruction, functionContext.EndfinallyJumpTarget, functionContext.BasicBlocks);
                    functionContext.FlowingNextInstructionMode = FlowingNextInstructionMode.None;

                    break;
                }
                case Code.Endfinally:
                {
                    var currentFinallyClause = GetCurrentExceptionHandler(exceptionHandlers, instruction.Offset);

                    if (currentFinallyClause == null)
                        throw new InvalidOperationException("Can't find exception clause matching this endfinally/endfault instruction.");

                    EmitEndfinally(functionContext, currentFinallyClause);

                    break;
                }
                #endregion

                #region Instruction flags (Unaligned, Volatile)
                case Code.Volatile:
                    functionContext.InstructionFlags |= InstructionFlags.Volatile;
                    break;
                case Code.Unaligned:
                    functionContext.InstructionFlags |= InstructionFlags.Unaligned;
                    break;
                #endregion

                default:
                    throw new NotImplementedException(string.Format("Opcode {0} not implemented.", instruction.OpCode));
            }
        }

        private void EnsureClassInitialized(FunctionCompilerContext functionContext, Class @class)
        {
            // TODO: Add thread protection (with lock and actual class initialization in a separate method to improve code reuse)
            // If there was a type initializer, let's call it.
            // Even through the type initializer itself will check if type initialization is necessary inside a lock,
            // we do a quick check before here already (early exit).
            //  if (!classInitialized)
            //  {
            //      EnsureClassInitialized();
            //  }
            // with:
            //  void EnsureClassInitialized()
            //  {
            //      lock (initMutex)
            //      {
            //          if (!classInitialized)
            //          {
            //              InitializeClass();
            //              classInitialized = true;
            //          }
            //      }
            //  }
            if (@class.InitializeType != ValueRef.Empty)
            {
                var functionGlobal = functionContext.FunctionGlobal;

                // TODO: We temporarily ignore extern class in test mode
                // Not sure if that's the way we want to keep it
                if (TestMode && !@class.Type.IsLocal)
                    return;

                // Check if class is initialized
                var indices = new[]
                {
                    LLVM.ConstInt(int32LLVM, 0, false),                                                 // Pointer indirection
                    LLVM.ConstInt(int32LLVM, (int)RuntimeTypeInfoFields.TypeInitialized, false),        // Type initialized flag
                };

                var classInitializedAddress = LLVM.BuildInBoundsGEP(builder, @class.GeneratedEETypeRuntimeLLVM, indices, string.Empty);
                var classInitialized = LLVM.BuildLoad(builder, classInitializedAddress, string.Empty);
                classInitialized = LLVM.BuildIntCast(builder, classInitialized, LLVM.Int1TypeInContext(context), string.Empty);

                var typeNeedInitBlock = LLVM.AppendBasicBlockInContext(context, functionGlobal, string.Empty);
                var nextBlock = LLVM.AppendBasicBlockInContext(context, functionGlobal, string.Empty);

                LLVM.MoveBasicBlockAfter(typeNeedInitBlock, LLVM.GetInsertBlock(builder));
                LLVM.MoveBasicBlockAfter(nextBlock, typeNeedInitBlock);

                LLVM.BuildCondBr(builder, classInitialized, nextBlock, typeNeedInitBlock);

                // Initialize class (first time)
                LLVM.PositionBuilderAtEnd(builder, typeNeedInitBlock);
                LLVM.BuildCall(builder, @class.InitializeType, new ValueRef[0], string.Empty);

                // Set flag so that it won't be initialized again
                // Note: Inner function already does this, so commented out for now.
                // However, enabling it here might help LLVM performs additional optimization if happens multiple time in same function? (vs heavier code? need to test in practice)
                //LLVM.BuildStore(builder, LLVM.ConstInt(LLVM.Int1TypeInContext(context), 1, false), classInitializedAddress);
                LLVM.BuildBr(builder, nextBlock);

                // Normal path
                LLVM.PositionBuilderAtEnd(builder, nextBlock);
                functionContext.BasicBlock = nextBlock;
            }
        }

        private ValueRef ResolveVirtualMethod(FunctionCompilerContext functionContext, ref Function targetMethod, ref StackValue thisObject)
        {
            // Get constrained class
            var constrainedClass = functionContext.ConstrainedClass;
            if (constrainedClass != null)
                functionContext.ConstrainedClass = null;

            var method = targetMethod.MethodReference.Resolve();

            ValueRef resolvedMethod;
            if ((method.Attributes & MethodAttributes.Virtual) == MethodAttributes.Virtual)
            {
                // Build indices for GEP
                var indices = new[]
                {
                    LLVM.ConstInt(int32LLVM, 0, false), // Pointer indirection
                    LLVM.ConstInt(int32LLVM, (int) ObjectFields.RuntimeTypeInfo, false), // Access RTTI
                };

                Class @class;

                // Process constrained class
                if (constrainedClass != null)
                {
                    if (!constrainedClass.Type.TypeDefinitionCecil.IsValueType)
                    {
                        // If thisType is a reference type, dereference
                        thisObject = new StackValue(constrainedClass.Type.StackType, constrainedClass.Type,
                            LLVM.BuildPointerCast(builder, LLVM.BuildLoad(builder, thisObject.Value, string.Empty),
                                constrainedClass.Type.DefaultTypeLLVM, string.Empty));
                    }
                    else
                    {
                        var matchingMethod = CecilExtensions.TryMatchMethod(constrainedClass, targetMethod.MethodReference,
                            false);
                        if (matchingMethod != null)
                        {
                            // If thisType is a value type and implements method, then ptr is passed unmodified
                            targetMethod = matchingMethod;

                            // Convert to appropriate type (if necessary)
                            var refType = GetType(constrainedClass.Type.TypeReferenceCecil.MakeByReferenceType(), TypeState.Opaque);
                            if (thisObject.StackType != StackValueType.Reference || thisObject.Type != refType)
                            {
                                thisObject = new StackValue(refType.StackType, refType,
                                    LLVM.BuildPointerCast(builder, thisObject.Value, refType.DefaultTypeLLVM, string.Empty));
                            }
                        }
                        else
                        {
                            // If thisType is a value type and doesn't implement method, dereference, box and pass as this
                            thisObject = new StackValue(constrainedClass.Type.StackType, constrainedClass.Type,
                                LLVM.BuildPointerCast(builder, LLVM.BuildLoad(builder, thisObject.Value, string.Empty),
                                    constrainedClass.Type.DefaultTypeLLVM, string.Empty));

                            thisObject = new StackValue(StackValueType.Object, constrainedClass.Type,
                                BoxValueType(constrainedClass.Type, thisObject));
                        }
                    }

                    @class = constrainedClass;
                }
                else
                {
                    @class = GetClass(thisObject.Type);
                }

                // TODO: Checking actual type stored in thisObject we might be able to statically resolve method?

                // If it's a byref value type, emit a normal call
                if (thisObject.Type.TypeReferenceCecil.IsByReference
                    && GetType(((ByReferenceType)thisObject.Type.TypeReferenceCecil).ElementType, TypeState.Opaque).TypeDefinitionCecil.IsValueType
                    && MemberEqualityComparer.Default.Equals(targetMethod.DeclaringType.TypeReferenceCecil, ((ByReferenceType)thisObject.Type.TypeReferenceCecil).ElementType))
                {
                    resolvedMethod = targetMethod.GeneratedValue;
                }
                else
                {
                    // Get RTTI pointer
                    var rttiPointer = LLVM.BuildInBoundsGEP(builder, thisObject.Value, indices, string.Empty);
                    rttiPointer = LLVM.BuildLoad(builder, rttiPointer, string.Empty);

                    if (targetMethod.MethodReference.DeclaringType.Resolve().IsInterface)
                    {
                        // Interface call

                        // Cast to object type (enough to have IMT)
                        rttiPointer = LLVM.BuildPointerCast(builder, rttiPointer, LLVM.TypeOf(GetClass(@object).GeneratedEETypeRuntimeLLVM), string.Empty);

                        // Get method stored in IMT slot
                        indices = new[]
                        {
                            LLVM.ConstInt(int32LLVM, 0, false), // Pointer indirection
                            LLVM.ConstInt(int32LLVM, (int) RuntimeTypeInfoFields.InterfaceMethodTable, false), // Access IMT
                            LLVM.ConstInt(int32LLVM, (ulong) targetMethod.VirtualSlot, false), // Access specific IMT slot
                        };

                        var imtEntry = LLVM.BuildInBoundsGEP(builder, rttiPointer, indices, string.Empty);

                        var methodPointer = LLVM.BuildLoad(builder, imtEntry, string.Empty);

                        // Resolve interface call
                        // TODO: Improve resolveInterfaceCall(): if no match is found, it's likely due to covariance/contravariance, so we will need a fallback
                        resolvedMethod = LLVM.BuildCall(builder, resolveInterfaceCallFunctionLLVM, new[]
                        {
                            //LLVM.ConstInt(int32LLVM, methodId, false),
                            LLVM.ConstPointerCast(targetMethod.GeneratedValue, intPtrLLVM),
                            methodPointer,
                        }, string.Empty);
                        resolvedMethod = LLVM.BuildPointerCast(builder, resolvedMethod,
                            LLVM.PointerType(targetMethod.FunctionType, 0), string.Empty);
                    }
                    else
                    {
                        // Cast to expected RTTI type
                        rttiPointer = LLVM.BuildPointerCast(builder, rttiPointer, LLVM.TypeOf(@class.GeneratedEETypeRuntimeLLVM), string.Empty);

                        // Virtual table call
                        if (targetMethod.VirtualSlot == -1)
                        {
                            throw new InvalidOperationException("Trying to call a virtual method without slot.");
                        }

                        // Get method stored in vtable slot
                        indices = new[]
                        {
                            LLVM.ConstInt(int32LLVM, 0, false), // Pointer indirection
                            LLVM.ConstInt(int32LLVM, (int) RuntimeTypeInfoFields.VirtualTable, false), // Access vtable
                            LLVM.ConstInt(int32LLVM, (ulong) targetMethod.VirtualSlot, false), // Access specific vtable slot
                        };

                        var vtable = LLVM.BuildInBoundsGEP(builder, rttiPointer, indices, string.Empty);
                        resolvedMethod = LLVM.BuildLoad(builder, vtable, string.Empty);
                        resolvedMethod = LLVM.BuildPointerCast(builder, resolvedMethod, LLVM.PointerType(targetMethod.FunctionType, 0), string.Empty);
                    }
                }
            }
            else if (method.HasPInvokeInfo)
            {
                // PInvoke behaves almost like a virtual call, but directly use given class vtable
                var @class = GetClass(targetMethod.DeclaringType);

                // Get method stored in vtable slot
                var indices = new[]
                {
                    LLVM.ConstInt(int32LLVM, 0, false), // Pointer indirection
                    LLVM.ConstInt(int32LLVM, (int) RuntimeTypeInfoFields.VirtualTable, false), // Access vtable
                    LLVM.ConstInt(int32LLVM, (ulong) targetMethod.VirtualSlot, false), // Access specific vtable slot
                };

                var vtable = LLVM.BuildInBoundsGEP(builder, @class.GeneratedEETypeRuntimeLLVM, indices, string.Empty);
                resolvedMethod = LLVM.BuildLoad(builder, vtable, string.Empty);
                resolvedMethod = LLVM.BuildPointerCast(builder, resolvedMethod, LLVM.PointerType(targetMethod.FunctionType, 0), string.Empty);
            }
            else
            {
                // Normal call
                // Callvirt on non-virtual function is only done to force "this" NULL check
                // However, that's probably a part of the .NET spec that we want to skip for performance reasons,
                // so maybe we should keep this as is?
                resolvedMethod = targetMethod.GeneratedValue;
            }
            return resolvedMethod;
        }

        private static ExceptionHandlerInfo GetCurrentExceptionHandler(List<ExceptionHandlerInfo> exceptionHandlers, int offset)
        {
            ExceptionHandlerInfo currentExceptionHandler = null;
            for (int index = 0; index < exceptionHandlers.Count; ++index)
            {
                var exceptionHandler = exceptionHandlers[index];

                if (offset >= exceptionHandler.Source.HandlerStart.Offset
                    && (exceptionHandler.Source.HandlerEnd == null || offset < exceptionHandler.Source.HandlerEnd.Offset))
                {
                    currentExceptionHandler = exceptionHandler;
                    break;
                }
            }
            return currentExceptionHandler;
        }

        private ValueRef BoxValueType(Type type, StackValue valueType)
        {
            // Allocate object
            var allocatedObject = AllocateObject(type, StackValueType.Object);

            var dataPointer = GetDataPointer(allocatedObject);

            // Convert to local type
            var value = ConvertFromStackToLocal(type, valueType);

            // Copy data
            var expectedPointerType = LLVM.PointerType(LLVM.TypeOf(value), 0);
            if (expectedPointerType != LLVM.TypeOf(dataPointer))
                dataPointer = LLVM.BuildPointerCast(builder, dataPointer, expectedPointerType, string.Empty);
            LLVM.BuildStore(builder, value, dataPointer);
            return allocatedObject;
        }

        private void GenerateLeave(List<ExceptionHandlerInfo> activeTryHandlers, Instruction targetInstruction, ValueRef endfinallyJumpTarget, BasicBlockRef[] basicBlocks)
        {
            // Check if we need to go through a finally handler
            for (int index = activeTryHandlers.Count - 1; index >= 0; index--)
            {
                var exceptionHandler = activeTryHandlers[index];

                // Jumping inside that exception clause?
                if (targetInstruction.Offset >= exceptionHandler.Source.TryStart.Offset
                    && targetInstruction.Offset < exceptionHandler.Source.TryEnd.Offset)
                    break;

                // Leaving through a finally clause
                if (exceptionHandler.Source.HandlerType == ExceptionHandlerType.Finally)
                {
                    // Find or insert index of this instruction
                    var leaveTargetIndex = exceptionHandler.LeaveTargets.IndexOf(targetInstruction);
                    if (leaveTargetIndex == -1)
                    {
                        leaveTargetIndex = exceptionHandler.LeaveTargets.Count;
                        exceptionHandler.LeaveTargets.Add(targetInstruction);
                    }

                    // Update desired jump target
                    LLVM.BuildStore(builder, LLVM.ConstInt(int32LLVM, (ulong)leaveTargetIndex, false), endfinallyJumpTarget);

                    // Actual jump will be to finally clause
                    targetInstruction = exceptionHandler.Source.HandlerStart;
                    break;
                }
            }

            EmitBr(basicBlocks[targetInstruction.Offset]);
        }

        private ValueRef GetDataPointer(ValueRef obj)
        {
            // Get data pointer
            var indices = new[]
            {
                LLVM.ConstInt(int32LLVM, 0, false),                         // Pointer indirection
                LLVM.ConstInt(int32LLVM, (int)ObjectFields.Data, false),    // Data
            };

            var dataPointer = LLVM.BuildInBoundsGEP(builder, obj, indices, string.Empty);
            return dataPointer;
        }

        /// <summary>
        /// Merges all the stacks of this instruction targets.
        /// </summary>
        /// <param name="functionContext">The function context.</param>
        /// <param name="instruction">The instruction.</param>
        /// <exception cref="System.InvalidOperationException">Backward jump with a non-empty stack unknown target.</exception>
        private void MergeStacks(FunctionCompilerContext functionContext, Instruction instruction)
        {
            var forwardStacks = functionContext.ForwardStacks;
            var targets = instruction.Operand is Instruction[] ? (Instruction[])instruction.Operand : new[] { (Instruction)instruction.Operand };

            foreach (var target in targets)
            {
                // Backward jump? Make sure stack was properly created by a previous forward jump, or empty
                if (target.Offset < instruction.Offset)
                {
                    var forwardStack = forwardStacks[target.Offset];
                    if (forwardStack != null && forwardStack.Length > 0)
                        throw new InvalidOperationException("Backward jump with a non-empty stack unknown target.");
                }

                // Merge stack (add PHI incoming)
                MergeStack(functionContext.Stack, functionContext.BasicBlock, ref forwardStacks[target.Offset], functionContext.BasicBlocks[target.Offset]);
            }
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
            }

            for (int index = 0; index < sourceStack.Count; index++)
            {
                var stackValue = sourceStack[index];

                var mergedStackValue = targetStack[index];

                // First time? Need to create PHI node
                if (mergedStackValue == null)
                {
                    // TODO: Check stack type during merging?
                    LLVM.PositionBuilderAtEnd(builder2, targetBasicBlock);
                    mergedStackValue = new StackValue(stackValue.StackType, stackValue.Type, LLVM.BuildPhi(builder2, LLVM.TypeOf(stackValue.Value), string.Empty));
                    targetStack[index] = mergedStackValue;
                }

                // Convert type (if necessary)
                // TOOD: Reuse common code with cast/conversion code
                var value = stackValue.Value;
                if (LLVM.TypeOf(value) != LLVM.TypeOf(mergedStackValue.Value) && LLVM.GetTypeKind(LLVM.TypeOf(mergedStackValue.Value)) == TypeKind.PointerTypeKind)
                {
                    // Position before last instruction (which should be a branching instruction)
                    LLVM.PositionBuilderBefore(builder2, LLVM.GetLastInstruction(sourceBasicBlock));
                    value = LLVM.BuildPointerCast(builder2, value, LLVM.TypeOf(mergedStackValue.Value), string.Empty);
                }

                // Add values from previous stack value
                LLVM.AddIncoming(mergedStackValue.Value, new[] { value }, new[] { sourceBasicBlock });
            }
        }
    }
}