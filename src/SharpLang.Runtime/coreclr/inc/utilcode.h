//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
//*****************************************************************************
// UtilCode.h
//
// Utility functions implemented in UtilCode.lib.
//
//*****************************************************************************

#ifndef __UtilCode_h__
#define __UtilCode_h__

#include "corhlprpriv.h"
#include "clrtypes.h"
#include "safemath.h"

// #include "clrhost.h"
#define ClrSleepEx SleepEx

#define IS_DIGIT(ch) ((ch >= W('0')) && (ch <= W('9')))
#define DIGIT_TO_INT(ch) (ch - W('0'))
#define INT_TO_DIGIT(i) ((WCHAR)(W('0') + i))

#ifndef NumItems
// Number of elements in a fixed-size array
#define NumItems(s) (sizeof(s) / sizeof(s[0]))
#endif

#define CLRGetTickCount64() GetTickCount64()

//*****************************************************************************
// Placement new is used to new and object at an exact location.  The pointer
// is simply returned to the caller without actually using the heap.  The
// advantage here is that you cause the ctor() code for the object to be run.
// This is ideal for heaps of C++ objects that need to get init'd multiple times.
// Example:
//      void        *pMem = GetMemFromSomePlace();
//      Foo *p = new (pMem) Foo;
//      DoSomething(p);
//      p->~Foo();
//*****************************************************************************
#ifndef __PLACEMENT_NEW_INLINE
#define __PLACEMENT_NEW_INLINE
inline void *__cdecl operator new(size_t, void *_P)
{
    LIMITED_METHOD_DAC_CONTRACT;

    return (_P);
}
#endif // __PLACEMENT_NEW_INLINE

// Used to remove trailing zeros from Decimal types.
// NOTE: Assumes hi32 bits are empty (used for conversions from Cy->Dec)
inline HRESULT DecimalCanonicalize(DECIMAL* dec)
{
    WRAPPER_NO_CONTRACT;

    // Clear the VARENUM field
    (*(USHORT*)dec) = 0;

    // Remove trailing zeros:
    DECIMAL temp;
    DECIMAL templast;
    temp = templast = *dec;

    // Ensure the hi 32 bits are empty (should be if we came from a currency)
    if ((DECIMAL_HI32(temp) != 0) || (DECIMAL_SCALE(temp) > 4))
        return DISP_E_OVERFLOW;

    // Return immediately if dec represents a zero.
    if (DECIMAL_LO32(temp) == 0 && DECIMAL_MID32(temp) == 0)
        return S_OK;

    // Compare to the original to see if we've
    // lost non-zero digits (and make sure we don't overflow the scale BYTE)

#ifdef _PREFAST_
#pragma warning(push)
#pragma warning(disable:6219) // "Suppress PREFast warning about Implicit cast between semantically different integer types" 
#endif
    while ((DECIMAL_SCALE(temp) <= 4) && (VARCMP_EQ == VarDecCmp(dec, &temp)))
    {

#ifdef _PREFAST_
#pragma warning(pop)
#endif
        templast = temp;

        // Remove the last digit and normalize.  Ignore temp.Hi32
        // as Currency values will have a max of 64 bits of data.
        DECIMAL_SCALE(temp)--;
        UINT64 temp64 = (((UINT64) DECIMAL_MID32(temp)) << 32) + DECIMAL_LO32(temp);
        temp64 /= 10;

        DECIMAL_MID32(temp) = (ULONG)(temp64 >> 32);
        DECIMAL_LO32(temp) = (ULONG)temp64;
    }
    *dec = templast;

    return S_OK;
}

//*****************************************************************************
// Handle accessing localizable resource strings
//*****************************************************************************
// NOTE: Should use locale names as much as possible.  LCIDs don't support
// custom cultures on Vista+.
// TODO: This should always use the names
#ifdef FEATURE_USE_LCID    
typedef LCID LocaleID;
typedef LCID LocaleIDValue;
#else
typedef LPCWSTR LocaleID;
typedef WCHAR LocaleIDValue[LOCALE_NAME_MAX_LENGTH];
#endif

