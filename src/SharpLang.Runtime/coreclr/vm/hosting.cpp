//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
//

//


#include "common.h"

// non-zero return value if this function causes the OS to switch to another thread
// See file:spinlock.h#SwitchToThreadSpinning for an explanation of dwSwitchCount
BOOL __SwitchToThread (DWORD dwSleepMSec, DWORD dwSwitchCount)
{
  CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_ANY;
        SO_TOLERANT;
    }
    CONTRACTL_END;
	
    return  __DangerousSwitchToThread(dwSleepMSec, dwSwitchCount, FALSE);
}

#undef SleepEx
BOOL __DangerousSwitchToThread (DWORD dwSleepMSec, DWORD dwSwitchCount, BOOL goThroughOS)
{
    // If you sleep for a long time, the thread should be in Preemptive GC mode.
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_ANY;
        SO_TOLERANT;
        PRECONDITION(dwSleepMSec < 10000 || GetThread() == NULL || !GetThread()->PreemptiveGCDisabled());
    }
    CONTRACTL_END;

    if (CLRTaskHosted())
    {
        Thread *pThread = GetThread();
        if (pThread && pThread->HasThreadState(Thread::TS_YieldRequested))
        {
            pThread->ResetThreadState(Thread::TS_YieldRequested);
        }
    }

    if (dwSleepMSec > 0)
    {
        // when called with goThroughOS make sure to not call into the host. This function
        // may be called from GetRuntimeFunctionCallback() which is called by the OS to determine
        // the personality routine when it needs to unwind managed code off the stack. when this
        // happens in the context of an SO we want to avoid calling into the host
        if (goThroughOS)
            ::SleepEx(dwSleepMSec, FALSE);
        else
            ClrSleepEx(dwSleepMSec,FALSE);
        return TRUE;
    }

    // In deciding when to insert sleeps, we wait until we have been spinning
    // for a long time and then always sleep.  The former is to let short perf-critical
    // __SwitchToThread loops avoid context switches.  The latter is to ensure
    // that if many threads are spinning waiting for a lower-priority thread
    // to run that they will eventually all be asleep at the same time.
    // 
    // The specific values are derived from the NDP 2.0 SP1 fix: it waits for 
    // 8 million cycles of __SwitchToThread calls where each takes ~300-500,
    // which means we should wait in the neighborhood of 25000 calls.
    // 
    // As of early 2011, ARM CPUs are much slower, so we need a lower threshold.
    // The following two values appear to yield roughly equivalent spin times
    // on their respective platforms.
    //
#ifdef _TARGET_ARM_
    #define SLEEP_START_THRESHOLD (5 * 1024)
#else
    #define SLEEP_START_THRESHOLD (32 * 1024)
#endif

    _ASSERTE(CALLER_LIMITS_SPINNING < SLEEP_START_THRESHOLD);
    if (dwSwitchCount >= SLEEP_START_THRESHOLD)
    {
        if (goThroughOS)
            ::SleepEx(1, FALSE);
        else
            ClrSleepEx(1, FALSE);
    }

#ifdef FEATURE_INCLUDE_ALL_INTERFACES
    IHostTaskManager *provider = CorHost2::GetHostTaskManager();
    if ((provider != NULL) && (goThroughOS == FALSE))
    {
        DWORD option = 0;

        HRESULT hr;

        BEGIN_SO_TOLERANT_CODE_CALLING_HOST(GetThread());
        hr = provider->SwitchToTask(option);
        END_SO_TOLERANT_CODE_CALLING_HOST;

        return hr == S_OK;
    }
    else
#endif // FEATURE_INCLUDE_ALL_INTERFACES
    {
        return SwitchToThread();
    }
}