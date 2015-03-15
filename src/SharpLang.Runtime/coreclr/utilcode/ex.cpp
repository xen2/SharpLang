#include "common.h"
#include "ex.h"

void DECLSPEC_NORETURN ThrowHR(HRESULT hr)
{
    WRAPPER_NO_CONTRACT;

    STRESS_LOG1(LF_EH, LL_INFO100, "ThrowHR: HR = %x\n", hr);
    
    if (hr == E_OUTOFMEMORY)
        ThrowOutOfMemory();

    // Catchers assume only failing hresults
    _ASSERTE(FAILED(hr));   
    if (hr == S_OK)
        hr = E_FAIL;

    assert(false);
    //EX_THROW(HRException, (hr));
}

void DECLSPEC_NORETURN ThrowOutOfMemory()
{
    CONTRACTL
    {
        THROWS;
        GC_NOTRIGGER;
        SO_TOLERANT;
        SUPPORTS_DAC;
    }
    CONTRACTL_END;
    
    //g_hrFatalError = COR_E_OUTOFMEMORY;

    // Regular CLR builds - throw our pre-created OOM exception object
    //PAL_CPP_THROW(Exception *, Exception::GetOOMException());
    assert(false);
}
