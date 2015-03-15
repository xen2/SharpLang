//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#ifndef CHECK_INL_
#define CHECK_INL_

#include "check.h"
#ifdef SHARPLANG_NOTIMPLEMENTED
#include "clrhost.h"
#endif // SHARPLANG_NOTIMPLEMENTED
#include "debugmacros.h"
#include "clrtypes.h"

inline LONG *CHECK::InitTls()
{
    return NULL;
}

FORCEINLINE BOOL CHECK::EnterAssert()
{
    return TRUE;
}

FORCEINLINE void CHECK::LeaveAssert()
{
}

FORCEINLINE BOOL CHECK::IsInAssert()
{
    return FALSE;
}

FORCEINLINE BOOL CHECK::EnforceAssert()
{
    if (s_neverEnforceAsserts)
        return FALSE;
    else
    {
        CHECK chk;
        return !chk.IsInAssert();
    }
}

FORCEINLINE void CHECK::ResetAssert()
{
    CHECK chk;
    if (chk.IsInAssert())
        chk.LeaveAssert();
}

inline void CHECK::SetAssertEnforcement(BOOL value)
{
    s_neverEnforceAsserts = !value;
}

// Fail records the result of a condition check.  Can take either a
// boolean value or another check result
FORCEINLINE BOOL CHECK::Fail(BOOL condition)
{
    return !condition;
}

FORCEINLINE BOOL CHECK::Fail(const CHECK &check)
{
    m_message = check.m_message;
    return m_message != NULL;
}

#ifndef _DEBUG
FORCEINLINE void CHECK::Setup(LPCSTR message)
{
    m_message = message;
}

FORCEINLINE LPCSTR CHECK::FormatMessage(LPCSTR messageFormat, ...)
{
    return messageFormat;
}
#endif

FORCEINLINE CHECK::operator BOOL ()
{
    return m_message == NULL;
}

FORCEINLINE BOOL CHECK::operator!()
{
    return m_message != NULL;
}

inline CHECK CheckAlignment(UINT alignment)
{
    STATIC_CONTRACT_WRAPPER;
    CHECK((alignment & (alignment-1)) == 0);
    CHECK_OK;
}

inline CHECK CheckAligned(UINT value, UINT alignment)
{
    STATIC_CONTRACT_WRAPPER;
    CHECK(AlignmentTrim(value, alignment) == 0);
    CHECK_OK;
}

#ifndef PLATFORM_UNIX
// For Unix this and the previous function get the same types.
// So, exclude this one.
inline CHECK CheckAligned(ULONG value, UINT alignment)
{
    STATIC_CONTRACT_WRAPPER;
    CHECK(AlignmentTrim(value, alignment) == 0);
    CHECK_OK;
}
#endif // PLATFORM_UNIX

inline CHECK CheckAligned(UINT64 value, UINT alignment)
{
    STATIC_CONTRACT_WRAPPER;
    CHECK(AlignmentTrim(value, alignment) == 0);
    CHECK_OK;
}

inline CHECK CheckAligned(const void *address, UINT alignment)
{
    STATIC_CONTRACT_WRAPPER;
    CHECK(AlignmentTrim((SIZE_T)address, alignment) == 0);
    CHECK_OK;
}

inline CHECK CheckOverflow(UINT value1, UINT value2)
{
    CHECK(value1 + value2 >= value1);
    CHECK_OK;
}

#if defined(_MSC_VER)
inline CHECK CheckOverflow(ULONG value1, ULONG value2)
{
    CHECK(value1 + value2 >= value1);
    CHECK_OK;
}
#endif

inline CHECK CheckOverflow(UINT64 value1, UINT64 value2)
{
    CHECK(value1 + value2 >= value1);
    CHECK_OK;
}

inline CHECK CheckOverflow(PTR_CVOID address, UINT offset)
{
    TADDR targetAddr = dac_cast<TADDR>(address);
#if POINTER_BITS == 32
    CHECK((UINT) (SIZE_T)(targetAddr) + offset >= (UINT) (SIZE_T) (targetAddr));
#else
    CHECK((UINT64) targetAddr + offset >= (UINT64) targetAddr);
#endif

    CHECK_OK;
}

