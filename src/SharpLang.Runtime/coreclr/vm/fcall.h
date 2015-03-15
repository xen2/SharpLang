//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// FCall.H
//

//
// FCall is a high-performance alternative to ECall. Unlike ECall, FCall
// methods do not necessarily create a frame.   Jitted code calls directly
// to the FCall entry point.   It is possible to do operations that need
// to have a frame within an FCall, you need to manually set up the frame
// before you do such operations.

// It is illegal to cause a GC or EH to happen in an FCALL before setting
// up a frame.  To prevent accidentally violating this rule, FCALLs turn
// on BEGINGCFORBID, which insures that these things can't happen in a 
// checked build without causing an ASSERTE.  Once you set up a frame,
// this state is turned off as long as the frame is active, and then is
// turned on again when the frame is torn down.   This mechanism should
// be sufficient to insure that the rules are followed.

// In general you set up a frame by using the following macros

//      HELPER_METHOD_FRAME_BEGIN_RET*()    // Use If the FCALL has a return value
//      HELPER_METHOD_FRAME_BEGIN*()        // Use If FCALL does not return a value
//      HELPER_METHOD_FRAME_END*()              

// These macros introduce a scope which is protected by an HelperMethodFrame.
// In this scope you can do EH or GC.   There are rules associated with 
// their use.  In particular

//      1) These macros can only be used in the body of a FCALL (that is
//         something using the FCIMPL* or HCIMPL* macros for their decaration.

//      2) You may not perform a 'return' within this scope..

// Compile time errors occur if you try to violate either of these rules.

// The frame that is set up does NOT protect any GC variables (in particular the
// arguments of the FCALL.  Thus you need to do an explicit GCPROTECT once the
// frame is established if you need to protect an argument.  There are flavors
// of HELPER_METHOD_FRAME that protect a certain number of GC variables.  For
// example

//      HELPER_METHOD_FRAME_BEGIN_RET_2(arg1, arg2)

// will protect the GC variables arg1, and arg2 as well as erecting the frame.

// Another invariant that you must be aware of is the need to poll to see if
// a GC is needed by some other thread.   Unless the FCALL is VERY short, 
// every code path through the FCALL must do such a poll.  The important 
// thing here is that a poll will cause a GC, and thus you can only do it
// when all you GC variables are protected.   To make things easier 
// HELPER_METHOD_FRAMES that protect things automatically do this poll.
// If you don't need to protect anything HELPER_METHOD_FRAME_BEGIN_0
// will also do the poll. 

// Sometimes it is convenient to do the poll a the end of the frame, you 
// can use HELPER_METHOD_FRAME_BEGIN_NOPOLL and HELPER_METHOD_FRAME_END_POLL
// to do the poll at the end.   If somewhere in the middle is the best
// place you can do that too with HELPER_METHOD_POLL()

// You don't need to erect a helper method frame to do a poll.  FC_GC_POLL
// can do this (remember all your GC refs will be trashed).  

// Finally if your method is VERY small, you can get away without a poll,
// you have to use FC_GC_POLL_NOT_NEEDED to mark this.
// Use sparingly!

// It is possible to set up the frame as the first operation in the FCALL and
// tear it down as the last operation before returning.  This works and is 
// reasonably efficient (as good as an ECall), however, if it is the case that
// you can defer the setup of the frame to an unlikely code path (exception path)
// that is much better.   

// If you defer setup of the frame, all codepaths leading to the frame setup
// must be wrapped with PERMIT_HELPER_METHOD_FRAME_BEGIN/END.  These block
// certain compiler optimizations that interfere with the delayed frame setup.
// These macros are automatically included in the HCIMPL, FCIMPL, and frame
// setup macros.

// <TODO>TODO: we should have a way of doing a trial allocation (an allocation that
// will fail if it would cause a GC).  That way even FCALLs that need to allocate
// would not necessarily need to set up a frame.  </TODO>

// It is common to only need to set up a frame in order to throw an exception.
// While this can be done by doing 

//      HELPER_METHOD_FRAME_BEGIN()         // Use if FCALL does not return a value
//      COMPlusThrow(execpt);
//      HELPER_METHOD_FRAME_END()           

// It is more efficient (in space) to use convenience macro FCTHROW that does 
// this for you (sets up a frame, and does the throw).

//      FCTHROW(except)

