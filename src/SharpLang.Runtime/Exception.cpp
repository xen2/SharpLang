#ifdef __SEH__
#include <Windows.h>
#endif

#include <cstdint>
#include <cstdlib>
#include <unwind.h>
#include <cstring>
#include <cstdio>

#include "llvm/Support/Dwarf.h"

#include "RuntimeType.h"

// TODO: Improve and unify code so that SEH and DWARF shares most of the code
// TODO: cleanupException is not called
// TODO: Investigate why ExceptionInfo needs aligned (x86) and alloc padding (x64)

struct ExceptionInfo
{
	Object* exceptionObject;
	struct _Unwind_Exception unwindException;
#ifdef __SEH__
	// TODO: Investigate why this is needed (otherwise crash)
	int padding[8];
#endif
} __attribute__((__aligned__));

int64_t exceptionBaseFromUnwindOffset = ((uintptr_t) (((ExceptionInfo*) (NULL)))) - ((uintptr_t) &(((ExceptionInfo*) (NULL))->unwindException));

/// Read a uleb128 encoded value and advance pointer
/// See Variable Length Data in:
/// @link http://dwarfstd.org/Dwarf3.pdf @unlink
/// @param data reference variable holding memory pointer to decode from
/// @returns decoded value
static uintptr_t readULEB128(const uint8_t **data) {
	uintptr_t result = 0;
	uintptr_t shift = 0;
	unsigned char byte;
	const uint8_t *p = *data;

	do {
		byte = *p++;
		result |= (byte & 0x7f) << shift;
		shift += 7;
	} while (byte & 0x80);

	*data = p;

	return result;
}


/// Read a sleb128 encoded value and advance pointer
/// See Variable Length Data in:
/// @link http://dwarfstd.org/Dwarf3.pdf @unlink
/// @param data reference variable holding memory pointer to decode from
/// @returns decoded value
static uintptr_t readSLEB128(const uint8_t **data) {
	uintptr_t result = 0;
	uintptr_t shift = 0;
	unsigned char byte;
	const uint8_t *p = *data;

	do {
		byte = *p++;
		result |= (byte & 0x7f) << shift;
		shift += 7;
	} while (byte & 0x80);

	*data = p;

	if ((byte & 0x40) && (shift < (sizeof(result) << 3))) {
		result |= (~0 << shift);
	}

	return result;
}

unsigned getEncodingSize(uint8_t Encoding) {
	if (Encoding == llvm::dwarf::DW_EH_PE_omit)
		return 0;

	switch (Encoding & 0x0F) {
	case llvm::dwarf::DW_EH_PE_absptr:
		return sizeof(uintptr_t);
	case llvm::dwarf::DW_EH_PE_udata2:
		return sizeof(uint16_t);
	case llvm::dwarf::DW_EH_PE_udata4:
		return sizeof(uint32_t);
	case llvm::dwarf::DW_EH_PE_udata8:
		return sizeof(uint64_t);
	case llvm::dwarf::DW_EH_PE_sdata2:
		return sizeof(int16_t);
	case llvm::dwarf::DW_EH_PE_sdata4:
		return sizeof(int32_t);
	case llvm::dwarf::DW_EH_PE_sdata8:
		return sizeof(int64_t);
	default:
		// not supported
		abort();
	}
}

