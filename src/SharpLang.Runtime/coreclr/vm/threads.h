//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// THREADS.H -
//


// 
// 
// Currently represents a logical and physical COM+ thread. Later, these concepts will be separated.
//

// 
// #RuntimeThreadLocals.
// 
// Windows has a feature call Thread Local Storage (TLS, which is data that the OS allocates every time it
// creates a thread). Programs access this storage by using the Windows TlsAlloc, TlsGetValue, TlsSetValue
// APIs (see http://msdn2.microsoft.com/en-us/library/ms686812.aspx). The runtime allocates two such slots
// for its use
// 
//     * A slot that holds a pointer to the runtime thread object code:Thread (see code:#ThreadClass). The
//         runtime has a special optimized version of this helper code:GetThread (we actually emit assembly
//         code on the fly so it is as fast as possible). These code:Thread objects live in the
//         code:ThreadStore.
//         
//      * The other slot holds the current code:AppDomain (a managed equivalent of a process). The
//          runtime thread object also has a pointer to the thread's AppDomain (see code:Thread.m_pDomain,
//          so in theory this TLS is redundant. It is there for speed (one less pointer indirection). The
//          optimized helper for this is code:GetAppDomain (we emit assembly code on the fly for this one
//          too).
//          
// Initially these TLS slots are empty (when the OS starts up), however before we run managed code, we must
// set them properly so that managed code knows what AppDomain it is in and we can suspend threads properly
// for a GC (see code:#SuspendingTheRuntime)
// 
// #SuspendingTheRuntime
// 
// One of the primary differences between runtime code (managed code), and traditional (unmanaged code) is
// the existence of the GC heap (see file:gc.cpp#Overview). For the GC to do its job, it must be able to
// traverse all references to the GC heap, including ones on the stack of every thread, as well as any in
// hardware registers. While it is simple to state this requirement, it has long reaching effects, because
// properly accounting for all GC heap references ALL the time turns out to be quite hard. When we make a
// bookkeeping mistake, a GC reference is not reported at GC time, which means it will not be updated when the
// GC happens. Since memory in the GC heap can move, this can cause the pointer to point at 'random' places
// in the GC heap, causing data corruption. This is a 'GC Hole', and is very bad. We have special modes (see
// code:EEConfig.GetGCStressLevel) called GCStress to help find such issues.
// 
// In order to find all GC references on the stacks we need insure that no thread is manipulating a GC
// reference at the time of the scan. This is the job of code:Thread.SuspendRuntime. Logically it suspends
// every thread in the process. Unfortunately it can not literally simply call the OS SuspendThread API on
// all threads. The reason is that the other threads MIGHT hold important locks (for example there is a lock
// that is taken when unmanaged heap memory is requested, or when a DLL is loaded). In general process
// global structures in the OS will be protected by locks, and if you suspend a thread it might hold that
// lock. If you happen to need that OS service (eg you might need to allocated unmanaged memory), then
// deadlock will occur (as you wait on the suspended thread, that never wakes up).
// 
// Luckily, we don't need to actually suspend the threads, we just need to insure that all GC references on
// the stack are stable. This is where the concept of cooperative mode and preemptive mode (a bad name) come
// from.
// 
// #CooperativeMode
// 
// The runtime keeps a table of all threads that have ever run managed code in the code:ThreadStore table.
// The ThreadStore table holds a list of Thread objects (see code:#ThreadClass). This object holds all
// infomation about managed threads. Cooperative mode is defined as the mode the thread is in when the field
// code:Thread.m_fPreemptiveGCDisabled is non-zero. When this field is zero the thread is said to be in
// Preemptive mode (named because if you preempt the thread in this mode, it is guaranteed to be in a place
// where a GC can occur).
// 
// When a thread is in cooperative mode, it is basically saying that it is potentially modifying GC
// references, and so the runtime must Cooperate with it to get to a 'GC Safe' location where the GC
// references can be enumerated. This is the mode that a thread is in MOST times when it is running managed
// code (in fact if the EIP is in JIT compiled code, there is only one place where you are NOT in cooperative
// mode (Inlined PINVOKE transition code)). Conversely, any time non-runtime unmanaged code is running, the
// thread MUST NOT be in cooperative mode (you risk deadlock otherwise). Only code in mscorwks.dll might be
// running in either cooperative or preemptive mode.
// 
// It is easier to describe the invariant associated with being in Preemptive mode. When the thread is in
// preemptive mode (when code:Thread.m_fPreemptiveGCDisabled is zero), the thread guarantees two things
// 
//     * That it not currently running code that manipulates GC references.
//     * That it has set the code:Thread.m_pFrame pointer in the code:Thread to be a subclass of the class
//         code:Frame which marks the location on the stack where the last managed method frame is. This
//         allows the GC to start crawling the stack from there (essentially skip over the unmanaged frames).
//     * That the thread will not reenter managed code if the global variable code:g_TrapReturningThreads is
//         set (it will call code:Thread.RareDisablePreemptiveGC first which will block if a a suspension is
//         in progress)
// 
// The basic idea is that the suspension logic in code:Thread.SuspendRuntime first sets the global variable
// code:g_TrapReturningThreads and then checks if each thread in the ThreadStore is in Cooperative mode. If a
// thread is NOT in cooperative mode, the logic simply skips the thread, because it knows that the thread
// will stop itself before reentering managed code (because code:g_TrapReturningThreads is set). This avoids
// the deadlock problem mentioned earlier, because threads that are running unmanaged code are allowed to
// run. Enumeration of GC references starts at the first managed frame (pointed at by code:Thread.m_pFrame).
// 
// When a thread is in cooperative mode, it means that GC references might be being manipulated. There are
// two important possibilities
// 
//     * The CPU is running JIT compiled code
//     * The CPU is running code elsewhere (which should only be in mscorwks.dll, because everywhere else a
//         transition to preemptive mode should have happened first)
//     
// * #PartiallyInteruptibleCode
// * #FullyInteruptibleCode
// 
// If the Instruction pointer (x86/x64: EIP, ARM: R15/PC) is in JIT compiled code, we can detect this because we have tables that
// map the ranges of every method back to their code:MethodDesc (this the code:ICodeManager interface). In
// addition to knowing the method, these tables also point at 'GCInfo' that tell for that method which stack
// locations and which registers hold GC references at any particular instruction pointer. If the method is
// what is called FullyInterruptible, then we have information for any possible instruction pointer in the
// method and we can simply stop the thread (however we have to do this carefully TODO explain).
// 
// However for most methods, we only keep GC information for paticular EIP's, in particular we keep track of
// GC reference liveness only at call sites. Thus not every location is 'GC Safe' (that is we can enumerate
// all references, but must be 'driven' to a GC safe location).
// 
// We drive threads to GC safe locations by hijacking. This is a term for updating the return address on the
// stack so that we gain control when a method returns. If we find that we are in JITTed code but NOT at a GC
// safe location, then we find the return address for the method and modfiy it to cause the runtime to stop.
// We then let the method run. Hopefully the method quickly returns, and hits our hijack, and we are now at a
// GC-safe location (all call sites are GC-safe). If not we repeat the procedure (possibly moving the
// hijack). At some point a method returns, and we get control. For methods that have loops that don't make
// calls, we are forced to make the method FullyInterruptible, so we can be sure to stop the mehod.
// 
// This leaves only the case where we are in cooperative modes, but not in JIT compiled code (we should be in
// clr.dll). In this case we simply let the thread run. The idea is that code in clr.dll makes the
// promise that it will not do ANYTHING that will block (which includes taking a lock), while in cooperative
// mode, or do anything that might take a long time without polling to see if a GC is needed. Thus this code
// 'cooperates' to insure that GCs can happen in a timely fashion.
//
// If you need to switch the GC mode of the current thread, look for the GCX_COOP() and GCX_PREEMP() macros.
//

