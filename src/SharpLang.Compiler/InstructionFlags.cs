using System;

namespace SharpLang.CompilerServices
{
    [Flags]
    public enum InstructionFlags
    {
        None = 0,
        Volatile = 1,
        Unaligned = 2,
    }
}