/// Read a pointer encoded value and advance pointer
/// See Variable Length Data in:
/// @link http://dwarfstd.org/Dwarf3.pdf @unlink
/// @param data reference variable holding memory pointer to decode from
/// @param encoding dwarf encoding type
/// @returns decoded value
static uintptr_t readEncodedPointer(const uint8_t **data, uint8_t encoding) {
	uintptr_t result = 0;
	const uint8_t *p = *data;

	if (encoding == llvm::dwarf::DW_EH_PE_omit)
		return(result);

	// first get value
	switch (encoding & 0x0F) {
	case llvm::dwarf::DW_EH_PE_absptr:
		result = *((uintptr_t*) p);
		p += sizeof(uintptr_t);
		break;
	case llvm::dwarf::DW_EH_PE_uleb128:
		result = readULEB128(&p);
		break;
		// Note: This case has not been tested
	case llvm::dwarf::DW_EH_PE_sleb128:
		result = readSLEB128(&p);
		break;
	case llvm::dwarf::DW_EH_PE_udata2:
		result = *((uint16_t*) p);
		p += sizeof(uint16_t);
		break;
	case llvm::dwarf::DW_EH_PE_udata4:
		result = *((uint32_t*) p);
		p += sizeof(uint32_t);
		break;
	case llvm::dwarf::DW_EH_PE_udata8:
		result = *((uint64_t*) p);
		p += sizeof(uint64_t);
		break;
	case llvm::dwarf::DW_EH_PE_sdata2:
		result = *((int16_t*) p);
		p += sizeof(int16_t);
		break;
	case llvm::dwarf::DW_EH_PE_sdata4:
		result = *((int32_t*) p);
		p += sizeof(int32_t);
		break;
	case llvm::dwarf::DW_EH_PE_sdata8:
		result = *((int64_t*) p);
		p += sizeof(int64_t);
		break;
	default:
		// not supported
		abort();
		break;
	}

	// then add relative offset
	switch (encoding & 0x70) {
	case llvm::dwarf::DW_EH_PE_absptr:
		// do nothing
		break;
	case llvm::dwarf::DW_EH_PE_pcrel:
		result += (uintptr_t) (*data);
		break;
	case llvm::dwarf::DW_EH_PE_textrel:
	case llvm::dwarf::DW_EH_PE_datarel:
	case llvm::dwarf::DW_EH_PE_funcrel:
	case llvm::dwarf::DW_EH_PE_aligned:
	default:
		// not supported
		abort();
		break;
	}

	// then apply indirection
	if (encoding & llvm::dwarf::DW_EH_PE_indirect) {
		result = *((uintptr_t*) result);
	}

	*data = p;

	return result;
}

static bool handleActionValue(int64_t *resultAction,
	uint8_t TTypeEncoding,
	const uint8_t *classInfo,
	uintptr_t actionEntry,
    struct ExceptionInfo* exceptionInfo)
{
	const uint8_t *actionPos = (uint8_t*)actionEntry;

	for (int i = 0; true; ++i)
	{
		// Each emitted dwarf action corresponds to a 2 tuple of
		// type info address offset, and action offset to the next
		// emitted action.
		int64_t typeOffset = readSLEB128(&actionPos);
		const uint8_t* tempActionPos = actionPos;
		int64_t actionOffset = readSLEB128(&tempActionPos);

		// Note: A typeOffset == 0 implies that a cleanup llvm.eh.selector
		//       argument has been matched.
		if (typeOffset > 0)
		{
			unsigned EncSize = getEncodingSize(TTypeEncoding);
			const uint8_t *EntryP = classInfo - typeOffset * EncSize;
			uintptr_t P = readEncodedPointer(&EntryP, TTypeEncoding);
			
			// Expected exception type
			struct EEType* expectedExceptionType = reinterpret_cast<struct EEType*>(P);

			// Actual exception type
			struct EEType* exceptionType = exceptionInfo->exceptionObject->eeType;

			// Check if they match (by testing each class in hierarchy)
			while (exceptionType != NULL)
			{
				if (exceptionType == expectedExceptionType)
				{
					*resultAction = typeOffset; // or should it be i + 1?
					return true;
				}

				exceptionType = exceptionType->base;
			}
		}

		if (!actionOffset)
			break;

		actionPos += actionOffset;
	}

	// No match
	return false;
}

