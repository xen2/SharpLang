using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    class FunctionCompilerContext
    {
        public FunctionCompilerContext(Function function) : this(function.GeneratedValue, function.Signature)
        {
            MethodReference = function.MethodReference;
            Method = MethodReference.Resolve();
        }

        public FunctionCompilerContext(ValueRef functionGlobal, FunctionSignature signature)
        {
            FunctionGlobal = functionGlobal;
            FlowingNextInstructionMode = FlowingNextInstructionMode.Implicit;
            Signature = signature;
        }

        public MethodReference MethodReference { get; set; }
        public MethodDefinition Method { get; set; }
        public MethodBody Body { get; set; }
        public ValueRef FunctionGlobal { get; set; }
        public BasicBlockRef BasicBlock { get; set; }
        public FunctionStack Stack { get; set; }
        public List<StackValue> Locals { get; set; }
        public List<StackValue> Arguments { get; set; }
        public FunctionSignature Signature { get; set; }

        public List<Scope> Scopes { get; set; } 

        public BasicBlockRef[] BasicBlocks { get; set; }
        public StackValue[][] ForwardStacks { get; set; }

        /// <summary>
        /// Specify if we have to manually add an unconditional branch to go to next block (flowing) or not (due to a previous explicit conditional branch).
        /// </summary>
        public FlowingNextInstructionMode FlowingNextInstructionMode { get; set; }

        // Instruction states
        public InstructionFlags InstructionFlags { get; set; }
        public Class ConstrainedClass { get; set; }

        // Exception handling
        public List<ExceptionHandlerInfo> ExceptionHandlers { get; set; }
        public List<ExceptionHandlerInfo> ActiveTryHandlers { get; set; }
        public ValueRef EndfinallyJumpTarget { get; set; }
        public ValueRef ExceptionSlot { get; set; }
        public ValueRef ExceptionHandlerSelectorSlot { get; set; }
        public BasicBlockRef ResumeExceptionBlock { get; set; }
        public BasicBlockRef LandingPadBlock { get; set; }

        // Debug info
        public DIDescriptor DebugFile { get; set; }
    }
}