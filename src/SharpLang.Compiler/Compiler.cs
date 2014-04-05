using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public sealed partial class Compiler
    {
        private ModuleRef module;
        private ContextRef context;
        private BuilderRef builder;

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
                        CompileMethod(method);
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
                    var function = CompileMethod(method);
                }
            }
        }

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

        Function CompileMethod(MethodReference method)
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

        private void CompileMethodImpl(MethodDefinition method, Function function)
        {
            var numParams = method.Parameters.Count;
            var body = method.Body;
            var functionGlobal = function.GeneratedValue;

            var basicBlock = LLVM.AppendBasicBlockInContext(context, functionGlobal, string.Empty);

            LLVM.PositionBuilderAtEnd(builder, basicBlock);

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

            for (int index = 0; index < function.ParameterTypes.Length; index++)
            {
                var argType = function.ParameterTypes[index];
                var arg = LLVM.GetParam(functionGlobal, (uint)index);
                args.Add(new StackValue(argType.StackType, argType, arg));
            }

            // Some wasted space due to unused offsets, but we only keep one so it should be fine.
            var branchTargets = new int[body.CodeSize];
            var basicBlocks = new BasicBlockRef[body.CodeSize];

            // First instruction can always be reached
            branchTargets[0] = 1;

            // Find branch targets (which will require PHI node for stack merging)
            for (int index = 0; index < body.Instructions.Count; index++)
            {
                var instruction = body.Instructions[index];

                var flowControl = instruction.OpCode.FlowControl;
                if (flowControl == FlowControl.Cond_Branch
                    || flowControl == FlowControl.Branch)
                {
                    var target = (Instruction)instruction.Operand;

                    // Operand Target can be reached
                    branchTargets[target.Offset] += 2;
                }

                // TODO: Break?
                if (flowControl == FlowControl.Cond_Branch)
                {
                    // Need to enforce a block to be created after a conditional branch
                    if (instruction.Next != null)
                        branchTargets[instruction.Next.Offset] += 2;
                }
                else if (flowControl != FlowControl.Branch
                    && flowControl != FlowControl.Throw
                    && flowControl != FlowControl.Return)
                {
                    // Next instruction can be reached
                    if (instruction.Next != null)
                        branchTargets[instruction.Next.Offset]++;
                }
            }

            // Create basic block
            // TODO: Could be done during previous pass
            for (int offset = 0; offset < branchTargets.Length; offset++)
            {
                // We only create basic block sif there is at least 2 way to access the instruction.
                if (branchTargets[offset] < 2)
                    continue;

                basicBlocks[offset] = LLVM.AppendBasicBlockInContext(context, functionGlobal, string.Format("L_{0:x4}", offset));
            }

            // Specify if we have to manually add an unconditional branch to go to next block (flowing) or not (due to a previous explicit conditional branch)
            bool flowingToNextBlock = false;

            foreach (var instruction in body.Instructions)
            {
                var stackMerges = branchTargets[instruction.Offset];
                bool needPhiNodes = (stackMerges > 1);

                if (needPhiNodes)
                {
                    var previousBasicBlock = basicBlock;
                    basicBlock = basicBlocks[instruction.Offset];

                    if (flowingToNextBlock)
                    {
                        // Add a jump from previous block to new block
                        LLVM.BuildBr(builder, basicBlock);
                    }

                    // Position builder to write at beginning of new block
                    LLVM.PositionBuilderAtEnd(builder, basicBlock);

                    // Replace all stack with PHI nodes.
                    // LLVM will optimize all the unecessary ones.
                    // LLVm also allows self-referential PHI nodes so we don't need to care about loops.
                    for (int index = 0; index < stack.Count; index++)
                    {
                        var stackValue = stack[index];

                        // TODO: Check stack type during merging?
                        var newStackValue = new StackValue(stackValue.StackType, stackValue.Type, LLVM.BuildPhi(builder, LLVM.TypeOf(stackValue.Value), string.Empty));

                        // Add values flowing from previous instruction
                        LLVM.AddIncoming(newStackValue.Value, new[] { stackValue.Value }, new[] { previousBasicBlock });

                        stack[index] = newStackValue;
                    }
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
                        var targetMethod = CompileMethod(targetMethodReference);

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
            }
        }
    }
}