void ParseLSDA(const uint8_t *lsda, uint8_t *ttypeEncoding, uint8_t *callSiteEncoding, const uint8_t **callSiteTableStart, const uint8_t **callSiteTableEnd, const uint8_t **classInfo)
{
	// Note: See JITDwarfEmitter::EmitExceptionTable(...) for corresponding
	//       dwarf emission

	// Parse LSDA header.
	uint8_t lpStartEncoding = *lsda++;

	if (lpStartEncoding != llvm::dwarf::DW_EH_PE_omit) {
		readEncodedPointer(&lsda, lpStartEncoding);
	}

	*ttypeEncoding = *lsda++;
	uintptr_t classInfoOffset;

	if (*ttypeEncoding != llvm::dwarf::DW_EH_PE_omit) {
		// Calculate type info locations in emitted dwarf code which
		// were flagged by type info arguments to llvm.eh.selector
		// intrinsic
		classInfoOffset = readULEB128(&lsda);
		*classInfo = lsda + classInfoOffset;
	}

	// Walk call-site table looking for range that
	// includes current PC.

	*callSiteEncoding = *lsda++;
	uint32_t callSiteTableLength = readULEB128(&lsda);
	*callSiteTableStart = lsda;
	*callSiteTableEnd = *callSiteTableStart + callSiteTableLength;
}

