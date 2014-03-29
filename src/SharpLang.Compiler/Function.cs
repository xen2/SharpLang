using SharpLLVM;

namespace SharpLang.CompilerServices
{
    class Function
    {
        public Function(ValueRef generatedValue)
        {
            GeneratedValue = generatedValue;
        }

        /// <summary>
        /// Gets or sets the LLVM generated value.
        /// </summary>
        /// <value>
        /// The LLVM generated value.
        /// </value>
        public ValueRef GeneratedValue { get; internal set; }
    }
}