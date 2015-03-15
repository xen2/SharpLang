#ifndef _OBJECT_H_
#define _OBJECT_H_

#include "../../RuntimeType.h"

// Make sure to include utility before redefining __in (__in is used in mingw32 stdc++ for std::move)
#include "clr_std/utility"

// securecrt
#define __in
#define __in_z
#define __in_opt
#define __in_bcount(x)
#define __in_ecount(x)
#define __in_ecount_opt(x)
#define __out
#define __out_z
#define __out_opt
#define __out_bcount(x)
#define __out_ecount(x)
#define __out_ecount_opt(x)
#define __deref_out_z
#define __deref_out_opt
#define __inout
#define __inout_z
#define __annotation(x)
#define __assume(x) (void)0
#define __success(expr)

#include "contract.h"
#include "holder.h"
#include "gcenv.h"

// #include "corinfo.h"
typedef SIZE_T GSCookie;

#include "util.hpp"

#include "daccess.h"
#include "utilcode.h"

// #include "pal.h"
#ifdef _MSC_VER
#define PAL_NORETURN __declspec(noreturn)
#else
#define PAL_NORETURN
#endif
#define DECLSPEC_NORETURN   PAL_NORETURN

#include "ex.h"

// #include "exceptmacros.h"
#define INSTALL_UNWIND_AND_CONTINUE_HANDLER
#define UNINSTALL_UNWIND_AND_CONTINUE_HANDLER

#include "sstring.h"

#define SetObjectReference(_d,_r,_a)  (*(_d) = _r)

class PtrArray;
class SafeHandle;

class MethodDesc
{
};

class Module : public Object
{
};

class Signature
{
};

class SigBuilder
{
};

typedef Array<WCHAR>   CHARArray;
typedef Array<uint8_t> U1Array;
typedef Array<int32_t> I4Array;

typedef bool CLR_BOOL;
typedef uint8_t  U1;
typedef int8_t   I1;
typedef uint16_t U2;
typedef int16_t  I2;
typedef uint32_t U4;
typedef int32_t  I4;
typedef uint64_t U8;
typedef int64_t  I8;
typedef float    R4;
typedef double   R8;
typedef Object* OBJECTREF;
typedef StringObject* STRINGREF;
typedef Array<uint8_t>* U1ARRAYREF;
typedef Array<int32_t>* I4ARRAYREF;
typedef PtrArray* PTRARRAYREF;
typedef SafeHandle * SAFEHANDLE;
typedef SafeHandle * SAFEHANDLEREF;
typedef ArrayBase*   BASEARRAYREF;

typedef const char*     PTR_CSTR;
typedef BYTE*           PTR_BYTE;
typedef Object*         PTR_Object;
typedef MethodTable*    PTR_MethodTable;
typedef MethodTable*    TypeHandle;
typedef FieldDesc*      PTR_FieldDesc;
typedef MethodDesc*      PTR_MethodDesc;
typedef Module*         PTR_Module;

//
// _UNCHECKED_OBJECTREF is for code that can't deal with DEBUG OBJECTREFs
//
typedef PTR_Object _UNCHECKED_OBJECTREF;
typedef PTR_Object* PTR_UNCHECKED_OBJECTREF;

class MarshalByRefObjectBaseObject : public Object
{
	// FEATURE_REMOTING not enabled, so nothing to do
};

class SafeHandle : public Object
{
private:
	// READ ME:
    //   Modifying the order or fields of this object may require
    //   other changes to the classlib class definition of this
    //   object or special handling when loading this system class.
#ifdef _DEBUG
    STRINGREF m_debugStackTrace;   // Where we allocated this SafeHandle
#endif
    Volatile<LPVOID> m_handle;
    Volatile<INT32> m_state;        // Combined ref count and closed/disposed state (for atomicity)
    Volatile<CLR_BOOL> m_ownsHandle;
    Volatile<CLR_BOOL> m_fullyInitialized;  // Did constructor finish?

    // Describe the bits in the m_state field above.
    enum StateBits
    {
        SH_State_Closed = 0x00000001,
        SH_State_Disposed = 0x00000002,
        SH_State_RefCount = 0xfffffffc,
        SH_RefCountOne = 4,            // Amount to increment state field to yield a ref count increment of 1
    };

    static WORD s_IsInvalidHandleMethodSlot;
public:
    LPVOID GetHandle() const { 
        LIMITED_METHOD_CONTRACT;
        _ASSERTE(((unsigned int) m_state) >= SH_RefCountOne);
        return m_handle;
    }

	void AddRef();
	void Release(bool fDispose = false);
	void SetHandle(LPVOID handle);
};

// SAFEHANDLEREF defined above because CompressedStackObject needs it

void AcquireSafeHandle(SAFEHANDLEREF* s);
void ReleaseSafeHandle(SAFEHANDLEREF* s);

typedef Holder<SAFEHANDLEREF*, AcquireSafeHandle, ReleaseSafeHandle> SafeHandleHolder;

// WaitHandleBase
// Base class for WaitHandle 
class WaitHandleBase : public MarshalByRefObjectBaseObject
{
	friend class WaitHandleNative;
	friend class MscorlibBinder;

public:
	__inline LPVOID GetWaitHandle() { return m_handle; }
	__inline SAFEHANDLEREF GetSafeHandle() { return m_safeHandle; }

private:
	SAFEHANDLEREF   m_safeHandle;
	LPVOID          m_handle;
	CLR_BOOL        m_hasThreadAffinity;
};

