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

extern "C" void System_Runtime_InteropServices_Marshal__copy_to_unmanaged_System_Array_System_Int32_System_IntPtr_System_Int32_(Array<uint8_t>* source, int32_t sourceIndex, uint8_t* dest, int32_t length)
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

extern "C" void System_Runtime_InteropServices_Marshal__copy_from_unmanaged_System_IntPtr_System_Int32_System_Array_System_Int32_(uint8_t* source, int32_t destIndex, Array<uint8_t>* dest, int32_t length)
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

extern "C" int32_t System_Runtime_InteropServices_Marshal__AddRefInternal_System_IntPtr_(void* pUnk)
{
#ifdef _WIN32
	return ((IUnknown*)pUnk)->AddRef();
#else
	assert(false);
#endif
}

extern "C" int32_t System_Runtime_InteropServices_Marshal__ReleaseInternal_System_IntPtr_(void* pUnk)
{
#ifdef _WIN32
	return ((IUnknown*)pUnk)->Release();
#else
	assert(false);
#endif
}

#ifdef _WIN32
extern "C" int32_t System_Runtime_InteropServices_Marshal__QueryInterfaceInternal_System_IntPtr_System_Guid__System_IntPtr__(void* pUnk, IID iid, void** ppv)
{
	return ((IUnknown*)pUnk)->QueryInterface(iid, ppv);
}
#endif

extern "C" void* System_Runtime_InteropServices_Marshal__AllocHGlobal_System_IntPtr_(size_t size)
{
#ifdef _WIN32
	return GlobalAlloc(GMEM_FIXED, size);
#else
	return malloc(size);
#endif
}

extern "C" void System_Runtime_InteropServices_Marshal__FreeHGlobal_System_IntPtr_(void* hglobal)
{
#ifdef _WIN32
	GlobalFree(hglobal);
#else
	free(hglobal);
#endif
}

extern "C" void* System_Runtime_InteropServices_Marshal__StringToHGlobalAnsi_System_String_(String* str)
{
	uint32_t bufferLength = (str->length + 1) * UNI_MAX_UTF8_BYTES_PER_CODE_POINT;
	uint8_t* buffer = (uint8_t*) System_Runtime_InteropServices_Marshal__AllocHGlobal_System_IntPtr_(bufferLength);
	const uint16_t* src = (const uint16_t*)&str->firstChar;
	uint8_t* dest = buffer;
	ConvertUTF16toUTF8(&src, src + str->length, &dest, dest + bufferLength, strictConversion);
	buffer[str->length] = '\0';

	return buffer;
}

extern "C" void* System_Runtime_InteropServices_Marshal__StringToHGlobalUni_System_String_(String* str)
{
	uint32_t bufferLength = (str->length + 1) * sizeof(char16_t);
	uint8_t* buffer = (uint8_t*)System_Runtime_InteropServices_Marshal__AllocHGlobal_System_IntPtr_(bufferLength);
	const uint16_t* src = (const uint16_t*)&str->firstChar;
	memcpy(buffer, src, bufferLength);

	return buffer;
}

extern "C" String* System_Runtime_InteropServices_Marshal__PtrToStringAnsi_System_IntPtr_(void* ptr)
{
	if (ptr == NULL)
		return NULL;

	return String::Create((char*)ptr);
}

extern "C" String* System_Runtime_InteropServices_Marshal__PtrToStringUni_System_IntPtr_(void* ptr)
{
	if (ptr == NULL)
		return NULL;

	return String::Create((char16_t*)ptr);
}

extern "C" int32_t System_Runtime_InteropServices_Marshal__SizeOf_System_Type_(RuntimeType* type)
{
	// TODO: Ugly hack, this is not a valid implementation, but should work out temporarily for simplest cases.
	return type->runtimeEEType->objectSize - sizeof(Object);
}
