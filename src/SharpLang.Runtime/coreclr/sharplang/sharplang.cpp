// SharpLang implementation for various methods required by CoreCLR runtime
#include "common.h"

void DoJITFailFast()
{
    exit(0);
}

// Temporary
extern EEType System_Byte___rtti;

// gchelpers.cpp
OBJECTREF   AllocatePrimitiveArray(CorElementType type, DWORD length, BOOL bAllocateInLargeHeap)
{
    // TODO: Implement this properly
	switch (type)
	{
	case ELEMENT_TYPE_U1:
		auto result = (Array<uint8_t>*)malloc(sizeof(Array<uint8_t>));
		result->eeType = &System_Byte___rtti;
		result->length = length;
		result->value = (uint8_t*) malloc(result->eeType->elementSize * length);
		return result;
	}

	assert(false);
	return NULL;
}

OBJECTREF AllocateValueSzArray(TypeHandle elementType, INT32 length)
{
    CONTRACTL {
        THROWS;
        GC_TRIGGERS;
        MODE_COOPERATIVE; // returns an objref without pinning it => cooperative        
    } CONTRACTL_END;

    // TODO: Implement this
	assert(false);
	return NULL;
}

// ceemain.cpp
Volatile<BOOL> g_fEEStarted = TRUE;

// syncclean.cpp
VolatilePtr<EEHashEntry*> SyncClean::m_EEHashTable;

void SyncClean::AddEEHashTable (EEHashEntry** entry)
{
    WRAPPER_NO_CONTRACT;

    if (!g_fEEStarted) {
        delete [] (entry-1);
        return;
    }

    BEGIN_GETTHREAD_ALLOWED
    _ASSERTE (GetThread() == NULL || GetThread()->PreemptiveGCDisabled());
    END_GETTHREAD_ALLOWED

    EEHashEntry ** pTempHashEntry = NULL;
    do
    {
        pTempHashEntry = (EEHashEntry**)m_EEHashTable;
        entry[-1] = (EEHashEntry *)pTempHashEntry;
    }
    while (FastInterlockCompareExchangePointer(m_EEHashTable.GetPointer(), entry, pTempHashEntry) != pTempHashEntry);
}

// clrhost_nodependencies.cpp
const NoThrow nothrow = { 0 };

void * __cdecl operator new(size_t n, const NoThrow&)
{
    // TODO: Proper GC implementation, w/ contract
    return malloc(n);
}

void * __cdecl operator new[](size_t n, const NoThrow&)
{
    // TODO: Proper GC implementation, w/ contract
    return malloc(n);
}

//*****************************************************************************
// Convert hex value into a wide string of hex digits 
//*****************************************************************************
HRESULT GetStr(
                                 DWORD  hHexNum, 
    __out_ecount((cbHexNum * 2)) LPWSTR szHexNum, 
                                 DWORD  cbHexNum)
{
    CONTRACTL
    {
        NOTHROW;
    }
    CONTRACTL_END;
    
    _ASSERTE (szHexNum);
    cbHexNum *= 2; // each nibble is a char
    while (cbHexNum != 0)
    {
        DWORD thisHexDigit = hHexNum % 16;
        hHexNum /= 16;
        cbHexNum--;
        if (thisHexDigit < 10)
        {
            *(szHexNum+cbHexNum) = (BYTE)(thisHexDigit + W('0'));
        }
        else
        {
            *(szHexNum+cbHexNum) = (BYTE)(thisHexDigit - 10 + W('A'));
        }
    }
    return S_OK;
}

//*****************************************************************************
// Convert a GUID into a pointer to a Wide char string
//*****************************************************************************
int 
GuidToLPWSTR(
                          GUID   Guid,      // The GUID to convert.
    __out_ecount(cchGuid) LPWSTR szGuid,    // String into which the GUID is stored
                          DWORD  cchGuid)   // Count in wchars
{
    CONTRACTL
    {
        NOTHROW;
    }
    CONTRACTL_END;
    
    int         i;
    
    // successive fields break the GUID into the form DWORD-WORD-WORD-WORD-WORD.DWORD 
    // covering the 128-bit GUID. The string includes enclosing braces, which are an OLE convention.

    if (cchGuid < 39) // 38 chars + 1 null terminating.
        return 0;

    // {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}
    // ^
    szGuid[0]  = W('{');

    // {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}
    //  ^^^^^^^^
    if (FAILED (GetStr(Guid.Data1, szGuid+1 , 4))) return 0;

    szGuid[9]  = W('-');
    
    // {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}
    //           ^^^^
    if (FAILED (GetStr(Guid.Data2, szGuid+10, 2))) return 0;

    szGuid[14] = W('-');
    
    // {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}
    //                ^^^^
    if (FAILED (GetStr(Guid.Data3, szGuid+15, 2))) return 0;

    szGuid[19] = W('-');
    
    // Get the last two fields (which are byte arrays).
    // {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}
    //                     ^^^^
    for (i=0; i < 2; ++i)
        if (FAILED(GetStr(Guid.Data4[i], szGuid + 20 + (i * 2), 1)))
            return (0);

    szGuid[24] = W('-');
    
    // {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}
    //                          ^^^^^^^^^^^^
    for (i=0; i < 6; ++i)
        if (FAILED(GetStr(Guid.Data4[i+2], szGuid + 25 + (i * 2), 1)))
            return (0);

    // {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}
    //                                      ^
    szGuid[37] = W('}');
    szGuid[38] = W('\0');

    return 39;
} // GuidToLPWSTR

// gcenv.cpp
__declspec(thread) Thread * pCurrentThread;

Thread * GetThread()
{
    return pCurrentThread;
}

// sstring.cpp
void SString::ConvertToUnicode() const
{
    assert(false);
}

// vars.cpp
GPTR_IMPL(EEConfig, g_pConfig);     // configuration data (from the registry)
