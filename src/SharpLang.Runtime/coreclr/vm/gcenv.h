//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 
//

#ifndef GCENV_H_
#define GCENV_H_

#ifndef min
#define min(a,b) (((a) < (b)) ? (a) : (b))
#endif

#ifndef max
#define max(a,b) (((a) > (b)) ? (a) : (b))
#endif

#define MODE_COOPERATIVE
#define MODE_PREEMPTIVE
#define MODE_ANY
#define GC_TRIGGERS
#define SVAL_DECL(type, var) \
    static type var
#define SVAL_IMPL_INIT(type, cls, var, init) \
    type cls::var = init
#define SPTR_DECL(type, var) \
    static type* var
#define GPTR_DECL(type, var) \
    extern type* var
#define GPTR_IMPL(type, var) \
    type* var
#define GARY_DECL(type, var, size) \
    extern type var[size]
#define GVAL_DECL(type, var) \
    extern type var
#define GVAL_IMPL(type, var) \
    type var

#define _SPTR_DECL(acc_type, store_type, var) \
    static store_type var
#define _SPTR_IMPL(acc_type, store_type, cls, var) \
    store_type cls::var
#define _SPTR_IMPL_INIT(acc_type, store_type, cls, var, init) \
    store_type cls::var = init
    
#define SPTR_DECL(type, var) _SPTR_DECL(type*, PTR_##type, var)
#define SPTR_IMPL(type, cls, var) _SPTR_IMPL(type*, PTR_##type, cls, var)
#define SPTR_IMPL_INIT(type, cls, var, init) _SPTR_IMPL_INIT(type*, PTR_##type, cls, var, init)


enum CrstFlags
{
    CRST_DEFAULT	= 0,
    CRST_REENTRANCY	= 0x1,
    CRST_UNSAFE_SAMELEVEL	= 0x2,
    CRST_UNSAFE_COOPGC	= 0x4,
    CRST_UNSAFE_ANYMODE	= 0x8,
    CRST_DEBUGGER_THREAD	= 0x10,
    CRST_HOST_BREAKABLE	= 0x20,
    CRST_TAKEN_DURING_SHUTDOWN	= 0x80,
    CRST_GC_NOTRIGGER_WHEN_TAKEN	= 0x100,
    CRST_DEBUG_ONLY_CHECK_FORBID_SUSPEND_THREAD	= 0x200
};

#include "crsttypes.h"

/*typedef int CrstFlags;
typedef int CrstType;

void UnsafeInitializeCriticalSection(CRITICAL_SECTION * lpCriticalSection);
void UnsafeEEEnterCriticalSection(CRITICAL_SECTION *lpCriticalSection);
void UnsafeEELeaveCriticalSection(CRITICAL_SECTION * lpCriticalSection);
void UnsafeDeleteCriticalSection(CRITICAL_SECTION *lpCriticalSection);

#define CRST_REENTRANCY         0
#define CRST_UNSAFE_SAMELEVEL   0
#define CRST_UNSAFE_ANYMODE     0
#define CRST_DEBUGGER_THREAD    0
#define CRST_DEFAULT            0

class CrstStatic
{
    CRITICAL_SECTION m_cs;
#ifdef _DEBUG
    UINT32 m_holderThreadId;
#endif

public:
    bool InitNoThrow(CrstType eType, CrstFlags eFlags = CRST_DEFAULT)
    {
        UnsafeInitializeCriticalSection(&m_cs);
        return true;
    }

    void Destroy()
    {
        UnsafeDeleteCriticalSection(&m_cs);
    }

    void Enter()
    {
        UnsafeEEEnterCriticalSection(&m_cs);
#ifdef _DEBUG
        m_holderThreadId = GetCurrentThreadId();
#endif
    }

    void Leave()
    {
#ifdef _DEBUG
        m_holderThreadId = 0;
#endif
        UnsafeEELeaveCriticalSection(&m_cs);
    }

#ifdef _DEBUG
    EEThreadId GetHolderThreadId()
    {
        return m_holderThreadId;
    }

    bool OwnedByCurrentThread()
    {
        return GetHolderThreadId().IsSameThread();
    }
#endif
};

class CrstHolder
{
    CrstStatic * m_pLock;

public:
    CrstHolder(CrstStatic * pLock)
        : m_pLock(pLock)
    {
        m_pLock->Enter();
    }

    ~CrstHolder()
    {
        m_pLock->Leave();
    }
};

class FinalizerThread
{
public:
    static void EnableFinalization() {}
    
    static void FinalizerThreadWait() {}

    static bool HaveExtraWorkForFinalizer()
    {
        return false;
    }
};*/

//
// Performance logging
//

#define COUNTER_ONLY(x)

#include "../gc/sample/etmdummy.h"

#define ETW_EVENT_ENABLED(e,f) false

// Various types used to refer to object references or handles. This will get more complex if we decide
// Redhawk wants to wrap object references in the debug build.
typedef DPTR(Object) PTR_Object;
typedef DPTR(PTR_Object) PTR_PTR_Object;

struct ScanContext;
typedef void promote_func(PTR_PTR_Object, ScanContext*, unsigned);

struct alloc_context;

#endif // GCENV_H_
