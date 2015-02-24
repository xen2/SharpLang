#ifdef _WIN32
#include <windows.h>
#endif
#include <stdint.h>
#include <string.h>
#include "RuntimeType.h"
#include "ConvertUTF.h"

extern "C" int32_t System_Runtime_InteropServices_Marshal__GetLastWin32Error__()
{
#ifdef _WIN32
	return GetLastError();
#else
	assert(false);
#endif
}

extern "C" int32_t System_Runtime_InteropServices_Marshal__SizeOfHelper_System_Type_System_Boolean_(RuntimeType* type, bool throwIfNotMarshalable)
{
	// TODO: Ugly hack, this is not a valid implementation, but should work out temporarily for simplest cases.
	return type->runtimeEEType->objectSize - sizeof(Object);
}

extern "C" int32_t System_Runtime_InteropServices_Marshal__GetSystemMaxDBCSCharSize__()
{
	// TODO: Use CoreCLR implementation
	return 2;
}

extern "C" void System_Runtime_InteropServices_Marshal__CopyToNative_System_Object_System_Int32_System_IntPtr_System_Int32_(Array<uint8_t>* source, int32_t sourceIndex, uint8_t* dest, int32_t length)
{
	// TODO: Null checks
	// Check bounds
	if (sourceIndex + length > source->length)
	{
		// TODO: Throw exception
		return;
	}

	// Get element size
	int32_t elementSize = source->eeType->elementSize;

	memcpy((void*) dest, (const void*) (source->value + sourceIndex), elementSize * length);
}

extern "C" void System_Runtime_InteropServices_Marshal__CopyToManaged_System_IntPtr_System_Object_System_Int32_System_Int32_(uint8_t* source, Array<uint8_t>* dest, int32_t destIndex, int32_t length)
{
	// TODO: Null checks
	// Check bounds
	if (destIndex + length > dest->length)
	{
		// TODO: Throw exception
		return;
	}

	// Get element size
	int32_t elementSize = dest->eeType->elementSize;

	memcpy((void*) (dest->value + destIndex), (const void*) source, elementSize * length);
}