// Since FCALLS have to conform to the EE calling conventions and not to C
// calling conventions, FCALLS, need to be declared using special macros (FCIMPL*) 
// that implement the correct calling conventions.  There are variants of these
// macros depending on the number of args, and sometimes the types of the 
// arguments. 

//------------------------------------------------------------------------
//    A very simple example:
//
//      FCIMPL2(INT32, Div, INT32 x, INT32 y)
//      {
//          if (y == 0) 
//              FCThrow(kDivideByZeroException);
//          return x/y;
//      }
//      FCIMPLEND
//
//
// *** WATCH OUT FOR THESE GOTCHAS: ***
// ------------------------------------
//  - In your FCDECL & FCIMPL protos, don't declare a param as type OBJECTREF
//    or any of its deriveds. This will break on the checked build because
//    __fastcall doesn't enregister C++ objects (which OBJECTREF is).
//    Instead, you need to do something like;
//
//      FCIMPL(.., .., Object* pObject0)
//          OBJECTREF pObject = ObjectToOBJECTREF(pObject0);
//      FCIMPL
//
//    For similar reasons, use Object* rather than OBJECTREF as a return type.  
//    Consider either using ObjectToOBJECTREF or calling VALIDATEOBJECTREF
//    to make sure your Object* is valid.
//
//  - FCThrow() must be called directly from your FCall impl function: it
//    cannot be called from a subfunction. Calling from a subfunction breaks
//    the VC code parsing workaround that lets us recover the callee saved registers.
//    Fortunately, you'll get a compile error complaining about an
//    unknown variable "__me".
//
//  - If your FCall returns VOID, you must use FCThrowVoid() rather than
//    FCThrow(). This is because FCThrow() has to generate an unexecuted
//    "return" statement for the code parser.
//
//  - If first and/or second argument of your FCall is 64-bit value on x86
//    (ie INT64, UINT64 or DOUBLE), you must use "V" versions of FCDECL and 
//    FCIMPL macros to enregister arguments correctly. For example, FCDECL3_IVI 
//    must be used for FCalls that take 3 arguments and 2nd argument is INT64.
//
//  - You may use structs for protecting multiple OBJECTREF's simultaneously.
//    In these cases, you must use a variant of a helper method frame with PROTECT
//    in the name, to ensure all the OBJECTREF's in the struct get protected.
//    Also, initialize all the OBJECTREF's first.  Like this:
//    
//    FCIMPL4(Object*, COMNlsInfo::nativeChangeCaseString, LocaleIDObject* localeUNSAFE,
//            INT_PTR pNativeTextInfo, StringObject* pStringUNSAFE, CLR_BOOL bIsToUpper)
//    {
//      [ignoring CONTRACT for now]
//      struct _gc 
//      {
//          STRINGREF pResult;
//          STRINGREF pString;
//          LOCALEIDREF pLocale;
//      } gc;
//      gc.pResult = NULL;
//      gc.pString = ObjectToSTRINGREF(pStringUNSAFE);
//      gc.pLocale = (LOCALEIDREF)ObjectToOBJECTREF(localeUNSAFE);
//  
//      HELPER_METHOD_FRAME_BEGIN_RET_PROTECT(gc)
//  
//    If you forgot the PROTECT part, the macro will only protect the first OBJECTREF, 
//    introducing a subtle GC hole in your code.  Fortunately, we now issue a 
//    compile-time error if you forget.

// How FCall works:
// ----------------
//   An FCall target uses __fastcall or some other calling convention to
//   match the IL calling convention exactly. Thus, a call to FCall is a direct
//   call to the target w/ no intervening stub or frame.
//
//   The tricky part is when FCThrow is called. FCThrow must generate
//   a proper method frame before allocating and throwing the exception.
//   To do this, it must recover several things:
//
//      - The location of the FCIMPL's return address (since that's
//        where the frame will be based.)
//
//      - The on-entry values of the callee-saved regs; which must
//        be recorded in the frame so that GC can update them.
//        Depending on how VC compiles your FCIMPL, those values are still
//        in the original registers or saved on the stack.
//
//        To figure out which, FCThrow() generates the code:
//
//              while (NULL == __FCThrow(__me, ...)) {};
//              return 0;
//
//        The "return" statement will never execute; but its presence guarantees
//        that VC will follow the __FCThrow() call with a VC epilog
//        that restores the callee-saved registers using a pretty small
//        and predictable set of Intel opcodes. __FCThrow() parses this
//        epilog and simulates its execution to recover the callee saved
//        registers.
//
//        The while loop is to prevent the compiler from doing tail call optimizations.
//        The helper frame interpretter needs the frame to be present.
//
//      - The MethodDesc* that this FCall implements. This MethodDesc*
//        is part of the frame and ensures that the FCall will appear
//        in the exception's stack trace. To get this, FCDECL declares
//        a static local __me, initialized to point to the FC target itself.
//        This address is exactly what's stored in the ECall lookup tables;
//        so __FCThrow() simply does a reverse lookup on that table to recover
//        the MethodDesc*.
//


