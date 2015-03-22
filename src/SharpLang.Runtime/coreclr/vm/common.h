#ifndef _common_h_ 
#define _common_h_

#include <math.h>

#ifdef _WIN32
#define _copysign copysign
#define _isnan isnan
#endif

#include "object.h"
#include "contract.h"
#include "holder.h"
#include "stackprobe.h"

// make all the unsafe redefinitions available
#include "unsafe.h"

typedef DPTR(class Object)              PTR_Object;
typedef DPTR(class StringObject)        PTR_StringObject;

// #include "eeconfig.h"
class EEConfig
{
public:
	bool EnforceFIPSPolicy() { return true; }
	bool LegacyHMACMode() { return false; }
};

// #include "gchelpers.h"
OBJECTREF AllocatePrimitiveArray(CorElementType type, DWORD cElements, BOOL bAllocateInLargeHeap = FALSE);
OBJECTREF AllocateValueSzArray(TypeHandle elementType, INT32 length);

// #include "jitinterface.h"
EXTERN_C void DoJITFailFast ();

// #include "ceeload.h"
//
// Flags used to control the Runtime's debugging modes. These indicate to
// the Runtime that it needs to load the Runtime Controller, track data
// during JIT's, etc.
//
enum DebuggerControlFlag
{
    DBCF_NORMAL_OPERATION           = 0x0000,

    DBCF_USER_MASK                  = 0x00FF,
    DBCF_GENERATE_DEBUG_CODE        = 0x0001,
    DBCF_ALLOW_JIT_OPT              = 0x0008,
    DBCF_PROFILER_ENABLED           = 0x0020,
//    DBCF_ACTIVATE_REMOTE_DEBUGGING  = 0x0040,  Deprecated.  DO NOT USE

    DBCF_INTERNAL_MASK              = 0xFF00,
    DBCF_PENDING_ATTACH             = 0x0100,
    DBCF_ATTACHED                   = 0x0200,
    DBCF_FIBERMODE                  = 0x0400
};

#define GC_TRIGGERS

EXTERN_C Thread* STDCALL GetThread();
BOOL SetThread(Thread*);

#define BEGIN_SO_INTOLERANT_CODE_NOTHROW(pThread, ActionOnSO)
#define END_SO_INTOLERANT_CODE

#define GCPROTECT_BEGIN(ObjRefStruct)
#define GCPROTECT_END()

// #include "ibclogger.h"
class IBCLogger {};
typedef PTR_VOID HashDatum;

#include "vars.hpp"
#include "util.hpp"

#include "fcall.h"
#include "qcall.h"

// #include "clrex.h"
struct ExceptionData;

#include "ex.h"

// various macros
#ifndef NOINLINE
#ifdef _MSC_VER
#define NOINLINE __declspec(noinline)
#elif defined __GNUC__
#define NOINLINE __attribute__ ((noinline))
#else
#define NOINLINE
#endif
#endif // !NOINLINE

#ifndef ASSERT 
#define ASSERT _ASSERTE
#endif

// #include "clrinternal.h"
typedef void *CRITSEC_COOKIE;

#include "threads.h"

//
// By default logging, and debug GC are enabled under debug
//
// These can be enabled in non-debug by removing the #ifdef _DEBUG
// allowing one to log/check_gc a free build.
//
#if defined(_DEBUG) && !defined(DACCESS_COMPILE) && !defined(CROSSGEN_COMPILE)

        // You should be using CopyValueClass if you are doing an memcpy
        // in the CG heap.
    #if !defined(memcpy) 
    FORCEINLINE void* memcpyNoGCRefs(void * dest, const void * src, size_t len) {
            WRAPPER_NO_CONTRACT;
            return memcpy(dest, src, len);
        }
    extern "C" void *  __cdecl GCSafeMemCpy(void *, const void *, size_t);
    #define memcpy(dest, src, len) GCSafeMemCpy(dest, src, len)
    #endif // !defined(memcpy)

    #if !defined(CHECK_APP_DOMAIN_LEAKS)
    #define CHECK_APP_DOMAIN_LEAKS 1
    #endif
#else // !_DEBUG && !DACCESS_COMPILE && !CROSSGEN_COMPILE
    FORCEINLINE void* memcpyNoGCRefs(void * dest, const void * src, size_t len) {
            WRAPPER_NO_CONTRACT;
            
            return memcpy(dest, src, len);
        }
#endif // !_DEBUG && !DACCESS_COMPILE && !CROSSGEN_COMPILE

#include "log.h"
#include "crst.h"
#include "binder.h"
#include "excep.h"

#include "perfcounters.h"
#define GetClrInstanceId() (0)

// #include "callhelpers.h"
class MethodDescCallSite
{
public:
    MethodDescCallSite(BinderMethodID id, OBJECTREF* porProtectedThis = NULL) {}

	void Call(const ARG_SLOT* pArguments) { assert(false); }
};

#include "threads.h"

#include "spinlock.h"

#include "eehash.inl"

extern INT64 g_PauseTime;          // Total duration of all pauses in the runtime

EXTERN_C Thread* STDCALL GetThread();
BOOL SetThread(Thread*);

// This is a mechanism by which macros can make the Thread pointer available to inner scopes
// that is robust to code changes.  If the outer Thread no longer is available for some reason
// (e.g. code refactoring), this GET_THREAD() macro will fall back to calling GetThread().
const bool CURRENT_THREAD_AVAILABLE = false;
Thread * const CURRENT_THREAD = NULL;
#define GET_THREAD() (CURRENT_THREAD_AVAILABLE ? CURRENT_THREAD : GetThread())

#define MAKE_CURRENT_THREAD_AVAILABLE() \
    Thread * __pThread = GET_THREAD(); \
    MAKE_CURRENT_THREAD_AVAILABLE_EX(__pThread)

#define MAKE_CURRENT_THREAD_AVAILABLE_EX(__pThread) \
    Thread * CURRENT_THREAD = __pThread; \
    const bool CURRENT_THREAD_AVAILABLE = true; \
    (void)CURRENT_THREAD_AVAILABLE; /* silence "local variable initialized but not used" warning */ \

#endif // !_common_h_