//*****************************************************************************
//
// **** REGUTIL - Static helper functions for reading/writing to Windows registry.
//
//*****************************************************************************


class REGUTIL
{
public:
//*****************************************************************************

    enum CORConfigLevel
    {
        COR_CONFIG_ENV          = 0x01,
        COR_CONFIG_USER         = 0x02,
        COR_CONFIG_MACHINE      = 0x04,
        COR_CONFIG_FUSION       = 0x08,

        COR_CONFIG_REGISTRY     = (COR_CONFIG_USER|COR_CONFIG_MACHINE|COR_CONFIG_FUSION),
        COR_CONFIG_ALL          = (COR_CONFIG_ENV|COR_CONFIG_USER|COR_CONFIG_MACHINE),
    };

    //
    // NOTE: The following function is deprecated; use the CLRConfig class instead. 
    // To access a configuration value through CLRConfig, add an entry in file:../inc/CLRConfigValues.h.
    // 
    static DWORD GetConfigDWORD_DontUse_(
        LPCWSTR        name,
        DWORD          defValue,
        CORConfigLevel level = COR_CONFIG_ALL,
        BOOL           fPrependCOMPLUS = TRUE);

    //
    // NOTE: The following function is deprecated; use the CLRConfig class instead. 
    // To access a configuration value through CLRConfig, add an entry in file:../inc/CLRConfigValues.h.
    // 
    static HRESULT GetConfigDWORD_DontUse_(
        LPCWSTR name,
        DWORD defValue,
        __out DWORD * result,
        CORConfigLevel level = COR_CONFIG_ALL,
        BOOL fPrependCOMPLUS = TRUE);
    
    static ULONGLONG GetConfigULONGLONG_DontUse_(
        LPCWSTR        name,
        ULONGLONG      defValue,
        CORConfigLevel level = COR_CONFIG_ALL,
        BOOL           fPrependCOMPLUS = TRUE);

    //
    // NOTE: The following function is deprecated; use the CLRConfig class instead. 
    // To access a configuration value through CLRConfig, add an entry in file:../inc/CLRConfigValues.h.
    // 
    static DWORD GetConfigFlag_DontUse_(
        LPCWSTR        name,
        DWORD          bitToSet,
        BOOL           defValue = FALSE);

    //
    // NOTE: The following function is deprecated; use the CLRConfig class instead. 
    // To access a configuration value through CLRConfig, add an entry in file:../inc/CLRConfigValues.h.
    // 
    static LPWSTR GetConfigString_DontUse_(
        LPCWSTR name,
        BOOL fPrependCOMPLUS = TRUE,
        CORConfigLevel level = COR_CONFIG_ALL,
        BOOL fUsePerfCache = TRUE);

    static void   FreeConfigString(__in __in_z LPWSTR name);

#ifdef FEATURE_CORECLR
private:
#endif //FEATURE_CORECLR
    static LPWSTR EnvGetString(LPCWSTR name, BOOL fPrependCOMPLUS);
#ifdef FEATURE_CORECLR
public:
#endif //FEATURE_CORECLR

