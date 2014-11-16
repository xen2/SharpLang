using System.Runtime.CompilerServices;

namespace System
{
    static class SharpLangHelper
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern static unsafe void* GetObjectPointer(object obj);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern static unsafe object GetObjectFromPointer(void* obj);
    }
}