#ifndef __threads_h__
#define __threads_h__

#include "gc.h"
class AVInRuntimeImplOkayHolder {};

#define GetThreadNULLOk() GetThread()

class Thread
{
public:
    Context* GetContext() { return Context::GetDefault(); }
    AppDomain* GetDomain() { return AppDomain::GetDefault(); }
    
        // If we are trying to suspend a thread, we set the appropriate pending bit to
    // indicate why we want to suspend it (TS_GCSuspendPending, TS_UserSuspendPending,
    // TS_DebugSuspendPending).
    //
    // If instead the thread has blocked itself, via WaitSuspendEvent, we indicate
    // this with TS_SyncSuspended.  However, we need to know whether the synchronous
    // suspension is for a user request, or for an internal one (GC & Debug).  That's
    // because a user request is not allowed to resume a thread suspended for
    // debugging or GC.  -- That's not stricly true.  It is allowed to resume such a
    // thread so long as it was ALSO suspended by the user.  In other words, this
    // ensures that user resumptions aren't unbalanced from user suspensions.
    //
    enum ThreadState
    {
        TS_Unknown                = 0x00000000,    // threads are initialized this way

        TS_AbortRequested         = 0x00000001,    // Abort the thread
        TS_GCSuspendPending       = 0x00000002,    // waiting to get to safe spot for GC
        TS_UserSuspendPending     = 0x00000004,    // user suspension at next opportunity
        TS_DebugSuspendPending    = 0x00000008,    // Is the debugger suspending threads?
        TS_GCOnTransitions        = 0x00000010,    // Force a GC on stub transitions (GCStress only)