#if !defined(__FCall_h__) && !defined(CLR_STANDALONE_BINDER)
#define __FCall_h__

#include "runtimeexceptionkind.h"

#define F_CALL_CONV

typedef CLR_BOOL FC_BOOL_RET;
typedef CLR_CHAR FC_CHAR_RET;

#define FCDECL0(rettype, funcname) rettype funcname()
#define FCDECL1(rettype, funcname, a1) rettype funcname(a1)
#define FCDECL1_V(rettype, funcname, a1) rettype funcname(a1)
#define FCDECL2(rettype, funcname, a1, a2) rettype funcname(a1, a2)
#define FCDECL2VA(rettype, funcname, a1, a2) rettype funcname(a1, a2, ...)
#define FCDECL2_VV(rettype, funcname, a1, a2) rettype funcname(a1, a2)
#define FCDECL2_VI(rettype, funcname, a1, a2) rettype funcname(a1, a2)
#define FCDECL2_IV(rettype, funcname, a1, a2) rettype funcname(a1, a2)
#define FCDECL3(rettype, funcname, a1, a2, a3) rettype funcname(a1, a2, a3)
#define FCDECL3_IIV(rettype, funcname, a1, a2, a3) rettype funcname(a1, a2, a3)
#define FCDECL3_VII(rettype, funcname, a1, a2, a3) rettype funcname(a1, a2, a3)
#define FCDECL3_IVV(rettype, funcname, a1, a2, a3) rettype funcname(a1, a2, a3)
#define FCDECL3_IVI(rettype, funcname, a1, a2, a3) rettype funcname(a1, a2, a3)
#define FCDECL3_VVI(rettype, funcname, a1, a2, a3) rettype funcname(a1, a2, a3)
#define FCDECL4(rettype, funcname, a1, a2, a3, a4) rettype funcname(a1, a2, a3, a4)
#define FCDECL5(rettype, funcname, a1, a2, a3, a4, a5) rettype funcname(a1, a2, a3, a4, a5)
#define FCDECL6(rettype, funcname, a1, a2, a3, a4, a5, a6) rettype funcname(a1, a2, a3, a4, a5, a6)
#define FCDECL7(rettype, funcname, a1, a2, a3, a4, a5, a6, a7) rettype funcname(a1, a2, a3, a4, a5, a6, a7)
#define FCDECL8(rettype, funcname, a1, a2, a3, a4, a5, a6, a7, a8) rettype funcname(a1, a2, a3, a4, a5, a6, a7, a8)
#define FCDECL9(rettype, funcname, a1, a2, a3, a4, a5, a6, a7, a8, a9) rettype funcname(a1, a2, a3, a4, a5, a6, a7, a8, a9)
#define FCDECL10(rettype,funcname, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10) rettype funcname(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10)
#define FCDECL11(rettype,funcname, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11) rettype funcname(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11)
#define FCDECL12(rettype,funcname, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12) rettype funcname(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12)
#define FCDECL13(rettype,funcname, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13) rettype funcname(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13)
#define FCDECL14(rettype,funcname, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14) rettype funcname(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14)

#define FCDECL5_IVI(rettype, funcname, a1, a2, a3, a4, a5) rettype funcname(a1, a2, a3, a4, a5)
#define FCDECL5_VII(rettype, funcname, a1, a2, a3, a4, a5) rettype funcname(a1, a2, a3, a4, a5)

#define FCIMPL_PROLOG(funcname)
#define FCIMPL_EPILOG()