#if defined(_MSC_VER)
inline CHECK CheckOverflow(const void *address, ULONG offset)
{
#if POINTER_BITS == 32
    CHECK((ULONG) (SIZE_T) address + offset >= (ULONG) (SIZE_T) address);
#else
    CHECK((UINT64) address + offset >= (UINT64) address);
#endif

    CHECK_OK;
}
#endif

inline CHECK CheckOverflow(const void *address, UINT64 offset)
{
#if POINTER_BITS == 32
    CHECK(offset >> 32 == 0);
    CHECK((UINT) (SIZE_T) address + (UINT) offset >= (UINT) (SIZE_T) address);
#else
    CHECK((UINT64) address + offset >= (UINT64) address);
#endif

    CHECK_OK;
}


inline CHECK CheckUnderflow(UINT value1, UINT value2)
{
    CHECK(value1 - value2 <= value1);

    CHECK_OK;
}

#ifndef PLATFORM_UNIX
// For Unix this and the previous function get the same types.
// So, exclude this one.
inline CHECK CheckUnderflow(ULONG value1, ULONG value2)
{
    CHECK(value1 - value2 <= value1);

    CHECK_OK;
}
#endif // PLATFORM_UNIX

inline CHECK CheckUnderflow(UINT64 value1, UINT64 value2)
{
    CHECK(value1 - value2 <= value1);

    CHECK_OK;
}

inline CHECK CheckUnderflow(const void *address, UINT offset)
{
#if POINTER_BITS == 32
    CHECK((UINT) (SIZE_T) address - offset <= (UINT) (SIZE_T) address);
#else
    CHECK((UINT64) address - offset <= (UINT64) address);
#endif

    CHECK_OK;
}

#if defined(_MSC_VER)
inline CHECK CheckUnderflow(const void *address, ULONG offset)
{
#if POINTER_BITS == 32
    CHECK((ULONG) (SIZE_T) address - offset <= (ULONG) (SIZE_T) address);
#else
    CHECK((UINT64) address - offset <= (UINT64) address);
#endif

    CHECK_OK;
}
#endif

inline CHECK CheckUnderflow(const void *address, UINT64 offset)
{
#if POINTER_BITS == 32
    CHECK(offset >> 32 == 0);
    CHECK((UINT) (SIZE_T) address - (UINT) offset <= (UINT) (SIZE_T) address);
#else
    CHECK((UINT64) address - offset <= (UINT64) address);
#endif

    CHECK_OK;
}

inline CHECK CheckUnderflow(const void *address, void *address2)
{
#if POINTER_BITS == 32
    CHECK((UINT) (SIZE_T) address - (UINT) (SIZE_T) address2 <= (UINT) (SIZE_T) address);
#else
    CHECK((UINT64) address - (UINT64) address2 <= (UINT64) address);
#endif

    CHECK_OK;
}

inline CHECK CheckZeroedMemory(const void *memory, SIZE_T size)
{
    CHECK(CheckOverflow(memory, (UINT64)size));

    BYTE *p = (BYTE *) memory;
    BYTE *pEnd = p + size;

    while (p < pEnd)
        CHECK(*p++ == 0);

    CHECK_OK;
}

inline CHECK CheckBounds(const void *rangeBase, UINT32 rangeSize, UINT32 offset)
{
    CHECK(CheckOverflow(dac_cast<PTR_CVOID>(rangeBase), rangeSize));
    CHECK(offset <= rangeSize);
    CHECK_OK;
}

inline CHECK CheckBounds(const void *rangeBase, UINT32 rangeSize, UINT32 offset, UINT32 size)
{
    CHECK(CheckOverflow(dac_cast<PTR_CVOID>(rangeBase), rangeSize));
    CHECK(CheckOverflow(offset, size));
    CHECK(offset + size <= rangeSize);
    CHECK_OK;
}

#endif  // CHECK_INL_