    static BOOL UseRegistry();

private:
//*****************************************************************************
// Get either a DWORD or ULONGLONG. Always puts the result in a ULONGLONG that
// you can safely cast to a DWORD if fGetDWORD is TRUE.
//*****************************************************************************    
    static HRESULT GetConfigInteger(
        LPCWSTR name,
        ULONGLONG defValue,
        __out ULONGLONG * result,
        BOOL fGetDWORD = TRUE,
        CORConfigLevel level = COR_CONFIG_ALL,
        BOOL fPrependCOMPLUS = TRUE);
public:

#ifndef FEATURE_CORECLR
    static void AllowRegistryUse(BOOL fAllowUse);


//*****************************************************************************
// Open's the given key and returns the value desired.  If the key or value is
// not found, then the default is returned.
//*****************************************************************************
    static long GetLong(                    // Return value from registry or default.
        LPCTSTR     szName,                 // Name of value to get.
        long        iDefault,               // Default value to return if not found.
        LPCTSTR     szKey=NULL,             // Name of key, NULL==default.
        HKEY        hKey=HKEY_LOCAL_MACHINE);// What key to work on.

//*****************************************************************************
// Open's the given key and returns the value desired.  If the key or value is
// not found, then the default is returned.
//*****************************************************************************
    static long SetLong(                    // Return value from registry or default.
        LPCTSTR     szName,                 // Name of value to get.
        long        iValue,                 // Value to set.
        LPCTSTR     szKey=NULL,             // Name of key, NULL==default.
        HKEY        hKey=HKEY_LOCAL_MACHINE);// What key to work on.

//*****************************************************************************
// Open's the given key and returns the value desired.  If the key or value is
// not found, then it's created
//*****************************************************************************
    static long SetOrCreateLong(            // Return value from registry or default.
        LPCTSTR     szName,                 // Name of value to get.
        long        iValue,                 // Value to set.
        LPCTSTR     szKey=NULL,             // Name of key, NULL==default.
        HKEY        hKey=HKEY_LOCAL_MACHINE);// What key to work on.



//*****************************************************************************
// Set an entry in the registry of the form:
// HKEY_CLASSES_ROOT\szKey\szSubkey = szValue.  If szSubkey or szValue are
// NULL, omit them from the above expression.
//*****************************************************************************
    static BOOL SetKeyAndValue(             // TRUE or FALSE.
        LPCTSTR     szKey,                  // Name of the reg key to set.
        LPCTSTR     szSubkey,               // Optional subkey of szKey.
        LPCTSTR     szValue);               // Optional value for szKey\szSubkey.

//*****************************************************************************
// Delete an entry in the registry of the form:
// HKEY_CLASSES_ROOT\szKey\szSubkey.
//*****************************************************************************
    static LONG DeleteKey(                  // TRUE or FALSE.
        LPCTSTR     szKey,                  // Name of the reg key to set.
        LPCTSTR     szSubkey);              // Subkey of szKey.

//*****************************************************************************
// Open the key, create a new keyword and value pair under it.
//*****************************************************************************
    static BOOL SetRegValue(                // Return status.
        LPCTSTR     szKeyName,              // Name of full key.
        LPCTSTR     szKeyword,              // Name of keyword.
        LPCTSTR     szValue);               // Value of keyword.

//*****************************************************************************
// Does standard registration of a CoClass with a progid.
//*****************************************************************************
    static HRESULT RegisterCOMClass(        // Return code.
        REFCLSID    rclsid,                 // Class ID.
        LPCTSTR     szDesc,                 // Description of the class.
        LPCTSTR     szProgIDPrefix,         // Prefix for progid.
        int         iVersion,               // Version # for progid.
        LPCTSTR     szClassProgID,          // Class progid.
        LPCTSTR     szThreadingModel,       // What threading model to use.
        LPCTSTR     szModule,               // Path to class.
        HINSTANCE   hInst,                  // Handle to module being registered
        LPCTSTR     szAssemblyName,         // Optional assembly name
        LPCTSTR     szVersion,              // Optional Runtime Version (directry containing runtime)
        BOOL        fExternal,              // flag - External to mscoree.
        BOOL        fRelativePath);         // flag - Relative path in szModule

//*****************************************************************************
// Unregister the basic information in the system registry for a given object
// class.
//*****************************************************************************
    static HRESULT UnregisterCOMClass(      // Return code.
        REFCLSID    rclsid,                 // Class ID we are registering.
        LPCTSTR     szProgIDPrefix,         // Prefix for progid.
        int         iVersion,               // Version # for progid.
        LPCTSTR     szClassProgID,          // Class progid.
        BOOL        fExternal);             // flag - External to mscoree.

//*****************************************************************************
// Does standard registration of a CoClass with a progid.
// NOTE: This is the non-side-by-side execution version.
//*****************************************************************************
    static HRESULT RegisterCOMClass(        // Return code.
        REFCLSID    rclsid,                 // Class ID.
        LPCTSTR     szDesc,                 // Description of the class.
        LPCTSTR     szProgIDPrefix,         // Prefix for progid.
        int         iVersion,               // Version # for progid.
        LPCTSTR     szClassProgID,          // Class progid.
        LPCTSTR     szThreadingModel,       // What threading model to use.
        LPCTSTR     szModule,               // Path to class.
        BOOL        bInprocServer = true);  // Whether we register the server as inproc or local

//*****************************************************************************
// Unregister the basic information in the system registry for a given object
// class.
// NOTE: This is the non-side-by-side execution version.
//*****************************************************************************
    static HRESULT UnregisterCOMClass(      // Return code.
        REFCLSID    rclsid,                 // Class ID we are registering.
        LPCTSTR     szProgIDPrefix,         // Prefix for progid.
        int         iVersion,               // Version # for progid.
        LPCTSTR     szClassProgID);         // Class progid.

//*****************************************************************************
// Register a type library.
//*****************************************************************************
    static HRESULT RegisterTypeLib(         // Return code.
        REFGUID     rtlbid,                 // TypeLib ID we are registering.
        int         iVersion,               // Typelib version.
        LPCTSTR     szDesc,                 // TypeLib description.
        LPCTSTR     szModule);              // Path to the typelib.

//*****************************************************************************
// Remove the registry keys for a type library.
//*****************************************************************************
    static HRESULT UnregisterTypeLib(       // Return code.
        REFGUID     rtlbid,                 // TypeLib ID we are registering.
        int         iVersion);              // Typelib version.

#endif //#ifndef FEATURE_CORECLR

//*****************************************************************************
// (Optional) Initialize the config registry cache
// (see ConfigCacheValueNameSeenPerhaps, below.)
//*****************************************************************************
    static void InitOptionalConfigCache();

private:

#ifndef FEATURE_CORECLR

//*****************************************************************************
// Register the basics for a in proc server.
//*****************************************************************************
    static HRESULT RegisterClassBase(       // Return code.
        REFCLSID    rclsid,                 // Class ID we are registering.
        LPCTSTR     szDesc,                 // Class description.
        LPCTSTR     szProgID,               // Class prog ID.
        LPCTSTR     szIndepProgID,          // Class version independant prog ID.
        __out_ecount (cchOutCLSID) LPTSTR szOutCLSID, // CLSID formatted in character form.
        DWORD      cchOutCLSID);           // Out CLS ID buffer size in characters


//*****************************************************************************
// Delete the basic settings for an inproc server.
//*****************************************************************************
    static HRESULT UnregisterClassBase(     // Return code.
        REFCLSID    rclsid,                 // Class ID we are registering.
        LPCTSTR     szProgID,               // Class prog ID.
        LPCTSTR     szIndepProgID,          // Class version independant prog ID.
        __out_ecount (cchOutCLSID) LPTSTR      szOutCLSID,            // Return formatted class ID here.
        DWORD      cchOutCLSID);           // Out CLS ID buffer size in characters

#endif //#ifndef FEATURE_CORECLR

//*****************************************************************************
// Return TRUE if the registry value name might have been seen in the registry
// at startup;
// return FALSE if the value was definitely not seen at startup.
//
// Perf Optimization for VSWhidbey:113373.
//*****************************************************************************
    static BOOL RegCacheValueNameSeenPerhaps(
        LPCWSTR name);
//*****************************************************************************
// Return TRUE if the environment variable name might have been seen at startup;
// return FALSE if the value was definitely not seen at startup.
//*****************************************************************************
    static BOOL EnvCacheValueNameSeenPerhaps(
        LPCWSTR name);