#ifdef __SEH__
// TODO: Reorganize method so as to share most of it with its DWARF counterpart (by adding our own interface to query/set IP, GR, unwind, etc...)
extern "C" EXCEPTION_DISPOSITION sharpPersonality(EXCEPTION_RECORD *record,
                                void *frame,
                                CONTEXT *context,
                                DISPATCHER_CONTEXT *dispatch)
{
	if (record->ExceptionFlags & EXCEPTION_TARGET_UNWIND)
	{
		// Restore rdx (stored in exception information)
		context->Rdx = record->ExceptionInformation[1];
		return ExceptionContinueSearch;
	}

	EXCEPTION_DISPOSITION ret = ExceptionContinueSearch;
	struct _Unwind_Exception* exceptionObject = (struct _Unwind_Exception*)record->ExceptionInformation[0];

	const uint8_t* lsda = (const uint8_t*)dispatch->HandlerData;

	uintptr_t pc = dispatch->ControlPc - 1;

	uintptr_t funcStart = (uintptr_t)dispatch->ImageBase + (uintptr_t)dispatch->FunctionEntry->BeginAddress;
	uintptr_t pcOffset = pc - funcStart;
	const uint8_t *classInfo = NULL;

	uint8_t ttypeEncoding;
	uint8_t callSiteEncoding;
	const uint8_t *callSiteTableStart;
	const uint8_t *callSiteTableEnd;
	ParseLSDA(lsda, &ttypeEncoding, &callSiteEncoding, &callSiteTableStart, &callSiteTableEnd, &classInfo);
	const uint8_t *actionTableStart = callSiteTableEnd;
	const uint8_t *callSitePtr = callSiteTableStart;

	while (callSitePtr < callSiteTableEnd) {
		uintptr_t start = readEncodedPointer(&callSitePtr, callSiteEncoding);
		uintptr_t length = readEncodedPointer(&callSitePtr, callSiteEncoding);
		uintptr_t landingPad = readEncodedPointer(&callSitePtr, callSiteEncoding);

		// Note: Action value
		uintptr_t actionEntry = readULEB128(&callSitePtr);

		if (landingPad == 0) {
			continue; // no landing pad for this entry
		}

		if (actionEntry) {
			actionEntry += ((uintptr_t) actionTableStart) - 1;
		}

		bool exceptionMatched = false;

		if ((start <= pcOffset) && (pcOffset < (start + length))) {
			int64_t actionValue = 0;

			struct ExceptionInfo* exceptionInfo = (struct ExceptionInfo*)((char*)exceptionObject + exceptionBaseFromUnwindOffset);

			if (actionEntry) {
				exceptionMatched = handleActionValue(&actionValue,
					ttypeEncoding,
					classInfo,
					actionEntry,
					exceptionInfo);
			}

			//if (record->ExceptionFlags & (EXCEPTION_UNWINDING | EXCEPTION_EXIT_UNWIND)) {
			{
				// Found landing pad for the PC.
				// Set Instruction Pointer to so we re-enter function
				// at landing pad. The landing pad is created by the
				// compiler to take two parameters in registers.
				record->NumberParameters = 2;

				// Note: this virtual register directly corresponds
				//       to the return of the llvm.eh.selector intrinsic
				if (!actionEntry || !exceptionMatched) {
					// We indicate cleanup only
					record->ExceptionInformation[1] = 0;
				}
				else {
					// Matched type info index of llvm.eh.selector intrinsic
					// passed here.
					record->ExceptionInformation[1] = actionValue;
				}

				// To execute landing pad set here
				RtlUnwindEx(frame, (PVOID)(funcStart + landingPad), record, exceptionInfo->exceptionObject, context, dispatch->HistoryTable);
				__builtin_unreachable();
			}
			//else if (exceptionMatched) {
			//	RtlUnwindEx(frame, (PVOID)(funcStart + landingPad), record, exceptionInfo->exceptionObject, context, dispatch->HistoryTable);
			//	ret = ExceptionContinueSearch;
			//}
			//else {
				// Note: Only non-clean up handlers are marked as
				//       found. Otherwise the clean up handlers will be
				//       re-found and executed during the clean up
				//       phase.
			//}

			break;
		}
	}

	return(ret);
}
#else
extern "C" _Unwind_Reason_Code sharpPersonality(int version, _Unwind_Action actions, uint64_t exceptionClass, struct _Unwind_Exception* exceptionObject, struct _Unwind_Context* context)
{
	const uint8_t *lsda = (const uint8_t*)_Unwind_GetLanguageSpecificData(context);
	uintptr_t pc = _Unwind_GetIP(context) - 1;

	_Unwind_Reason_Code ret = _URC_CONTINUE_UNWIND;

	// Get beginning current frame's code (as defined by the
	// emitted dwarf code)
	uintptr_t funcStart = _Unwind_GetRegionStart(context);
	uintptr_t pcOffset = pc - funcStart;
	const uint8_t *classInfo = NULL;

	uint8_t ttypeEncoding;
	uint8_t callSiteEncoding;
	const uint8_t *callSiteTableStart;
	const uint8_t *callSiteTableEnd;
	ParseLSDA(lsda, &ttypeEncoding, &callSiteEncoding, &callSiteTableStart, &callSiteTableEnd, &classInfo);
	const uint8_t *actionTableStart = callSiteTableEnd;
	const uint8_t *callSitePtr = callSiteTableStart;

	while (callSitePtr < callSiteTableEnd) {
		uintptr_t start = readEncodedPointer(&callSitePtr, callSiteEncoding);
		uintptr_t length = readEncodedPointer(&callSitePtr, callSiteEncoding);
		uintptr_t landingPad = readEncodedPointer(&callSitePtr, callSiteEncoding);

		// Note: Action value
		uintptr_t actionEntry = readULEB128(&callSitePtr);

		//if (exceptionClass != ourBaseExceptionClass) {
		//	// We have been notified of a foreign exception being thrown,
		//	// and we therefore need to execute cleanup landing pads
		//	actionEntry = 0;
		//}

		if (landingPad == 0) {
			continue; // no landing pad for this entry
		}

		if (actionEntry) {
			actionEntry += ((uintptr_t) actionTableStart) - 1;
		}

		bool exceptionMatched = false;

		if ((start <= pcOffset) && (pcOffset < (start + length))) {
			int64_t actionValue = 0;
            
            struct ExceptionInfo* exceptionInfo = (struct ExceptionInfo*)((char*)exceptionObject + exceptionBaseFromUnwindOffset);

			if (actionEntry) {
				exceptionMatched = handleActionValue(&actionValue,
					ttypeEncoding,
					classInfo,
					actionEntry,
					exceptionInfo);
			}

			if (!(actions & _UA_SEARCH_PHASE)) {
				// Found landing pad for the PC.
				// Set Instruction Pointer to so we re-enter function
				// at landing pad. The landing pad is created by the
				// compiler to take two parameters in registers.
				_Unwind_SetGR(context,
					__builtin_eh_return_data_regno(0),
					(uintptr_t)exceptionInfo);

				// Note: this virtual register directly corresponds
				//       to the return of the llvm.eh.selector intrinsic
				if (!actionEntry || !exceptionMatched) {
					// We indicate cleanup only
					_Unwind_SetGR(context,
						__builtin_eh_return_data_regno(1),
						0);
				}
				else {
					// Matched type info index of llvm.eh.selector intrinsic
					// passed here.
					_Unwind_SetGR(context,
						__builtin_eh_return_data_regno(1),
						actionValue);
				}

				// To execute landing pad set here
				_Unwind_SetIP(context, funcStart + landingPad);
				ret = _URC_INSTALL_CONTEXT;
			}
			else if (exceptionMatched) {
				ret = _URC_HANDLER_FOUND;
			}
			else {
				// Note: Only non-clean up handlers are marked as
				//       found. Otherwise the clean up handlers will be
				//       re-found and executed during the clean up
				//       phase.
			}

			break;
		}
	}

	return(ret);
}
#endif

