// Copyright (c) 2014 SharpLang - Virgile Bello

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SharpLang.Marshalling
{
    static class MarshalHelper
    {
        private const int ThunkCount = 4096;

        private static object thunkAllocatorLock = new object();
        private static int thunkNextEntry = 0;

        private static Delegate[] delegates = new Delegate[ThunkCount];

        [MethodImpl(MethodImplOptions.InternalCall)]
        private unsafe static extern IntPtr* GetThunkTargets();

        [MethodImpl(MethodImplOptions.InternalCall)]
        private unsafe static extern IntPtr* GetThunkPointers();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern uint GetThunkCurrentId();

        private static readonly Dictionary<SharpLangEETypePtr, IntPtr> delegateWrappers = new Dictionary<SharpLangEETypePtr, IntPtr>();

        public static unsafe void RegisterDelegateWrapper(SharpLangEEType* delegateType, IntPtr delegateWrapper)
        {
            delegateWrappers[delegateType] = delegateWrapper;
        }

        public static unsafe IntPtr GetFunctionPointerForDelegate(Delegate d)
        {
            IntPtr delegateWrapper;
            var delegateEETypePtr = *(SharpLangEETypePtr*)SharpLangHelper.GetObjectPointer(d);
            if (delegateWrappers.TryGetValue(delegateEETypePtr, out delegateWrapper))
            {
                return CreateThunk(d, delegateWrapper);
            }

            throw new PlatformNotSupportedException();
        }

        public static Delegate GetDelegate()
        {
            return delegates[GetThunkCurrentId()];
        }

        public static unsafe IntPtr CreateThunk(Delegate @delegate, IntPtr methodTarget)
        {
            lock (thunkAllocatorLock)
            {
                int thunkEntry = thunkNextEntry;
                Delegate thunkDelegate;
                while ((thunkDelegate = delegates[thunkEntry]) != null)
                {
                    // TODO: Use WeakReference<Delegate> instead, and check if it has been GC
                    if (++thunkEntry >= ThunkCount)
                        thunkEntry = 0;

                    // We looped, which means nothing was found
                    // TODO: We should give it another try after performing GC
                    if (thunkEntry == thunkNextEntry)
                    {
                        throw new InvalidOperationException("No thunk available");
                    }
                }

                // Setup delegate and thunk target
                // TODO: We probably want a struct instead of 2 separate arrays
                delegates[thunkEntry] = @delegate;
                GetThunkTargets()[thunkEntry] = methodTarget;

                // Return our thunk redirect function pointer
                return GetThunkPointers()[thunkEntry];                
            }
        }
    }
}