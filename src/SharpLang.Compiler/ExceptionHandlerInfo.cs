using System.Collections.Generic;
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
        public ExceptionHandlerInfo(ExceptionHandler source)
        {
            Source = source;
            LeaveTargets = new List<Instruction>();
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

        /// <summary>
        /// Gets the leave targets (instructions that can be reached when exiting finally clause).
        /// </summary>
        /// <value>
        /// The leave targets.
        /// </value>
        public List<Instruction> LeaveTargets { get; private set; }
    }
}