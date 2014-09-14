using SharpLLVM;

namespace SharpLang.CompilerServices
{
    class Scope
    {
        public Scope(Mono.Cecil.Cil.Scope source)
        {
            Source = source;
        }

        public Mono.Cecil.Cil.Scope Source { get; private set; }

        public ValueRef GeneratedScope { get; set; }
    }
}