        TS_LegalToJoin            = 0x00000020,    // Is it now legal to attempt a Join()

        TS_YieldRequested         = 0x00000040,    // The task should yield

#ifdef FEATURE_HIJACK
        TS_Hijacked               = 0x00000080,    // Return address has been hijacked
#endif // FEATURE_HIJACK
        TS_BlockGCForSO           = 0x00000100,    // If a thread does not have enough stack, WaitUntilGCComplete may fail.
                                                   // Either GC suspension will wait until the thread has cleared this bit,
                                                   // Or the current thread is going to spin if GC has suspended all threads.
        TS_Background             = 0x00000200,    // Thread is a background thread
        TS_Unstarted              = 0x00000400,    // Thread has never been started
        TS_Dead                   = 0x00000800,    // Thread is dead

        TS_WeOwn                  = 0x00001000,    // Exposed object initiated this thread
#ifdef FEATURE_COMINTEROP_APARTMENT_SUPPORT
        TS_CoInitialized          = 0x00002000,    // CoInitialize has been called for this thread

        TS_InSTA                  = 0x00004000,    // Thread hosts an STA
        TS_InMTA                  = 0x00008000,    // Thread is part of the MTA
#endif // FEATURE_COMINTEROP_APARTMENT_SUPPORT

        // Some bits that only have meaning for reporting the state to clients.
        TS_ReportDead             = 0x00010000,    // in WaitForOtherThreads()
        TS_FullyInitialized       = 0x00020000,    // Thread is fully initialized and we are ready to broadcast its existence to external clients

        TS_TaskReset              = 0x00040000,    // The task is reset

        TS_SyncSuspended          = 0x00080000,    // Suspended via WaitSuspendEvent
        TS_DebugWillSync          = 0x00100000,    // Debugger will wait for this thread to sync

        TS_StackCrawlNeeded       = 0x00200000,    // A stackcrawl is needed on this thread, such as for thread abort
                                                   // See comment for s_pWaitForStackCrawlEvent for reason.

        TS_SuspendUnstarted       = 0x00400000,    // latch a user suspension on an unstarted thread

        TS_Aborted                = 0x00800000,    // is the thread aborted?
        TS_TPWorkerThread         = 0x01000000,    // is this a threadpool worker thread?

        TS_Interruptible          = 0x02000000,    // sitting in a Sleep(), Wait(), Join()
        TS_Interrupted            = 0x04000000,    // was awakened by an interrupt APC. !!! This can be moved to TSNC

        TS_CompletionPortThread   = 0x08000000,    // Completion port thread

        TS_AbortInitiated         = 0x10000000,    // set when abort is begun

        TS_Finalized              = 0x20000000,    // The associated managed Thread object has been finalized.
                                                   // We can clean up the unmanaged part now.

        TS_FailStarted            = 0x40000000,    // The thread fails during startup.
        TS_Detached               = 0x80000000,    // Thread was detached by DllMain

        // <TODO> @TODO: We need to reclaim the bits that have no concurrency issues (i.e. they are only
        //         manipulated by the owning thread) and move them off to a different DWORD.  Note if this
        //         enum is changed, we also need to update SOS to reflect this.</TODO>

        // We require (and assert) that the following bits are less than 0x100.
        TS_CatchAtSafePoint = (TS_UserSuspendPending | TS_AbortRequested |
                               TS_GCSuspendPending | TS_DebugSuspendPending | TS_GCOnTransitions | TS_YieldRequested),
    };

    // Thread flags that have no concurrency issues (i.e., they are only manipulated by the owning thread). Use these
    // state flags when you have a new thread state that doesn't belong in the ThreadState enum above.
    //
    // <TODO>@TODO: its possible that the ThreadTasks from above and these flags should be merged.</TODO>
    enum ThreadStateNoConcurrency
    {
        TSNC_Unknown                    = 0x00000000, // threads are initialized this way

