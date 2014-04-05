using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
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
            if (!functions.TryGetValue(method, out function))
                throw new InvalidOperationException(string.Format("Could not find method {0}", method));

            return function;
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
            var parameterTypes = new Type[numParams];
            var parameterTypesLLVM = new TypeRef[numParams];
            for (int index = 0; index < numParams; index++)
            {
                var parameter = method.Parameters[index];
                var parameterType = CreateType(parameter.ParameterType);
                if (parameterType.GeneratedType.Value == IntPtr.Zero)
                    throw new InvalidOperationException();
                parameterTypes[index] = parameterType;
                parameterTypesLLVM[index] = parameterType.GeneratedType;
            }

            var returnType = CreateType(method.ReturnType);

            // Generate function global
            var methodDefinition = method.Resolve();
            bool isExternal = methodDefinition.Module.Assembly != assembly;
            var methodMangledName = Regex.Replace(method.FullName, @"(\W)", "_");
            var functionType = LLVM.FunctionType(returnType.GeneratedType, parameterTypesLLVM, false);
            var functionGlobal = LLVM.AddFunction(module, methodMangledName, functionType);

            function = new Function(method, functionGlobal, returnType, parameterTypes);
            functions.Add(method, function);

            if (isExternal)
            {
                // External weak linkage
                LLVM.SetLinkage(functionGlobal, Linkage.ExternalWeakLinkage);
            }
            else
            {
                // Need to compile
                LLVM.SetLinkage(functionGlobal, Linkage.ExternalLinkage);
                methodsToCompile.Add(new KeyValuePair<MethodDefinition, Function>(methodDefinition, function));
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
        private void CompileFunction(MethodDefinition method, Function function)
        {
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
                locals.Add(new StackValue(type.StackType, type, LLVM.BuildAlloca(builder, type.GeneratedType, local.Name)));
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
                        EmitRet(method);
                        flowingToNextBlock = false;
                        break;
                    }
                    case Code.Call:
                    {
                        var targetMethodReference = (MethodReference)instruction.Operand;
                        var targetMethod = GetFunction(targetMethodReference);

                        EmitCall(stack, targetMethodReference, targetMethod);

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
                    case Code.Ldc_I4:
                    {
                        var value = ((VariableDefinition)instruction.Operand).Index;
                        EmitI4(stack, value);
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