#define FCIMPL0(rettype, funcname) rettype funcname() { FCIMPL_PROLOG(funcname)
#define FCIMPL1(rettype, funcname, a1) rettype funcname(a1) {  FCIMPL_PROLOG(funcname)
#define FCIMPL1_V(rettype, funcname, a1) rettype funcname(a1) {  FCIMPL_PROLOG(funcname)
#define FCIMPL2(rettype, funcname, a1, a2) rettype funcname(a1, a2) {  FCIMPL_PROLOG(funcname)
#define FCIMPL2VA(rettype, funcname, a1, a2) rettype funcname(a1, a2, ...) {  FCIMPL_PROLOG(funcname)
#define FCIMPL2_VV(rettype, funcname, a1, a2) rettype funcname(a1, a2) {  FCIMPL_PROLOG(funcname)
#define FCIMPL2_VI(rettype, funcname, a1, a2) rettype funcname(a1, a2) {  FCIMPL_PROLOG(funcname)
#define FCIMPL2_IV(rettype, funcname, a1, a2) rettype funcname(a1, a2) {  FCIMPL_PROLOG(funcname)
#define FCIMPL3(rettype, funcname, a1, a2, a3) rettype funcname(a1, a2, a3) {  FCIMPL_PROLOG(funcname)
#define FCIMPL3_IIV(rettype, funcname, a1, a2, a3) rettype funcname(a1, a2, a3) {  FCIMPL_PROLOG(funcname)
#define FCIMPL3_IVV(rettype, funcname, a1, a2, a3) rettype funcname(a1, a2, a3) {  FCIMPL_PROLOG(funcname)
#define FCIMPL3_VII(rettype, funcname, a1, a2, a3) rettype funcname(a1, a2, a3) {  FCIMPL_PROLOG(funcname)
#define FCIMPL3_IVI(rettype, funcname, a1, a2, a3) rettype funcname(a1, a2, a3) {  FCIMPL_PROLOG(funcname)
#define FCIMPL3_VVI(rettype, funcname, a1, a2, a3) rettype funcname(a1, a2, a3) {  FCIMPL_PROLOG(funcname)
#define FCIMPL4(rettype, funcname, a1, a2, a3, a4) rettype funcname(a1, a2, a3, a4) {  FCIMPL_PROLOG(funcname)
#define FCIMPL5(rettype, funcname, a1, a2, a3, a4, a5) rettype funcname(a1, a2, a3, a4, a5) {  FCIMPL_PROLOG(funcname)
#define FCIMPL6(rettype, funcname, a1, a2, a3, a4, a5, a6) rettype funcname(a1, a2, a3, a4, a5, a6) {  FCIMPL_PROLOG(funcname)
#define FCIMPL7(rettype, funcname, a1, a2, a3, a4, a5, a6, a7) rettype funcname(a1, a2, a3, a4, a5, a6, a7) {  FCIMPL_PROLOG(funcname)
#define FCIMPL8(rettype, funcname, a1, a2, a3, a4, a5, a6, a7, a8) rettype funcname(a1, a2, a3, a4, a5, a6, a7, a8) {  FCIMPL_PROLOG(funcname)
#define FCIMPL9(rettype, funcname, a1, a2, a3, a4, a5, a6, a7, a8, a9) rettype funcname(a1, a2, a3, a4, a5, a6, a7, a8, a9) {  FCIMPL_PROLOG(funcname)
#define FCIMPL10(rettype,funcname, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10) rettype funcname(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10) {  FCIMPL_PROLOG(funcname)
#define FCIMPL11(rettype,funcname, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11) rettype funcname(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11) {  FCIMPL_PROLOG(funcname)
#define FCIMPL12(rettype,funcname, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12) rettype funcname(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12) {  FCIMPL_PROLOG(funcname)
#define FCIMPL13(rettype,funcname, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13) rettype funcname(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13) {  FCIMPL_PROLOG(funcname)
#define FCIMPL14(rettype,funcname, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14) rettype funcname(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14) {  FCIMPL_PROLOG(funcname)

#define FCIMPL5_IVI(rettype, funcname, a1, a2, a3, a4, a5) rettype funcname(a1, a2, a3, a4, a5) { FCIMPL_PROLOG(funcname)
#define FCIMPL5_VII(rettype, funcname, a1, a2, a3, a4, a5) rettype funcname(a1, a2, a3, a4, a5) { FCIMPL_PROLOG(funcname)

#define FCIMPLEND FCIMPL_EPILOG(); }

#define FCALL_CHECK \
        THROWS; \
        DISABLED(GC_TRIGGERS); /* FCALLS with HELPER frames have issues with GC_TRIGGERS */ \
        MODE_COOPERATIVE; \
        SO_TOLERANT

