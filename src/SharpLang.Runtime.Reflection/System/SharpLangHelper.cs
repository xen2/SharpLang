using System.Runtime.CompilerServices;

namespace System
{
    static class SharpLangHelper
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern static unsafe void* GetObjectPointer(object obj);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern static unsafe object GetObjectFromPointer(void* obj);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern unsafe T UnsafeCast<T>(object value) where T : class;
    }
}