    static BOOL s_fUseRegCache; // Enable registry cache; if FALSE, CCVNSP
                                 // always returns TRUE.
    static BOOL s_fUseEnvCache; // Enable env cache.

    static BOOL s_fUseRegistry; // Allow lookups in the registry

    // Open the .NetFramework keys once and cache the handles
    static HKEY s_hMachineFrameworkKey;
    static HKEY s_hUserFrameworkKey;
};

#include "clrconfig.h" 

// The HRESULT_FROM_WIN32 macro evaluates its arguments three times.
// <TODO>TODO: All HRESULT_FROM_WIN32(GetLastError()) should be replaced by calls to
//  this helper function avoid code bloat</TODO>
inline HRESULT HRESULT_FROM_GetLastError()
{
    WRAPPER_NO_CONTRACT;
    DWORD dw = GetLastError();
    // Make sure we return a failure
    if (dw == ERROR_SUCCESS)
    {
        _ASSERTE(!"We were expecting to get an error code, but a success code is being returned. Check this code path for Everett!");
        return E_FAIL;
    }
    else
        return HRESULT_FROM_WIN32(dw);
}

inline HRESULT HRESULT_FROM_GetLastErrorNA()
{
    WRAPPER_NO_CONTRACT;
    DWORD dw = GetLastError();
    // Make sure we return a failure
    if (dw == ERROR_SUCCESS)
        return E_FAIL;
    else
        return HRESULT_FROM_WIN32(dw);
}