void cleanupException(_Unwind_Reason_Code reason, struct _Unwind_Exception* ex)
{
	if (ex != NULL)
	{
		// TODO: Check exception class
		free((char*)ex + exceptionBaseFromUnwindOffset);
	}
}

extern "C" void throwException(Object* obj)
{
	struct ExceptionInfo* ex = (struct ExceptionInfo*)_aligned_malloc(sizeof(struct ExceptionInfo), 16);
	memset(ex, 0, sizeof(*ex));
	ex->exceptionObject = obj;
	ex->unwindException.exception_class = 0; // TODO
	ex->unwindException.exception_cleanup = cleanupException;
	_Unwind_RaiseException(&ex->unwindException);
	__builtin_unreachable(); 
}

#ifdef __SEH__
extern "C" uint32_t System_Reflection_Assembly__GetCallStack_System_IntPtr___(Array<uintptr_t>* result)
{
	UNWIND_HISTORY_TABLE history;
	CONTEXT context;
	DISPATCHER_CONTEXT dispatch;

	memset(&history, 0, sizeof(history));
	memset(&dispatch, 0, sizeof(dispatch));

	context.ContextFlags = CONTEXT_ALL;
	RtlCaptureContext(&context);

	int entryCount = 0;

	while (true)
	{
		// Find function start
		ULONGLONG imageBase;
		auto functionEntry = RtlLookupFunctionEntry(context.Rip, &imageBase, &history);

		if (!functionEntry)
			break;

		// Add result
		result->value[entryCount++] = imageBase + functionEntry->BeginAddress;

		// Too many entries?
		if (entryCount == result->length)
			break;

		// Unwind next frame
		PVOID handlerData;
		ULONG64 establisherFrame;
		RtlVirtualUnwind(0, imageBase, context.Rip, functionEntry, &context, &handlerData, &establisherFrame, NULL);

		if (context.Rip == 0)
			break;
	}

	return entryCount;
}

#else
struct CallstackData
{
	CallstackData(Array<uintptr_t>* callback) : callstack(callback), entries(0) {}

	Array<uintptr_t>* callstack;
	int entries;
};

static _Unwind_Reason_Code trace_func(struct _Unwind_Context* context, void* arg)
{
	auto data = (CallstackData*)arg;
	uintptr_t pc = _Unwind_GetIP(context);
	if (pc != 0)
	{
		uintptr_t funcStart = _Unwind_GetRegionStart(context);
		data->callstack->value[data->entries++] = funcStart;
		if (data->entries == data->callstack->length)
			return _URC_NORMAL_STOP;
	}

	return(_URC_NO_REASON);
}

// TODO: A variant that can take a List, so that it can appends instead of predetermined size
extern "C" uint32_t System_Reflection_Assembly__GetCallStack_System_IntPtr___(Array<uintptr_t>* result)
{
	CallstackData data(result);
	_Unwind_Backtrace(&trace_func, &data);
	return data.entries;
}
#endif
