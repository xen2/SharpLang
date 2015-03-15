//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

//

/*============================================================
**
** Class:  SafeHandle
**
**
** Purpose: The unmanaged implementation of the SafeHandle 
**          class
**
===========================================================*/

#include "common.h"
#include "vars.hpp"
#include "object.h"
#include "excep.h"
#include "frames.h"
#include "eecontract.h"
//#include "mdaassistants.h"
//#include "typestring.h"

// TODO: Implement this
void SafeHandle::SetHandle(LPVOID handle)
{
    m_handle = handle;
}

void AcquireSafeHandle(SAFEHANDLEREF* s) 
{
    WRAPPER_NO_CONTRACT;
    GCX_COOP();
    _ASSERTE(s != NULL && *s != NULL);
    //(*s)->AddRef(); 
}

void ReleaseSafeHandle(SAFEHANDLEREF* s) 
{
    WRAPPER_NO_CONTRACT;
    GCX_COOP();
    _ASSERTE(s != NULL && *s != NULL);
    //(*s)->Release(false); 
}