inline HRESULT BadError(HRESULT hr)
{
    LIMITED_METHOD_CONTRACT;
    _ASSERTE(!"Serious Error");
    return (hr);
}

//*****************************************************************************
// Convert a GUID into a pointer to a string
//*****************************************************************************
int GuidToLPWSTR(                  // Return status.
    GUID        Guid,                  // [IN] The GUID to convert.
    __out_ecount (cchGuid) LPWSTR szGuid, // [OUT] String into which the GUID is stored
    DWORD       cchGuid);              // [IN] Size in wide chars of szGuid

#ifndef DEBUG_NOINLINE
#if defined(_DEBUG)
#define DEBUG_NOINLINE __declspec(noinline)
#else
#define DEBUG_NOINLINE
#endif
#endif

namespace UtilCode
{
    // These are type-safe versions of Interlocked[Compare]Exchange
    // They avoid invoking struct cast operations via reinterpreting
    // the struct's address as a LONG* or LONGLONG* and dereferencing it.
    // 
    // If we had a global ::operator & (unary), we would love to use that
    // to ensure we were not also accidentally getting a structs's provided
    // operator &. TODO: probe with a static_assert?

    template <typename T, int SIZE = sizeof(T)>
    struct InterlockedCompareExchangeHelper;

    template <typename T>
    struct InterlockedCompareExchangeHelper<T, sizeof(LONG)>
    {
        static inline T InterlockedExchange(
            T volatile * target,
            T            value)
        {
            static_assert_no_msg(sizeof(T) == sizeof(LONG));
            LONG res = ::InterlockedExchange(
                reinterpret_cast<LONG volatile *>(target),
                *reinterpret_cast<LONG *>(/*::operator*/&(value)));
            return *reinterpret_cast<T*>(&res);
        }

        static inline T InterlockedCompareExchange(
            T volatile * destination,
            T            exchange,
            T            comparand)
        {
            static_assert_no_msg(sizeof(T) == sizeof(LONG));
            LONG res = ::InterlockedCompareExchange(
                reinterpret_cast<LONG volatile *>(destination),
                *reinterpret_cast<LONG*>(/*::operator*/&(exchange)),
                *reinterpret_cast<LONG*>(/*::operator*/&(comparand)));
            return *reinterpret_cast<T*>(&res);
        }
    };
 
    template <typename T>
    struct InterlockedCompareExchangeHelper<T, sizeof(LONGLONG)>
    {
        static inline T InterlockedExchange(
            T volatile * target,
            T            value)
        {
            static_assert_no_msg(sizeof(T) == sizeof(LONGLONG));
            LONGLONG res = ::InterlockedExchange64(
                reinterpret_cast<LONGLONG volatile *>(target),
                *reinterpret_cast<LONGLONG *>(/*::operator*/&(value)));
            return *reinterpret_cast<T*>(&res);
        }