        TSNC_DebuggerUserSuspend        = 0x00000001, // marked "suspended" by the debugger
        TSNC_DebuggerReAbort            = 0x00000002, // thread needs to re-abort itself when resumed by the debugger
        TSNC_DebuggerIsStepping         = 0x00000004, // debugger is stepping this thread
        TSNC_DebuggerIsManagedException = 0x00000008, // EH is re-raising a managed exception.
        TSNC_WaitUntilGCFinished        = 0x00000010, // The current thread is waiting for GC.  If host returns
                                                      // SO during wait, we will either spin or make GC wait.
        TSNC_BlockedForShutdown         = 0x00000020, // Thread is blocked in WaitForEndOfShutdown.  We should not hit WaitForEndOfShutdown again.
        TSNC_SOWorkNeeded               = 0x00000040, // The thread needs to wake up AD unload helper thread to finish SO work
        TSNC_CLRCreatedThread           = 0x00000080, // The thread was created through Thread::CreateNewThread
        TSNC_ExistInThreadStore         = 0x00000100, // For dtor to know if it needs to be removed from ThreadStore
        TSNC_UnsafeSkipEnterCooperative = 0x00000200, // This is a "fix" for deadlocks caused when cleaning up COM
        TSNC_OwnsSpinLock               = 0x00000400, // The thread owns a spinlock.
        TSNC_PreparingAbort             = 0x00000800, // Preparing abort.  This avoids recursive HandleThreadAbort call.
        TSNC_OSAlertableWait            = 0x00001000, // Preparing abort.  This avoids recursive HandleThreadAbort call.
        TSNC_ADUnloadHelper             = 0x00002000, // This thread is AD Unload helper.
        TSNC_CreatingTypeInitException  = 0x00004000, // Thread is trying to create a TypeInitException
        TSNC_InTaskSwitch               = 0x00008000, // A task is switching
        TSNC_AppDomainContainUnhandled  = 0x00010000, // Used to control how unhandled exception reporting occurs.
                                                      // See detailed explanation for this bit in threads.cpp
        TSNC_InRestoringSyncBlock       = 0x00020000, // The thread is restoring its SyncBlock for Object.Wait.
                                                      // After the thread is interrupted once, we turn off interruption
                                                      // at the beginning of wait.
        TSNC_DisableOleaut32Check       = 0x00040000, // Disable oleaut32 delay load check.  Oleaut32 has  
                                                      // been loaded
        TSNC_CannotRecycle              = 0x00080000, // A host can not recycle this Thread object.  When a thread
                                                      // has orphaned lock, we will apply this.
        TSNC_RaiseUnloadEvent           = 0x00100000, // Finalize thread is raising managed unload event which 
                                                      // may call AppDomain.Unload.
        TSNC_UnbalancedLocks            = 0x00200000, // Do not rely on lock accounting for this thread:
                                                      // we left an app domain with a lock count different from
                                                      // when we entered it
        TSNC_DisableSOCheckInHCALL      = 0x00400000, // Some HCALL method may be called directly from VM.
                                                      // We can not assert they are called in SOTolerant 
                                                      // region.
        TSNC_IgnoreUnhandledExceptions  = 0x00800000, // Set for a managed thread born inside an appdomain created with the APPDOMAIN_IGNORE_UNHANDLED_EXCEPTIONS flag.
        TSNC_ProcessedUnhandledException = 0x01000000,// Set on a thread on which we have done unhandled exception processing so that
                                                      // we dont perform it again when OS invokes our UEF. Currently, applicable threads include:
                                                      // 1) entry point thread of a managed app 
                                                      // 2) new managed thread created in default domain
                                                      //
                                                      // For such threads, we will return to the OS after our UE processing is done
                                                      // and the OS will start invoking the UEFs. If our UEF gets invoked, it will try to 
                                                      // perform the UE processing again. We will use this flag to prevent the duplicated
                                                      // effort.
                                                      // 
                                                      // Once we are completely independent of the OS UEF, we could remove this.
#ifdef FEATURE_SYNCHRONIZATIONCONTEXT_WAIT
        TSNC_InsideSyncContextWait      = 0x02000000, // Whether we are inside DoSyncContextWait
#endif // FEATURE_SYNCHRONIZATIONCONTEXT_WAIT
        TSNC_DebuggerSleepWaitJoin      = 0x04000000, // Indicates to the debugger that this thread is in a sleep wait or join state
                                                      // This almost mirrors the TS_Interruptible state however that flag can change
                                                      // during GC-preemptive mode whereas this one cannot.
#ifdef FEATURE_COMINTEROP
        TSNC_WinRTInitialized           = 0x08000000, // the thread has initialized WinRT
#endif // FEATURE_COMINTEROP

        TSNC_ForceStackCommit           = 0x10000000, // Commit the whole stack, even if disableCommitThreadStack is set

