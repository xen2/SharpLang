#include "common.h"
#include "floatclass.h"
#include "../classlibnative/cryptography/cryptography.h"
#include "../classlibnative/bcltype/number.h"

// Later we should directly use ecalllist.h info to automatically replace VTable pointers for internal calls

extern "C" double System_Math__Floor_System_Double_(double d)
{
	return COMDouble::Floor(d);
}

extern "C" double System_Math__Round_System_Double_(double d)
{
	return COMDouble::Round(d);
}

extern "C" Object* System_Number__FormatInt32_System_Int32_System_String_System_Globalization_NumberFormatInfo_(int32_t value, StringObject* format, NumberFormatInfo* info)
{
	return COMNumber::FormatInt32(value, format, info);
}

extern "C" Object* System_Threading_Interlocked__CompareExchange_System_Object__System_Object_System_Object_(Object** location1, Object* value, Object* comparand)
{
        return (Object*)InterlockedCompareExchangeT(location1, value, comparand);
}

extern "C" Object* System_Threading_Interlocked__CompareExchange_System_IntPtr__System_IntPtr_System_IntPtr_(void** location1, void* value, void* comparand)
{
        return (Object*)InterlockedCompareExchangeT(location1, value, comparand);
}

extern "C" Object* System_Runtime_InteropServices_GCHandle__InternalGet_System_IntPtr_(GCHandle* gcHandle)
{
        return gcHandle->value;
}

extern "C" Object* System_Runtime_InteropServices_GCHandle__InternalCompareExchange_System_IntPtr_System_Object_System_Object_System_Boolean_(GCHandle* gcHandle, Object* value, Object* oldValue, bool isPinned)
{
        return (Object*)InterlockedCompareExchangeT(&gcHandle->value, value, oldValue);
}

#if defined(FEATURE_CRYPTO)
extern "C" void System_Security_Cryptography_Utils___AcquireCSP_System_Security_Cryptography_CspParameters_System_Security_Cryptography_SafeProvHandle__(Object* param, SafeHandle** hProv)
{
	COMCryptography::_AcquireCSP(param, hProv);
}

extern "C" __declspec(dllexport) CRYPT_HASH_CTX* __stdcall CreateHash(CRYPT_PROV_CTX * pProvCtx, DWORD dwHashType)
{
	return COMCryptography::CreateHash(pProvCtx, dwHashType);
}

extern "C" __declspec(dllexport) void __stdcall HashData(CRYPT_HASH_CTX * pHashCtx, LPCBYTE pData, DWORD cbData, DWORD dwStart, DWORD dwSize)
{
	COMCryptography::HashData(pHashCtx, pData, cbData, dwStart, dwSize);
}

extern "C" __declspec(dllexport) void __stdcall EndHash(CRYPT_HASH_CTX * pHashCtx, QCall::ObjectHandleOnStack retHash)
{
	COMCryptography::EndHash(pHashCtx, retHash);
}
#endif

