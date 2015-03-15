#ifndef _H_UTIL
#define _H_UTIL

#include "winwrap.h"

#define GCX_COOP()
#define GCX_PREEMP()
#define GCX_COOP_NO_THREAD_BROKEN()
#define GCX_MAYBE_COOP_NO_THREAD_BROKEN(_cond)
#define GCX_ASSERT_PREEMP()


typedef BOOL (*FnLockOwner)(LPVOID);
struct LockOwner
{
    LPVOID lock;
    FnLockOwner lockOwnerFunc;
};

#define ENSURE_OLEAUT32_LOADED()

inline BOOL CLRTaskHosted()
{
    LIMITED_METHOD_DAC_CONTRACT;
    return FALSE;
}

//
//
// COMCHARACTER
//
//
class COMCharacter {
public:
    //These are here for support from native code.  They are never called from our managed classes.
    static BOOL nativeIsWhiteSpace(WCHAR c);
    static BOOL nativeIsDigit(WCHAR c);
};

#define FastInterlockIncrement              InterlockedIncrement
#define FastInterlockDecrement              InterlockedDecrement
#define FastInterlockExchange               InterlockedExchange
#define FastInterlockCompareExchange        InterlockedCompareExchange
#define FastInterlockExchangeAdd            InterlockedExchangeAdd
#define FastInterlockExchangeLong           InterlockedExchange64
#define FastInterlockCompareExchangeLong    InterlockedCompareExchange64
#define FastInterlockExchangeAddLong        InterlockedExchangeAdd64

//
// Forward FastInterlock[Compare]ExchangePointer to the 
// Utilcode Interlocked[Compare]ExchangeT.
// 
#define FastInterlockExchangePointer        InterlockedExchangeT
#define FastInterlockCompareExchangePointer InterlockedCompareExchangeT

FORCEINLINE void FastInterlockOr(DWORD RAW_KEYWORD(volatile) *p, const int msk)
{
    LIMITED_METHOD_CONTRACT;

    InterlockedOr((LONG *)p, msk);
}

FORCEINLINE void FastInterlockAnd(DWORD RAW_KEYWORD(volatile) *p, const int msk)
{
    LIMITED_METHOD_CONTRACT;

    InterlockedAnd((LONG *)p, msk);
}

#define IncCantStopCount()
#define DecCantStopCount()

#ifdef _DEBUG
#define FORCEINLINE_NONDEBUG
#else
#define FORCEINLINE_NONDEBUG FORCEINLINE
#endif

#endif /* _H_UTIL */
