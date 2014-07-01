#include <stdint.h>
#include <stdlib.h>
#include <unwind.h>
#include <string.h>
#include <stdio.h>

#include "llvm/Support/Dwarf.h"

typedef struct RuntimeTypeInfo
{
	RuntimeTypeInfo* base;
	uint32_t superTypeCount;
	uint32_t interfacesCount;
	RuntimeTypeInfo** superTypes;
	RuntimeTypeInfo** interfaceMap;
	void* interfaceMethodTable[19];
	void* virtualTable[0];
} RuntimeTypeInfo;

void cleanupException(_Unwind_Reason_Code reason, struct _Unwind_Exception* ex)
{
	if (ex != NULL)
		free(ex);
}

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

extern "C" _Unwind_Reason_Code sharpPersonality(int version, _Unwind_Action actions, uint64_t exceptionClass, struct _Unwind_Exception* exceptionObject, struct _Unwind_Context* context)
{
	const uint8_t *lsda = (const uint8_t*)_Unwind_GetLanguageSpecificData(context);
	uintptr_t pc = _Unwind_GetIP(context) - 1;

	_Unwind_Reason_Code ret = _URC_CONTINUE_UNWIND;

	// Get beginning current frame's code (as defined by the
	// emitted dwarf code)
	uintptr_t funcStart = _Unwind_GetRegionStart(context);
	uintptr_t pcOffset = pc - funcStart;
	const uint8_t *ClassInfo = NULL;

	// Note: See JITDwarfEmitter::EmitExceptionTable(...) for corresponding
	//       dwarf emission

	// Parse LSDA header.
	uint8_t lpStartEncoding = *lsda++;

	if (lpStartEncoding != llvm::dwarf::DW_EH_PE_omit) {
		readEncodedPointer(&lsda, lpStartEncoding);
	}

	uint8_t ttypeEncoding = *lsda++;
	uintptr_t classInfoOffset;

	if (ttypeEncoding != llvm::dwarf::DW_EH_PE_omit) {
		// Calculate type info locations in emitted dwarf code which
		// were flagged by type info arguments to llvm.eh.selector
		// intrinsic
		classInfoOffset = readULEB128(&lsda);
		ClassInfo = lsda + classInfoOffset;
	}

	// Walk call-site table looking for range that
	// includes current PC.

	uint8_t         callSiteEncoding = *lsda++;
	uint32_t        callSiteTableLength = readULEB128(&lsda);
	const uint8_t   *callSiteTableStart = lsda;
	const uint8_t   *callSiteTableEnd = callSiteTableStart +
		callSiteTableLength;
	const uint8_t   *actionTableStart = callSiteTableEnd;
	const uint8_t   *callSitePtr = callSiteTableStart;

	while (callSitePtr < callSiteTableEnd) {
		uintptr_t start = readEncodedPointer(&callSitePtr,
			callSiteEncoding);
		uintptr_t length = readEncodedPointer(&callSitePtr,
			callSiteEncoding);
		uintptr_t landingPad = readEncodedPointer(&callSitePtr,
			callSiteEncoding);

		// Note: Action value
		uintptr_t actionEntry = readULEB128(&callSitePtr);

		//if (exceptionClass != ourBaseExceptionClass) {
		//	// We have been notified of a foreign exception being thrown,
		//	// and we therefore need to execute cleanup landing pads
		//	actionEntry = 0;
		//}

		if (landingPad == 0) {
#ifdef DEBUG
			fprintf(stderr,
				"handleLsda(...): No landing pad found.\n");
#endif

			continue; // no landing pad for this entry
		}

		if (actionEntry) {
			actionEntry += ((uintptr_t) actionTableStart) - 1;
		}
		else {
#ifdef DEBUG
			fprintf(stderr,
				"handleLsda(...):No action table found.\n");
#endif
		}

		bool exceptionMatched = false;

		if ((start <= pcOffset) && (pcOffset < (start + length))) {
#ifdef DEBUG
			fprintf(stderr,
				"handleLsda(...): Landing pad found.\n");
#endif
			int64_t actionValue = 0;

			if (actionEntry) {
				exceptionMatched = true;
				//exceptionMatched = handleActionValue(&actionValue,
				//	ttypeEncoding,
				//	ClassInfo,
				//	actionEntry,
				//	exceptionClass,
				//	exceptionObject);
			}

			if (!(actions & _UA_SEARCH_PHASE)) {
#ifdef DEBUG
				fprintf(stderr,
					"handleLsda(...): installed landing pad "
					"context.\n");
#endif

				// Found landing pad for the PC.
				// Set Instruction Pointer to so we re-enter function
				// at landing pad. The landing pad is created by the
				// compiler to take two parameters in registers.
				_Unwind_SetGR(context,
					__builtin_eh_return_data_regno(0),
					(uintptr_t) exceptionObject);

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
#ifdef DEBUG
				fprintf(stderr,
					"handleLsda(...): setting handler found.\n");
#endif
				ret = _URC_HANDLER_FOUND;
			}
			else {
				// Note: Only non-clean up handlers are marked as
				//       found. Otherwise the clean up handlers will be
				//       re-found and executed during the clean up
				//       phase.
#ifdef DEBUG
				fprintf(stderr,
					"handleLsda(...): cleanup handler found.\n");
#endif
			}

			break;
		}
	}

	return(ret);
}

extern "C" void throwException()
{
	struct _Unwind_Exception* ex = (struct _Unwind_Exception*)malloc(sizeof(struct _Unwind_Exception));
	memset(ex, 0, sizeof(*ex));
	ex->exception_class = 0; // TODO
	ex->exception_cleanup = cleanupException;
	_Unwind_RaiseException(ex);
	__builtin_unreachable();
}

extern "C" bool isInstInterface(const RuntimeTypeInfo* runtimeTypeInfo, const RuntimeTypeInfo* expectedInterface)
{
	auto currentInterface = runtimeTypeInfo->interfaceMap;
	for (int i = 0; i < runtimeTypeInfo->interfacesCount; ++i)
	{
		if (*currentInterface == expectedInterface)
			return true;
		currentInterface++;
	}

	return false;
}

extern "C" void* allocObject(uint32_t size)
{
    return malloc(size);
}

typedef struct IMTEntry
{
	int32_t methodId;
	void* methodPointer;
} IMTEntry;

extern "C" void* resolveInterfaceCall(uint32_t method, void* content)
{
	void* result;

	if (((size_t)content & 1) == 0)
	{
		// Fast path: only one entry in this IMT slot
		result = (void*)content;
	}
	else
	{
		// Normal path: multiple entry in this IMT slot, go through the list
		IMTEntry* imtEntry = (IMTEntry*)((size_t)content & ~1);
		while (imtEntry->methodId != method && imtEntry->methodId != 0) { imtEntry++; }

		result = imtEntry->methodPointer;

		// TODO: Slow path: dispatch to add new entry (should only happen with either variance or generics methods)
		//assert(result != 0);
	}

	return result;
}