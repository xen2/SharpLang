using Mono.Cecil.Cil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    class ExceptionHandlerInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExceptionHandlerInfo"/> class.
        /// </summary>
        /// <param name="source">The Cecil exception handler.</param>
        /// <param name="catchDispatch">The catch dispatch basic block.</param>
        public ExceptionHandlerInfo(ExceptionHandler source, BasicBlockRef catchDispatch)
        {
            Source = source;
            CatchDispatch = catchDispatch;
        }

        /// <summary>
        /// Gets or sets the Cecil exception handler.
        /// </summary>
        /// <value>
        /// The source.
        /// </value>
        public ExceptionHandler Source { get; set; }

        /// <summary>
        /// Gets or sets the catch dispatch basic block, that will dispatch to appropriate catch clause depending on exception type.
        /// </summary>
        /// <value>
        /// The catch dispatch.
        /// </value>
        public BasicBlockRef CatchDispatch { get; set; }
    }
}