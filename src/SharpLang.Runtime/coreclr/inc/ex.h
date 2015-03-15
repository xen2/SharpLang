//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//


#if !defined(_EX_H_)
#define _EX_H_

#define EX_TRY  try
#define EX_CATCH_HRESULT(_hr)    catch (HRESULT hr) { _hr = hr; }
#define EX_CATCH    catch(...)
#define EX_END_CATCH(a)
#define EX_RETHROW  throw
#define EX_SWALLOW_NONTERMINAL catch(...) {}
#define EX_END_CATCH_UNREACHABLE
#define EX_CATCH_HRESULT_NO_ERRORINFO(_hr)                                      \
    EX_CATCH                                                                    \
    {                                                                           \
        (_hr) = GET_EXCEPTION()->GetHR();                                       \
        _ASSERTE(FAILED(_hr));                                                  \
    }                                                                           \
    EX_END_CATCH(SwallowAllExceptions)

void DECLSPEC_NORETURN ThrowHR(HRESULT hr);

inline void IfFailThrow(HRESULT hr)
{
    WRAPPER_NO_CONTRACT;

    if (FAILED(hr))
    {
        ThrowHR(hr);
    }
}

void DECLSPEC_NORETURN ThrowLastError();

#endif  // _EX_H_
