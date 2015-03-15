#include "RuntimeType.h"
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