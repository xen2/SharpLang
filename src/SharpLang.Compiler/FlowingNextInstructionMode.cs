namespace SharpLang.CompilerServices
{
    /// <summary>
    /// Specifies how code flow to next instruction
    /// </summary>
    public enum FlowingNextInstructionMode
    {
        /// <summary>
        /// Flows automatically to the next instruction (inserts a Br if it's a new basic block, and merge the stack).
        /// </summary>
        Implicit = 0,

        /// <summary>
        /// Flows explicitely to the next instruction (there is already a branch, just merge the stack).
        /// </summary>
        Explicit = 1,

        /// <summary>
        /// Do not flow to next instruction (no stack merge).
        /// </summary>
        None = 2,
    }
}