        TSNC_CallingManagedCodeDisabled = 0x20000000, // Use by multicore JIT feature to asert on calling managed code/loading module in background thread
                                                      // Exception, system module is allowed, security demand is allowed
        
        TSNC_LoadsTypeViolation         = 0x40000000, // Use by type loader to break deadlocks caused by type load level ordering violations

        TSNC_EtwStackWalkInProgress     = 0x80000000, // Set on the thread so that ETW can know that stackwalking is in progress
                                                      // and does not proceed with a stackwalk on the same thread
                                                      // There are cases during managed debugging when we can run into this situation
    };

    // Flags for thread states that have no concurrency issues.
    ThreadStateNoConcurrency m_StateNC;
    
    Volatile<ThreadState> m_State;   // Bits for the state of the thread
    
    void SetThreadState(ThreadState ts)
    {
        LIMITED_METHOD_CONTRACT;
        FastInterlockOr((DWORD*)&m_State, ts);
    }

    void ResetThreadState(ThreadState ts)
    {
        LIMITED_METHOD_CONTRACT;
        FastInterlockAnd((DWORD*)&m_State, ~ts);
    }
    
    BOOL HasThreadState(ThreadState ts)
    {
        LIMITED_METHOD_CONTRACT;
        return ((DWORD)m_State & ts);
    }
    
    static void BeginThreadAffinity() { }
    static void EndThreadAffinity() { }

    void EnablePreemptiveGC() { }
    void DisablePreemptiveGC() { }

    BOOL PreemptiveGCDisabled() { return false; }
};

#define ENABLE_FORBID_GC_LOADER_USE_IN_THIS_SCOPE()
class LeaveRuntimeHolder
{
public:
    template <typename T>
    LeaveRuntimeHolder(T target)
    {
        STATIC_CONTRACT_LIMITED_METHOD;
    }
};

#define GCHOLDER_CONTRACT_ARGS_NoDtor
#define GCHOLDER_CONTRACT_ARGS_HasDtor
#define GCHOLDER_DECLARE_CONTRACT_ARGS_BARE
#define GCHOLDER_DECLARE_CONTRACT_ARGS
#define GCHOLDER_DECLARE_CONTRACT_ARGS_INTERNAL
#define GCHOLDER_SETUP_CONTRACT_STACK_RECORD(mode)
#define GCHOLDER_CHECK_FOR_PREEMP_IN_NOTRIGGER(pThread)

#ifndef DACCESS_COMPILE
class GCHolderBase
{
protected:
    // NOTE: This method is FORCEINLINE'ed into its callers, but the callers are just the 
    // corresponding methods in the derived types, not all sites that use GC holders.  This
    // is done so that the #pragma optimize will take affect since the optimize settings
    // are taken from the template instantiation site, not the template definition site.
    template <BOOL THREAD_EXISTS>
    FORCEINLINE_NONDEBUG
    void PopInternal()
    {
        SCAN_SCOPE_END;
        WRAPPER_NO_CONTRACT;

#ifdef ENABLE_CONTRACTS_IMPL
        if (m_fPushedRecord)
        {
            *m_pClrDebugState = m_oldClrDebugState;
        }
        // Make sure that we're using the version of this template that matches the 
        // invariant setup in EnterInternal{Coop|Preemp}{_HackNoThread}
        _ASSERTE(!!THREAD_EXISTS == m_fThreadMustExist);
#endif

        if (m_WasCoop)
        {
            // m_WasCoop is only TRUE if we've already verified there's an EE thread.
            BEGIN_GETTHREAD_ALLOWED;

            _ASSERTE(m_Thread != NULL);  // Cannot switch to cooperative with no thread
            if (!m_Thread->PreemptiveGCDisabled())
                m_Thread->DisablePreemptiveGC();

            END_GETTHREAD_ALLOWED;
        }
        else
        {
            // Either we initialized m_Thread explicitly with GetThread() in the
            // constructor, or our caller (instantiator of GCHolder) called our constructor
            // with GetThread() (which we already asserted in the constuctor)
            // (i.e., m_Thread == GetThread()).  Also, note that if THREAD_EXISTS,
            // then m_Thread must be non-null (as it's == GetThread()).  So the
            // "if" below looks a little hokey since we're checking for either condition.
            // But the template param THREAD_EXISTS allows us to statically early-out
            // when it's TRUE, so we check it for perf.
            if (THREAD_EXISTS || m_Thread != NULL)
            {
                BEGIN_GETTHREAD_ALLOWED;
                if (m_Thread->PreemptiveGCDisabled())
                    m_Thread->EnablePreemptiveGC();
                END_GETTHREAD_ALLOWED;
            }
        }

        // If we have a thread then we assert that we ended up in the same state
        // which we started in.
        if (THREAD_EXISTS || m_Thread != NULL)
        {
            _ASSERTE(!!m_WasCoop == !!(m_Thread->PreemptiveGCDisabled()));
        }
    }

