//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 
//

#ifndef __COMMON_LANGUAGE_RUNTIME_HRESULTS__
#define __COMMON_LANGUAGE_RUNTIME_HRESULTS__

#ifndef EMAKEHR
#define SMAKEHR(val) MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_URT, val)
#define EMAKEHR(val) MAKE_HRESULT(SEVERITY_ERROR, FACILITY_URT, val)
#endif

#define COR_E_OVERFLOW EMAKEHR(0x1516)

#endif // __COMMON_LANGUAGE_RUNTIME_HRESULTS__
