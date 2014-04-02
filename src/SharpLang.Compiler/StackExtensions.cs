using System.Collections.Generic;

namespace SharpLang.CompilerServices
{
    static class StackExtensions
    {
        public static StackValue Pop(this List<StackValue> stack)
        {
            int lastIndex = stack.Count - 1;

            var stackValue = stack[lastIndex];
            stack.RemoveAt(lastIndex);

            return stackValue;
        }
    }
}