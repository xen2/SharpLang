using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    /// <summary>
    /// Provides access to C# LLVM code generator.
    /// </summary>
    public sealed partial class Compiler
    {
        private ModuleRef module;
        private ContextRef context;

        // Builder for normal instructions
        private BuilderRef builder;

        // Builder that is used for PHI nodes
        private BuilderRef builderPhi;

        private IAssemblyResolver assemblyResolver;
        private AssemblyDefinition assembly;
        private AssemblyDefinition corlib;

        private Dictionary<TypeReference, Type> types = new Dictionary<TypeReference, Type>();
        private Dictionary<TypeDefinition, Class> classes = new Dictionary<TypeDefinition, Class>();
        private Dictionary<MethodReference, Function> functions = new Dictionary<MethodReference, Function>();

        private List<KeyValuePair<MethodDefinition, Function>> methodsToCompile = new List<KeyValuePair<MethodDefinition, Function>>();

        public ModuleRef CompileAssembly(IAssemblyResolver assemblyResolver, AssemblyDefinition assembly)
        {
            this.assemblyResolver = assemblyResolver;
            this.assembly = assembly;
            corlib = assembly.MainModule.Import(typeof (void)).Resolve().Module.Assembly;
            module = LLVM.ModuleCreateWithName(assembly.Name.Name);
            context = LLVM.GetModuleContext(module);
            builder = LLVM.CreateBuilderInContext(context);
            builderPhi = LLVM.CreateBuilderInContext(context);

            // Process types
            foreach (var assemblyModule in assembly.Modules)
            {
                foreach (var type in assemblyModule.GetTypeReferences())
                {
                    CompileType(type);
                }

                foreach (var member in assemblyModule.GetMemberReferences())
                {
                    var method = member as MethodReference;
                    CompileType(member.DeclaringType);
                    if (method != null)
                    {
                        CompileFunction(method);
                    }
                }

                foreach (var type in assemblyModule.Types)
                {
                    CompileType(type);
                }
            }

            // Process methods
            foreach (var @class in classes)
            {
                CompileClassMethods(@class.Value);
            }

            // Generate code
            foreach (var methodToCompile in methodsToCompile)
            {
                CompileMethodImpl(methodToCompile.Key, methodToCompile.Value);
            }

            // Emit "main" which will call the assembly entry point (if any)
            Function entryPoint;
	        if (assembly.EntryPoint != null && functions.TryGetValue(assembly.EntryPoint, out entryPoint))
	        {
	            var mainFunctionType = LLVM.FunctionType(LLVM.Int32TypeInContext(context), new TypeRef[0], false);
	            var mainFunction = LLVM.AddFunction(module, "main", mainFunctionType);
                LLVM.SetLinkage(mainFunction, Linkage.ExternalLinkage);
                LLVM.PositionBuilderAtEnd(builder, LLVM.AppendBasicBlockInContext(context, mainFunction, string.Empty));
                LLVM.BuildCall(builder, entryPoint.GeneratedValue, new ValueRef[0], string.Empty);
	            LLVM.BuildRet(builder, LLVM.ConstInt(LLVM.Int32TypeInContext(context), 0, false));
	        }

            LLVM.DisposeBuilder(builder);

            // Verify module
            string message;
            if (LLVM.VerifyModule(module, VerifierFailureAction.PrintMessageAction, out message))
            {
                throw new InvalidOperationException(message);
            }
            
            return module;
        }

        /// <summary>
        /// Gets the specified class.
        /// </summary>
        /// <param name="typeDefinition">The type definition.</param>
        /// <returns></returns>
        Class GetClass(TypeDefinition typeDefinition)
        {
            Class @class;
            if (classes.TryGetValue(typeDefinition, out @class))
                throw new InvalidOperationException(string.Format("Could not find class {0}", typeDefinition));

            return @class;
        }

        /// <summary>
        /// Compiles the specified class.
        /// </summary>
        /// <param name="typeDefinition">The type definition.</param>
        /// <returns></returns>
        Class CompileClass(TypeDefinition typeDefinition)
        {
            Class @class;
            if (classes.TryGetValue(typeDefinition, out @class))
                return @class;

            TypeRef dataType;
            var stackType = StackValueType.Unknown;
            bool processFields = false;

            switch (typeDefinition.MetadataType)
            {
                case MetadataType.Void:
                    dataType = LLVM.VoidTypeInContext(context);
                    break;
                case MetadataType.Boolean:
                    dataType = LLVM.Int1TypeInContext(context);
                    stackType = StackValueType.Int32;
                    break;
                case MetadataType.Char:
                case MetadataType.Byte:
                case MetadataType.SByte:
                    dataType = LLVM.Int8TypeInContext(context);
                    stackType = StackValueType.Int32;
                    break;
                case MetadataType.Int16:
                case MetadataType.UInt16:
                    dataType = LLVM.Int16TypeInContext(context);
                    stackType = StackValueType.Int32;
                    break;
                case MetadataType.Int32:
                case MetadataType.UInt32:
                    dataType = LLVM.Int32TypeInContext(context);
                    stackType = StackValueType.Int32;
                    break;
                case MetadataType.Int64:
                case MetadataType.UInt64:
                    dataType = LLVM.Int64TypeInContext(context);
                    stackType = StackValueType.Int64;
                    break;
                case MetadataType.String:
                    // String: 32 bit length + char pointer
                    dataType = LLVM.StructCreateNamed(context, typeDefinition.FullName);
                    LLVM.StructSetBody(dataType, new[] { LLVM.Int32TypeInContext(context), LLVM.PointerType(LLVM.Int8TypeInContext(context), 0) }, false);
                    stackType = StackValueType.Value;
                    break;
                case MetadataType.Class:
                    // Process non-static fields
                    dataType = LLVM.StructCreateNamed(context, typeDefinition.FullName);
                    processFields = true;
                    stackType = StackValueType.Object;
                    break;
                default:
                    throw new NotImplementedException();
            }

            @class = new Class(typeDefinition, dataType, stackType);
            classes.Add(typeDefinition, @class);

            if (processFields)
            {
                var fieldTypes = new List<TypeRef>(typeDefinition.Fields.Count);

                foreach (var field in typeDefinition.Fields)
                {
                    if (field.IsStatic)
                        continue;

                    fieldTypes.Add(CompileType(assembly.MainModule.Import(field.FieldType)).GeneratedType);
                }

                LLVM.StructSetBody(dataType, fieldTypes.ToArray(), false);
            }

            return @class;
        }

        void CompileClassMethods(Class @class)
        {
            bool isExternal = @class.TypeDefinition.Module.Assembly != assembly;
            if (!isExternal)
            {
                // Process methods
                foreach (var method in @class.TypeDefinition.Methods)
                {
                    var function = CompileFunction(method);
                }
            }
        }

        /// <summary>
        /// Gets the specified type.
        /// </summary>
        /// <param name="typeReference">The type reference.</param>
        /// <returns></returns>
        Type GetType(TypeReference typeReference)
        {
            Type type;
            if (!types.TryGetValue(typeReference, out type))
                throw new InvalidOperationException(string.Format("Could not find type {0}", typeReference));

            return type;
        }

        /// <summary>
        /// Compiles the specified type.
        /// </summary>
        /// <param name="typeReference">The type reference.</param>
        /// <returns></returns>
        Type CompileType(TypeReference typeReference)
        {
            Type type;
            if (types.TryGetValue(typeReference, out type))
                return type;

            if (typeReference.MetadataType != MetadataType.Void)
            {
                var typeDefinition = typeReference.Resolve();
                var @class = CompileClass(typeDefinition);

                type = new Type(typeReference, @class.Type, @class.StackType);
            }
            else
            {
                type = new Type(typeReference, LLVM.VoidTypeInContext(context), StackValueType.Unknown);
            }

            types.Add(typeReference, type);

            if (type == null)
            {
                throw new NotImplementedException();
            }

            return type;
        }

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
        /// Compiles the function.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <returns></returns>
        Function CompileFunction(MethodReference method)
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
                var parameterType = CompileType(parameter.ParameterType);
                if (parameterType.GeneratedType.Value == IntPtr.Zero)
                    throw new InvalidOperationException();
                parameterTypes[index] = parameterType;
                parameterTypesLLVM[index] = parameterType.GeneratedType;
            }

            var returnType = CompileType(method.ReturnType);

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

        private void CompileMethodImpl(MethodDefinition method, Function function)
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

                var type = CompileType(local.VariableType);
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
                        var targetMethod = CompileFunction(targetMethodReference);

                        EmitCall(stack, targetMethodReference, targetMethod);

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
    }
}