    // NOTE: The rest of these methods are all FORCEINLINE so that the uses where 'conditional==true' 
    // can have the if-checks removed by the compiler.  The callers are just the corresponding methods
    // in the derived types, not all sites that use GC holders.  

    
    // This is broken - there is a potential race with the GC thread.  It is currently
    // used for a few cases where (a) we potentially haven't started up the EE yet, or
    // (b) we are on a "special thread".  We need a real solution here though.
    FORCEINLINE_NONDEBUG 
    void EnterInternalCoop_HackNoThread(bool conditional GCHOLDER_DECLARE_CONTRACT_ARGS_INTERNAL)
    {
        GCHOLDER_SETUP_CONTRACT_STACK_RECORD(Contract::MODE_Coop);

        m_Thread = GetThreadNULLOk();

#ifdef ENABLE_CONTRACTS_IMPL
        m_fThreadMustExist = false;
#endif // ENABLE_CONTRACTS_IMPL

        if (m_Thread != NULL)
        {
            BEGIN_GETTHREAD_ALLOWED;
            m_WasCoop = m_Thread->PreemptiveGCDisabled();

            if (conditional && !m_WasCoop)
            {
                m_Thread->DisablePreemptiveGC();
                _ASSERTE(m_Thread->PreemptiveGCDisabled());
            }
            END_GETTHREAD_ALLOWED;
        }
        else
        {
            m_WasCoop = FALSE;
        }
    }

    FORCEINLINE_NONDEBUG 
    void EnterInternalPreemp(bool conditional GCHOLDER_DECLARE_CONTRACT_ARGS_INTERNAL)
    {
        GCHOLDER_SETUP_CONTRACT_STACK_RECORD(Contract::MODE_Preempt);

        m_Thread = GetThreadNULLOk();

#ifdef ENABLE_CONTRACTS_IMPL
        m_fThreadMustExist = false;
        if (m_Thread != NULL && conditional)
        {
            BEGIN_GETTHREAD_ALLOWED;
            GCHOLDER_CHECK_FOR_PREEMP_IN_NOTRIGGER(m_Thread);
            END_GETTHREAD_ALLOWED;
        }
#endif  // ENABLE_CONTRACTS_IMPL

        if (m_Thread != NULL)
        {
            BEGIN_GETTHREAD_ALLOWED;
            m_WasCoop = m_Thread->PreemptiveGCDisabled();

            if (conditional && m_WasCoop)
            {
                m_Thread->EnablePreemptiveGC();
                _ASSERTE(!m_Thread->PreemptiveGCDisabled());
            }
            END_GETTHREAD_ALLOWED;
        }
        else
        {
            m_WasCoop = FALSE;
        }
    }

    FORCEINLINE_NONDEBUG 
    void EnterInternalCoop(Thread *pThread, bool conditional GCHOLDER_DECLARE_CONTRACT_ARGS_INTERNAL)
    {
        // This is the perf version. So we deliberately restrict the calls
        // to already setup threads to avoid the null checks and GetThread call
        _ASSERTE(pThread && (pThread == GetThread()));
#ifdef ENABLE_CONTRACTS_IMPL
        m_fThreadMustExist = true;
#endif // ENABLE_CONTRACTS_IMPL

        GCHOLDER_SETUP_CONTRACT_STACK_RECORD(Contract::MODE_Coop);

        m_Thread = pThread;
        m_WasCoop = m_Thread->PreemptiveGCDisabled();
        if (conditional && !m_WasCoop)
        {
            m_Thread->DisablePreemptiveGC();
            _ASSERTE(m_Thread->PreemptiveGCDisabled());
        }
    }

