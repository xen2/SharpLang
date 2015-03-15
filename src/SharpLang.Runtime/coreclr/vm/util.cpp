#include "common.h"
#include "util.hpp"

//
//
// COMCharacter and Helper functions
//
//

#ifndef FEATURE_PAL
/*============================GetCharacterInfoHelper============================
**Determines character type info (digit, whitespace, etc) for the given char.
**Args:   c is the character on which to operate.
**        CharInfoType is one of CT_CTYPE1, CT_CTYPE2, CT_CTYPE3 and specifies the type
**        of information being requested.
**Returns: The bitmask returned by GetStringTypeEx.  The caller needs to know
**         how to interpret this.
**Exceptions: ArgumentException if GetStringTypeEx fails.
==============================================================================*/
INT32 GetCharacterInfoHelper(WCHAR c, INT32 CharInfoType)
{
    WRAPPER_NO_CONTRACT;

    unsigned short result=0;
    if (!GetStringTypeEx(LOCALE_USER_DEFAULT, CharInfoType, &(c), 1, &result)) {
        _ASSERTE(!"This should not happen, verify the arguments passed to GetStringTypeEx()");
    }
    return(INT32)result;
}
#endif // !FEATURE_PAL

/*==============================nativeIsWhiteSpace==============================
**The locally available version of IsWhiteSpace.  Designed to be called by other
**native methods.  The work is mostly done by GetCharacterInfoHelper
**Args:  c -- the character to check.
**Returns: true if c is whitespace, false otherwise.
**Exceptions:  Only those thrown by GetCharacterInfoHelper.
==============================================================================*/
BOOL COMCharacter::nativeIsWhiteSpace(WCHAR c)
{
    WRAPPER_NO_CONTRACT;

#ifndef FEATURE_PAL
    if (c <= (WCHAR) 0x7F) // common case
    {
        BOOL result = (c == ' ') || (c == '\r') || (c == '\n') || (c == '\t') || (c == '\f') || (c == (WCHAR) 0x0B);

        ASSERT(result == ((GetCharacterInfoHelper(c, CT_CTYPE1) & C1_SPACE)!=0));

        return result;
    }

    // GetCharacterInfoHelper costs around 160 instructions
    return((GetCharacterInfoHelper(c, CT_CTYPE1) & C1_SPACE)!=0);
#else // !FEATURE_PAL
    return iswspace(c);
#endif // !FEATURE_PAL
}

/*================================nativeIsDigit=================================
**The locally available version of IsDigit.  Designed to be called by other
**native methods.  The work is mostly done by GetCharacterInfoHelper
**Args:  c -- the character to check.
**Returns: true if c is whitespace, false otherwise.
**Exceptions:  Only those thrown by GetCharacterInfoHelper.
==============================================================================*/
BOOL COMCharacter::nativeIsDigit(WCHAR c)
{
    WRAPPER_NO_CONTRACT;
#ifndef FEATURE_PAL
    return((GetCharacterInfoHelper(c, CT_CTYPE1) & C1_DIGIT)!=0);
#else // !FEATURE_PAL
    return iswdigit(c);
#endif // !FEATURE_PAL
}
