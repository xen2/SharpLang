#include <stdint.h>
#include <stdlib.h>
#include <unwind.h>
#include <string.h>
#include <stdio.h>

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
	printf("cleanup exception\n");
	if (ex != NULL)
		free(ex);
}

extern "C" _Unwind_Reason_Code sharpPersonality(int version, _Unwind_Action actions, uint64_t exceptionClass, struct _Unwind_Exception* exceptionObject, struct _Unwind_Context* context)
{
	printf("personality exception\n");
	return _URC_CONTINUE_UNWIND;
}

// Temporarily here to force _Unwind_RaiseException to be emitted.
extern "C" void throwException() __attribute__((noreturn));
extern "C" void throwException()
{
	printf("raise exception\n");
	struct _Unwind_Exception* ex = (struct _Unwind_Exception*)malloc(sizeof(struct _Unwind_Exception));
	memset(ex, 0, sizeof(*ex));
	ex->exception_class = 0x0101010101010101; // TODO
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