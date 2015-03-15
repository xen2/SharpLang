#include "common.h"
#include "excep.h"

VOID DECLSPEC_NORETURN RealCOMPlusThrowHR(HRESULT hr)
{
    assert(false);
}

VOID DECLSPEC_NORETURN RealCOMPlusThrow(RuntimeExceptionKind reKind)
{
    assert(false);
}

VOID DECLSPEC_NORETURN RealCOMPlusThrow(RuntimeExceptionKind  reKind, UINT resID, 
                                        LPCWSTR wszArg1, LPCWSTR wszArg2, LPCWSTR wszArg3, 
                                        LPCWSTR wszArg4, LPCWSTR wszArg5, LPCWSTR wszArg6)
{
    assert(false);
}

VOID DECLSPEC_NORETURN RealCOMPlusThrow(RuntimeExceptionKind reKind, LPCWSTR wszResourceName, Exception * pInnerException)
{
    assert(false);
}

VOID DECLSPEC_NORETURN RealCOMPlusThrowOM()
{
    assert(false);
}

VOID DECLSPEC_NORETURN RealCOMPlusThrowArgumentNull(LPCWSTR argName)
{
    assert(false);
}

VOID DECLSPEC_NORETURN RealCOMPlusThrowArgumentOutOfRange(LPCWSTR argName, LPCWSTR wszResourceName)
{
    assert(false);
}

VOID DECLSPEC_NORETURN RealCOMPlusThrowArgumentException(LPCWSTR argName, LPCWSTR wszResourceName)
{
    assert(false);
}

VOID DECLSPEC_NORETURN RealCOMPlusThrowWin32()
{
    assert(false);
}