    template <BOOL THREAD_EXISTS>
    FORCEINLINE_NONDEBUG 
    void EnterInternalPreemp(Thread *pThread, bool conditional GCHOLDER_DECLARE_CONTRACT_ARGS_INTERNAL)
    {
        // This is the perf version. So we deliberately restrict the calls
        // to already setup threads to avoid the null checks and GetThread call
        _ASSERTE(!THREAD_EXISTS || (pThread && (pThread == GetThread())));
#ifdef ENABLE_CONTRACTS_IMPL
        m_fThreadMustExist = !!THREAD_EXISTS;
#endif // ENABLE_CONTRACTS_IMPL

        GCHOLDER_SETUP_CONTRACT_STACK_RECORD(Contract::MODE_Preempt);

        m_Thread = pThread;

        if (THREAD_EXISTS || (m_Thread != NULL))
        {
            GCHOLDER_CHECK_FOR_PREEMP_IN_NOTRIGGER(m_Thread);
            m_WasCoop = m_Thread->PreemptiveGCDisabled();
            if (conditional && m_WasCoop)
            {
                m_Thread->EnablePreemptiveGC();
                _ASSERTE(!m_Thread->PreemptiveGCDisabled());
            }
        }
        else
        {
            m_WasCoop = FALSE;
        }
    }

private:
    Thread * m_Thread;
    BOOL     m_WasCoop;         // This is BOOL and not 'bool' because PreemptiveGCDisabled returns BOOL,
                                // so the codegen is better if we don't have to convert to 'bool'.
#ifdef ENABLE_CONTRACTS_IMPL
    bool                m_fThreadMustExist;     // used to validate that the proper Pop<THREAD_EXISTS> method is used
    bool                m_fPushedRecord;
    ClrDebugState       m_oldClrDebugState;
    ClrDebugState      *m_pClrDebugState;
    ContractStackRecord m_ContractStackRecord;
#endif
};

class GCCoopNoDtor : public GCHolderBase
{
public:
    DEBUG_NOINLINE 
    void Enter(bool conditional GCHOLDER_DECLARE_CONTRACT_ARGS)
    {
        WRAPPER_NO_CONTRACT;
        SCAN_SCOPE_BEGIN;
        if (conditional)
        {
            STATIC_CONTRACT_MODE_COOPERATIVE;
        }
        // The thread must be non-null to enter MODE_COOP
        this->EnterInternalCoop(GetThread(), conditional GCHOLDER_CONTRACT_ARGS_NoDtor);
    }

    DEBUG_NOINLINE 
    void Leave()
    {
        WRAPPER_NO_CONTRACT;
        SCAN_SCOPE_BEGIN;
        this->PopInternal<TRUE>();  // Thread must be non-NULL
    }
};

class GCPreempNoDtor : public GCHolderBase
{
public:
    DEBUG_NOINLINE 
    void Enter(bool conditional GCHOLDER_DECLARE_CONTRACT_ARGS)
    {
        SCAN_SCOPE_BEGIN;
        if (conditional)
        {
            STATIC_CONTRACT_MODE_PREEMPTIVE;
        }

        this->EnterInternalPreemp(conditional GCHOLDER_CONTRACT_ARGS_NoDtor);
    }

    DEBUG_NOINLINE 
    void Enter(Thread * pThreadNullOk, bool conditional GCHOLDER_DECLARE_CONTRACT_ARGS)
    {
        SCAN_SCOPE_BEGIN;
        if (conditional)
        {
            STATIC_CONTRACT_MODE_PREEMPTIVE;
        }

        this->EnterInternalPreemp<FALSE>( // Thread may be NULL
            pThreadNullOk, conditional GCHOLDER_CONTRACT_ARGS_NoDtor);
    }

    DEBUG_NOINLINE 
    void Leave()
    {
        SCAN_SCOPE_END;
        this->PopInternal<FALSE>(); // Thread may be NULL
    }
};

class GCCoop : public GCHolderBase
{
public:
    DEBUG_NOINLINE 
    GCCoop(GCHOLDER_DECLARE_CONTRACT_ARGS_BARE)
    {
        SCAN_SCOPE_BEGIN;
        STATIC_CONTRACT_MODE_COOPERATIVE;

        // The thread must be non-null to enter MODE_COOP
        this->EnterInternalCoop(GetThread(), true GCHOLDER_CONTRACT_ARGS_HasDtor);
    }

    DEBUG_NOINLINE 
    GCCoop(bool conditional GCHOLDER_DECLARE_CONTRACT_ARGS)
    {
        SCAN_SCOPE_BEGIN;
        if (conditional)
        {
            STATIC_CONTRACT_MODE_COOPERATIVE;
        }

        // The thread must be non-null to enter MODE_COOP
        this->EnterInternalCoop(GetThread(), conditional GCHOLDER_CONTRACT_ARGS_HasDtor);
    }

    DEBUG_NOINLINE
    ~GCCoop()
    {
        SCAN_SCOPE_END;
        this->PopInternal<TRUE>();  // Thread must be non-NULL
    }
};