typedef WaitHandleBase* WAITHANDLEREF;

class PtrArray : public Array<Object*>
{
public:
    void SetAt(size_t i, OBJECTREF ref)
    {
        CONTRACTL
        {
            NOTHROW;
            GC_NOTRIGGER;
            SO_TOLERANT;
            MODE_COOPERATIVE;
        }
        CONTRACTL_END;
        _ASSERTE(i < GetNumComponents());
        SetObjectReference(value + i, ref, GetAppDomain());
    }
    
    OBJECTREF GetAt(size_t i)
    {
        LIMITED_METHOD_CONTRACT;
        SUPPORTS_DAC;
        _ASSERTE(i < GetNumComponents());

        return value[i];
    }
};

class AppDomain;
class DomainAssembly;
class Module;
class Thread;
class LoaderAllocator;

/* a TypedByRef is a structure that is used to implement VB's BYREF variants.  
   it is basically a tuple of an address of some data along with a TypeHandle
   that indicates the type of the address */
class TypedByRef 
{
public:

    PTR_VOID data;
    EEType* type;  
};

enum StackCrawlMark
{
    LookForMe = 0,
    LookForMyCaller = 1,
    LookForMyCallersCaller = 2,
    LookForThread = 3
};

class NumberFormatInfo: public Object
{
public:
    // C++ data members                 // Corresponding data member in NumberFormatInfo.cs
                                        // Also update mscorlib.h when you add/remove fields

    I4ARRAYREF cNumberGroup;        // numberGroupSize
    I4ARRAYREF cCurrencyGroup;      // currencyGroupSize
    I4ARRAYREF cPercentGroup;       // percentGroupSize
    
    STRINGREF sPositive;            // positiveSign
    STRINGREF sNegative;            // negativeSign
    STRINGREF sNumberDecimal;       // numberDecimalSeparator
    STRINGREF sNumberGroup;         // numberGroupSeparator
    STRINGREF sCurrencyGroup;       // currencyDecimalSeparator
    STRINGREF sCurrencyDecimal;     // currencyGroupSeparator
    STRINGREF sCurrency;            // currencySymbol
#ifndef FEATURE_COREFX_GLOBALIZATION
    STRINGREF sAnsiCurrency;        // ansiCurrencySymbol
#endif
    STRINGREF sNaN;                 // nanSymbol
    STRINGREF sPositiveInfinity;    // positiveInfinitySymbol
    STRINGREF sNegativeInfinity;    // negativeInfinitySymbol
    STRINGREF sPercentDecimal;      // percentDecimalSeparator
    STRINGREF sPercentGroup;        // percentGroupSeparator
    STRINGREF sPercent;             // percentSymbol
    STRINGREF sPerMille;            // perMilleSymbol

    PTRARRAYREF sNativeDigits;      // nativeDigits (a string array)

#ifndef FEATURE_COREFX_GLOBALIZATION    
    INT32 iDataItem;                // Index into the CultureInfo Table.  Only used from managed code.
#endif
    INT32 cNumberDecimals;          // numberDecimalDigits
    INT32 cCurrencyDecimals;        // currencyDecimalDigits
    INT32 cPosCurrencyFormat;       // positiveCurrencyFormat
    INT32 cNegCurrencyFormat;       // negativeCurrencyFormat
    INT32 cNegativeNumberFormat;    // negativeNumberFormat
    INT32 cPositivePercentFormat;   // positivePercentFormat
    INT32 cNegativePercentFormat;   // negativePercentFormat
    INT32 cPercentDecimals;         // percentDecimalDigits
#ifndef FEATURE_CORECLR    
    INT32 iDigitSubstitution;       // digitSubstitution
#endif    

    CLR_BOOL bIsReadOnly;              // Is this NumberFormatInfo ReadOnly?
#ifndef FEATURE_COREFX_GLOBALIZATION
    CLR_BOOL bUseUserOverride;         // Flag to use user override. Only used from managed code.
#endif
    CLR_BOOL bIsInvariant;             // Is this the NumberFormatInfo for the Invariant Culture?
#ifndef FEATURE_CORECLR    
    CLR_BOOL bvalidForParseAsNumber;   // NEVER USED, DO NOT USE THIS! (Serialized in Whidbey/Everett)
    CLR_BOOL bvalidForParseAsCurrency; // NEVER USED, DO NOT USE THIS! (Serialized in Whidbey/Everett)
#endif // !FEATURE_CORECLR
};

typedef NumberFormatInfo * NUMFMTREF;

// ARG_SLOT
typedef unsigned __int64 ARG_SLOT;

#define ObjToArgSlot(objref) ((ARG_SLOT)(SIZE_T)(objref))
#define ArgSlotToObj(s) ((OBJECTREF)(SIZE_T)(s))

#define StringToArgSlot(objref) ((ARG_SLOT)(SIZE_T)(objref))
#define ArgSlotToString(s)    ((STRINGREF)(SIZE_T)(s))

#define PtrToArgSlot(ptr) ((ARG_SLOT)(SIZE_T)(ptr))
#define ArgSlotToPtr(s)   ((LPVOID)(SIZE_T)(s))

#define BoolToArgSlot(b)  ((ARG_SLOT)(CLR_BOOL)(!!(b)))
#define ArgSlotToBool(s)  ((BOOL)(s))

#endif // _OBJECT_H_
