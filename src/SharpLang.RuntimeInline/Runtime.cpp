#include <stdint.h>
#include <stdlib.h>

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