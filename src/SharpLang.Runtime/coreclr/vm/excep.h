#ifndef __excep_h__
#define __excep_h__

#include "runtimeexceptionkind.h"
#include "exceptmacros.h"

VOID DECLSPEC_NORETURN RealCOMPlusThrowHR(HRESULT hr);
VOID DECLSPEC_NORETURN RealCOMPlusThrow(RuntimeExceptionKind reKind);
VOID DECLSPEC_NORETURN RealCOMPlusThrow(RuntimeExceptionKind  reKind, UINT resID, 
                                        LPCWSTR wszArg1 = NULL, LPCWSTR wszArg2 = NULL, LPCWSTR wszArg3 = NULL, 
                                        LPCWSTR wszArg4 = NULL, LPCWSTR wszArg5 = NULL, LPCWSTR wszArg6 = NULL);
VOID DECLSPEC_NORETURN RealCOMPlusThrow(RuntimeExceptionKind reKind, LPCWSTR wszResourceName, Exception * pInnerException = NULL);
VOID DECLSPEC_NORETURN RealCOMPlusThrowOM();
VOID DECLSPEC_NORETURN RealCOMPlusThrowArgumentNull(LPCWSTR argName);
VOID DECLSPEC_NORETURN RealCOMPlusThrowArgumentOutOfRange(LPCWSTR argName, LPCWSTR wszResourceName);
VOID DECLSPEC_NORETURN RealCOMPlusThrowArgumentException(LPCWSTR argName, LPCWSTR wszResourceName);
VOID DECLSPEC_NORETURN RealCOMPlusThrowWin32();

// #include "../dlls/mscorrc/resource.h"
#define IDS_EE_CRYPTO_UNKNOWN_OPERATION         0x17f4

#endif // __excep_h__
