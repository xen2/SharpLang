// Copyright (c) 2014 SharpLang - Virgile Bello

using System.Runtime.CompilerServices;

namespace System.Reflection
{
    public partial class Assembly
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern static int GetCallStack(IntPtr[] callstack);
        
        public static Assembly GetExecutingAssembly()
        {
            var callstack = new IntPtr[64];
            var callstackSize = GetCallStack(callstack);

            // Ignore GetCallStack and GetExecutingAssembly
            return FindAssemblyOfMethodInCallStack(2, callstackSize, callstack);
        }

        public static Assembly GetCallingAssembly()
        {
            var callstack = new IntPtr[64];
            var callstackSize = GetCallStack(callstack);

            // Ignore GetCallStack, GetCallingAssembly and its parent
            return FindAssemblyOfMethodInCallStack(3, callstackSize, callstack);
        }

        private static unsafe Assembly FindAssemblyOfMethodInCallStack(int callstackStart, int callstackSize, IntPtr[] callstack)
        {
            for (int i = callstackStart; i < callstackSize; ++i)
            {
                var methodPointer = callstack[i];
                var eeType = SharpLangModule.ResolveEETypeFromMethodPointer(methodPointer);
                if (eeType.Value != null)
                {
                    var runtimeType = SharpLangModule.ResolveType(eeType);
                    return runtimeType.Module.Assembly;
                }
            }
            return null;
        }
    }
}