LPVOID __FCThrow(LPVOID me, enum RuntimeExceptionKind reKind, UINT resID, LPCWSTR arg1, LPCWSTR arg2, LPCWSTR arg3);
LPVOID __FCThrowArgument(LPVOID me, enum RuntimeExceptionKind reKind, LPCWSTR argumentName, LPCWSTR resourceName);

//==============================================================================================
// Throws an exception from an FCall. See rexcep.h for a list of valid
// exception codes.
//==============================================================================================
#define FCThrow(reKind) FCThrowEx(reKind, 0, 0, 0, 0)

//==============================================================================================
// This version lets you attach a message with inserts (similar to
// COMPlusThrow()).
//==============================================================================================
#define FCThrowEx(reKind, resID, arg1, arg2, arg3)              \
    {                                                           \
        assert(false);                                          \
    }

//==============================================================================================
// Like FCThrow but can be used for a VOID-returning FCall. The only
// difference is in the "return" statement.
//==============================================================================================
#define FCThrowVoid(reKind) FCThrowExVoid(reKind, 0, 0, 0, 0)

//==============================================================================================
// This version lets you attach a message with inserts (similar to
// COMPlusThrow()).
//==============================================================================================
#define FCThrowExVoid(reKind, resID, arg1, arg2, arg3)          \
    {                                                           \
        assert(false);                                          \
    }
    
// Use FCThrowRes to throw an exception with a localized error message from the
// ResourceManager in managed code.
#define FCThrowRes(reKind, resourceName) FCThrowArgumentEx(reKind, NULL, resourceName)
#define FCThrowArgumentNull(argName) FCThrowArgumentEx(kArgumentNullException, argName, NULL)
#define FCThrowArgumentOutOfRange(argName, message) FCThrowArgumentEx(kArgumentOutOfRangeException, argName, message)
#define FCThrowArgument(argName, message) FCThrowArgumentEx(kArgumentException, argName, message)

#define FCThrowArgumentEx(reKind, argName, resourceName)        \
    {                                                           \
        assert(false);                                          \
    }

// Use FCThrowRes to throw an exception with a localized error message from the
// ResourceManager in managed code.
#define FCThrowResVoid(reKind, resourceName) FCThrowArgumentVoidEx(reKind, NULL, resourceName)
#define FCThrowArgumentNullVoid(argName) FCThrowArgumentVoidEx(kArgumentNullException, argName, NULL)
#define FCThrowArgumentOutOfRangeVoid(argName, message) FCThrowArgumentVoidEx(kArgumentOutOfRangeException, argName, message)
#define FCThrowArgumentVoid(argName, message) FCThrowArgumentVoidEx(kArgumentException, argName, message)

#define FCThrowArgumentVoidEx(reKind, argName, resourceName)    \
    {                                                           \
        assert(false);                                          \
    }

#define HELPER_METHOD_FRAME_BEGIN_PROTECT(gc)
#define HELPER_METHOD_FRAME_BEGIN_RET_PROTECT(gc)
#define HELPER_METHOD_FRAME_BEGIN_ATTRIB(attribs)
#define HELPER_METHOD_FRAME_BEGIN_0()
#define HELPER_METHOD_FRAME_BEGIN_1(arg1)
#define HELPER_METHOD_FRAME_BEGIN_2(arg1, arg2)
#define HELPER_METHOD_FRAME_BEGIN_RET_0()
#define HELPER_METHOD_FRAME_BEGIN_RET_1(arg1)
#define HELPER_METHOD_FRAME_BEGIN_RET_2(arg1, arg2)
#define HELPER_METHOD_FRAME_END()

#define FC_GC_POLL()
#define FC_GC_POLL_RET()
#define FC_GC_POLL_NOT_NEEDED()

#define FCALL_CONTRACT \
    STATIC_CONTRACT_SO_TOLERANT; \
    STATIC_CONTRACT_THROWS; \
    /* FCALLS are a special case contract wise, they are "NOTRIGGER, unless you setup a frame" */ \
    STATIC_CONTRACT_GC_NOTRIGGER; \
    STATIC_CONTRACT_MODE_COOPERATIVE
    
typedef CLR_BOOL FC_BOOL_RET;
typedef UINT8 FC_UINT8_RET;
#define FC_RETURN_BOOL(x)   do { return !!(x); } while(0)

#define FCUnique(unique) { Volatile<int> u = (unique); while (u.LoadWithoutBarrier() == 0) { }; }

#endif //__FCall_h__
