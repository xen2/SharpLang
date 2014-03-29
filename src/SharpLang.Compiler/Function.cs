using SharpLLVM;

namespace SharpLang.CompilerServices
{
    class Function
    {
        /// <summary>
        /// Gets or sets the LLVM generated value.
        /// </summary>
        /// <value>
        /// The LLVM generated value.
        /// </value>
        public ValueRef GeneratedValue { get; internal set; }
    }
}