// This is broken - there is a potential race with the GC thread.  It is currently
// used for a few cases where (a) we potentially haven't started up the EE yet, or
// (b) we are on a "special thread".  We need a real solution here though.
class GCCoopHackNoThread : public GCHolderBase
{
public:
    DEBUG_NOINLINE 
    GCCoopHackNoThread(GCHOLDER_DECLARE_CONTRACT_ARGS_BARE)
    {
        SCAN_SCOPE_BEGIN;
        STATIC_CONTRACT_MODE_COOPERATIVE;

        this->EnterInternalCoop_HackNoThread(true GCHOLDER_CONTRACT_ARGS_HasDtor);
    }

    DEBUG_NOINLINE 
    GCCoopHackNoThread(bool conditional GCHOLDER_DECLARE_CONTRACT_ARGS)
    {
        SCAN_SCOPE_BEGIN;
        if (conditional)
        {
            STATIC_CONTRACT_MODE_COOPERATIVE;
        }

        this->EnterInternalCoop_HackNoThread(conditional GCHOLDER_CONTRACT_ARGS_HasDtor);
    }

    DEBUG_NOINLINE
    ~GCCoopHackNoThread()
    {
        SCAN_SCOPE_END;
        this->PopInternal<FALSE>();  // Thread might be NULL
    }
};

class GCCoopThreadExists : public GCHolderBase
{
public:
    DEBUG_NOINLINE 
    GCCoopThreadExists(Thread * pThread GCHOLDER_DECLARE_CONTRACT_ARGS)
    {
        SCAN_SCOPE_BEGIN;
        STATIC_CONTRACT_MODE_COOPERATIVE;

        this->EnterInternalCoop(pThread, true GCHOLDER_CONTRACT_ARGS_HasDtor);
    }

    DEBUG_NOINLINE 
    GCCoopThreadExists(Thread * pThread, bool conditional GCHOLDER_DECLARE_CONTRACT_ARGS)
    {
        SCAN_SCOPE_BEGIN;
        if (conditional)
        {
            STATIC_CONTRACT_MODE_COOPERATIVE;
        }

        this->EnterInternalCoop(pThread, conditional GCHOLDER_CONTRACT_ARGS_HasDtor);
    }

    DEBUG_NOINLINE
    ~GCCoopThreadExists()
    {
        SCAN_SCOPE_END;
        this->PopInternal<TRUE>();  // Thread must be non-NULL
    }
};

class GCPreemp : public GCHolderBase
{
public:
    DEBUG_NOINLINE 
    GCPreemp(GCHOLDER_DECLARE_CONTRACT_ARGS_BARE)
    {
        SCAN_SCOPE_BEGIN;
        STATIC_CONTRACT_MODE_PREEMPTIVE;

        this->EnterInternalPreemp(true GCHOLDER_CONTRACT_ARGS_HasDtor);
    }

    DEBUG_NOINLINE 
    GCPreemp(bool conditional GCHOLDER_DECLARE_CONTRACT_ARGS)
    {
        SCAN_SCOPE_BEGIN;
        if (conditional)
        {
            STATIC_CONTRACT_MODE_PREEMPTIVE;
        }

        this->EnterInternalPreemp(conditional GCHOLDER_CONTRACT_ARGS_HasDtor);
    }

    DEBUG_NOINLINE
    ~GCPreemp()
    {
        SCAN_SCOPE_END;
        this->PopInternal<FALSE>(); // Thread may be NULL
    }
};

class GCPreempThreadExists : public GCHolderBase
{
public:
    DEBUG_NOINLINE 
    GCPreempThreadExists(Thread * pThread GCHOLDER_DECLARE_CONTRACT_ARGS)
    {
        SCAN_SCOPE_BEGIN;
        STATIC_CONTRACT_MODE_PREEMPTIVE;

        this->EnterInternalPreemp<TRUE>(    // Thread must be non-NULL
                pThread, true GCHOLDER_CONTRACT_ARGS_HasDtor);
    }

    DEBUG_NOINLINE 
    GCPreempThreadExists(Thread * pThread, bool conditional GCHOLDER_DECLARE_CONTRACT_ARGS)
    {
        SCAN_SCOPE_BEGIN;
        if (conditional)
        {
            STATIC_CONTRACT_MODE_PREEMPTIVE;
        }    

        this->EnterInternalPreemp<TRUE>(    // Thread must be non-NULL
                pThread, conditional GCHOLDER_CONTRACT_ARGS_HasDtor);
    }

    DEBUG_NOINLINE
    ~GCPreempThreadExists()
    {
        SCAN_SCOPE_END;
        this->PopInternal<TRUE>();  // Thread must be non-NULL
    }
};
#endif // DACCESS_COMPILE

#endif //__threads_h__
