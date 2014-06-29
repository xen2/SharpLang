using System.Collections.Generic;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    class FunctionCompilerContext
    {
        public FunctionCompilerContext(Function function)
        {
            Function = function;
        }

        public Function Function { get; set; }
        public BasicBlockRef BasicBlock { get; set; }
        public List<StackValue> Stack { get; set; }
        public BasicBlockRef LandingPadBlock { get; set; }
    }
}