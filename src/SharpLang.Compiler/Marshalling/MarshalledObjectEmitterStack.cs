using System.Collections.Generic;
using System.Diagnostics;

namespace SharpLang.CompilerServices.Marshalling
{
    /// <summary>
    /// Represents a stack of object being marshalled.
    /// </summary>
    class MarshalledObjectEmitterStack
    {
        private readonly Stack<MarshalledObjectEmitter> stack = new Stack<MarshalledObjectEmitter>();

        public int Count
        {
            get { return stack.Count; }
        }

        [DebuggerStepThrough]
        public MarshalledObjectEmitter Peek()
        {
            return stack.Peek();
        }

        [DebuggerStepThrough]
        public void Push(MarshalledObjectEmitter marshalledObjectEmitter)
        {
            if (stack.Count > 0)
                marshalledObjectEmitter.Previous = stack.Peek();
            stack.Push(marshalledObjectEmitter);
        }

        [DebuggerStepThrough]
        public void Pop()
        {
            stack.Pop();
        }
    }
}