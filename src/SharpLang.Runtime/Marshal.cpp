#include <windows.h>
#include <stdint.h>
#include <string.h>
#include "RuntimeType.h"
#include "ConvertUTF.h"

extern "C" int32_t System_Runtime_InteropServices_Marshal__GetLastWin32Error__()
{
	return GetLastError();
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
	return ((IUnknown*)pUnk)->AddRef();
}

extern "C" int32_t System_Runtime_InteropServices_Marshal__ReleaseInternal_System_IntPtr_(void* pUnk)
{
	return ((IUnknown*)pUnk)->Release();
}

extern "C" int32_t System_Runtime_InteropServices_Marshal__QueryInterfaceInternal_System_IntPtr_System_Guid__System_IntPtr__(void* pUnk, IID iid, void** ppv)
{
	return ((IUnknown*)pUnk)->QueryInterface(iid, ppv);
}

extern "C" void* System_Runtime_InteropServices_Marshal__AllocHGlobal_System_IntPtr_(size_t size)
{
	return GlobalAlloc(GMEM_FIXED, size);
}

extern "C" void System_Runtime_InteropServices_Marshal__FreeHGlobal_System_IntPtr_(void* hglobal)
{
	GlobalFree(hglobal);
}

extern "C" void* System_Runtime_InteropServices_Marshal__StringToHGlobalAnsi_System_String_(String* str)
{
	uint32_t bufferLength = (str->length + 1) * UNI_MAX_UTF8_BYTES_PER_CODE_POINT;
	uint8_t* buffer = (uint8_t*) System_Runtime_InteropServices_Marshal__AllocHGlobal_System_IntPtr_(bufferLength);
	const uint16_t* src = (const uint16_t*)str->value;
	uint8_t* dest = buffer;
	ConvertUTF16toUTF8(&src, src + str->length, &dest, dest + bufferLength, strictConversion);
	buffer[str->length] = '\0';

	return buffer;
}

extern "C" void* System_Runtime_InteropServices_Marshal__StringToHGlobalUni_System_String_(String* str)
{
	uint32_t bufferLength = (str->length + 1) * sizeof(char16_t);
	uint8_t* buffer = (uint8_t*)System_Runtime_InteropServices_Marshal__AllocHGlobal_System_IntPtr_(bufferLength);
	const uint16_t* src = (const uint16_t*)str->value;
	memcpy(buffer, src, bufferLength);

	return buffer;
}

extern "C" String* System_Runtime_InteropServices_Marshal__PtrToStringAnsi_System_IntPtr_(void* ptr)
{
	if (ptr == NULL)
		return NULL;

	auto length = strlen((char*) ptr);

	auto str = (String*)malloc(sizeof(String));
	str->length = length;
	str->value = (char16_t*)malloc(sizeof(char16_t) * length);
	const_cast<char16_t*>(str->value)[str->length] = 0;

	auto strStart = str->value;
	ConvertUTF8toUTF16((const UTF8**)&ptr, (UTF8*)ptr + length, (UTF16**)&strStart, (UTF16*)strStart + length, strictConversion);

	return str;
}

extern "C" String* System_Runtime_InteropServices_Marshal__PtrToStringUni_System_IntPtr_(void* ptr)
{
	if (ptr == NULL)
		return NULL;

	auto length = 0;
	char16_t* lastChar = (char16_t*)ptr;
	while (*lastChar++)
		length++;

	auto str = (String*)malloc(sizeof(String));
	str->length = length;
	str->value = (char16_t*)malloc(sizeof(char16_t) * length);
	const_cast<char16_t*>(str->value)[str->length] = 0;

	memcpy((void*)str->value, ptr, sizeof(char16_t) * length);

	return str;
}

extern "C" int32_t System_Runtime_InteropServices_Marshal__SizeOf_System_Type_(RuntimeType* type)
{
	// TODO: Ugly hack, this is not a valid implementation, but should work out temporarily for simplest cases.
	return type->runtimeEEType->objectSize - sizeof(Object);
}