        static inline T InterlockedCompareExchange(
            T volatile * destination,
            T            exchange,
            T            comparand)
        {
            static_assert_no_msg(sizeof(T) == sizeof(LONGLONG));
            LONGLONG res = ::InterlockedCompareExchange64(
                reinterpret_cast<LONGLONG volatile *>(destination),
                *reinterpret_cast<LONGLONG*>(/*::operator*/&(exchange)),
                *reinterpret_cast<LONGLONG*>(/*::operator*/&(comparand)));
            return *reinterpret_cast<T*>(&res);
        }
    };
}
 
template <typename T>
inline T InterlockedExchangeT(
    T volatile * target,
    T            value)
{
    return ::UtilCode::InterlockedCompareExchangeHelper<T>::InterlockedExchange(
        target, value);
}

template <typename T>
inline T InterlockedCompareExchangeT(
    T volatile * destination,
    T            exchange,
    T            comparand)
{
    return ::UtilCode::InterlockedCompareExchangeHelper<T>::InterlockedCompareExchange(
        destination, exchange, comparand);
}

// Pointer variants for Interlocked[Compare]ExchangePointer
// If the underlying type is a const type, we have to remove its constness
// since Interlocked[Compare]ExchangePointer doesn't take const void * arguments.
template <typename T>
inline T* InterlockedExchangeT(
    T* volatile * target,
    T*            value)
{
    //STATIC_ASSERT(value == 0);
    typedef typename std::remove_const<T>::type * non_const_ptr_t;
    return reinterpret_cast<T*>(InterlockedExchangePointer(
        reinterpret_cast<PVOID volatile *>(const_cast<non_const_ptr_t volatile *>(target)),
        reinterpret_cast<PVOID>(const_cast<non_const_ptr_t>(value))));
}

template <typename T>
inline T* InterlockedCompareExchangeT(
    T* volatile * destination,
    T*            exchange,
    T*            comparand)
{
    //STATIC_ASSERT(exchange == 0);
    typedef typename std::remove_const<T>::type * non_const_ptr_t;
    return reinterpret_cast<T*>(InterlockedCompareExchangePointer(
        reinterpret_cast<PVOID volatile *>(const_cast<non_const_ptr_t volatile *>(destination)),
        reinterpret_cast<PVOID>(const_cast<non_const_ptr_t>(exchange)), 
        reinterpret_cast<PVOID>(const_cast<non_const_ptr_t>(comparand))));
}

// NULL pointer variants of the above to avoid having to cast NULL
// to the appropriate pointer type.
template <typename T>
inline T* InterlockedExchangeT(
    T* volatile *   target,
    int             value) // When NULL is provided as argument.
{
    //STATIC_ASSERT(value == 0);
    return InterlockedExchangeT(target, reinterpret_cast<T*>(value));
}

template <typename T>
inline T* InterlockedCompareExchangeT(
    T* volatile *   destination,
    int             exchange,  // When NULL is provided as argument.
    T*              comparand)
{
    //STATIC_ASSERT(exchange == 0);
    return InterlockedCompareExchangeT(destination, reinterpret_cast<T*>(exchange), comparand);
}

template <typename T>
inline T* InterlockedCompareExchangeT(
    T* volatile *   destination,
    T*              exchange,
    int             comparand) // When NULL is provided as argument.
{
    //STATIC_ASSERT(comparand == 0);
    return InterlockedCompareExchangeT(destination, exchange, reinterpret_cast<T*>(comparand));
}

#undef InterlockedExchangePointer
#define InterlockedExchangePointer Use_InterlockedExchangeT
#undef InterlockedCompareExchangePointer
#define InterlockedCompareExchangePointer Use_InterlockedCompareExchangeT

#endif // __UtilCode_h__
