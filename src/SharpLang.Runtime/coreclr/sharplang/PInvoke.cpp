#include "winwrap.h"
#include <stdint.h>
#include <stdio.h>
#include <string.h>

enum class PInvokeAttributes : uint16_t
{
	CharSetAnsi = 0x0002,
	CharSetUnicode = 0x0004,
	CharSetAuto = 0x0006,
	CharSetMask = 0x0006,
};

extern "C" void* PInvokeOpenLibrary(const char* moduleName)
{
	if (strcmp(moduleName, "__Internal") == 0 || strcmp(moduleName, "QCall") == 0 || strcmp(moduleName, "libcoreclr.so") == 0)
    {
        // Current module?
        static HMODULE localModule = GetModuleHandle(NULL);
		return localModule;
    }

	return LoadLibraryA(moduleName);
}

extern "C" void* PInvokeGetProcAddress(void* module, const char* procName, PInvokeAttributes pinvokeAttributes)
{
	// Try to load with exact name first
	auto result = (void*)GetProcAddress((HMODULE)module, procName);
	if (result != NULL)
		return result;

	// Try to append char set suffix (W or A)
	auto charsetProcName = (char*)malloc(strlen(procName) + 2);
	strcpy(charsetProcName, procName);
	switch ((PInvokeAttributes)((uint16_t)pinvokeAttributes & (uint16_t)PInvokeAttributes::CharSetMask))
	{
	case PInvokeAttributes::CharSetAuto:
	case PInvokeAttributes::CharSetUnicode:
		strcat(charsetProcName, "W");
		break;
	case PInvokeAttributes::CharSetAnsi:
	default:
		strcat(charsetProcName, "A");
		break;
	}

	result = (void*)GetProcAddress((HMODULE)module, charsetProcName);
	free(charsetProcName);

	// Try stdcall mangling
	char* buffer = (char*)malloc(strlen(procName) + 12);
	for (int i = 0; i < 128; i += 4)
	{
		sprintf(buffer, "%s@%i", procName, i);
		result = (void*)GetProcAddress((HMODULE)module, buffer);
		if (result != NULL)
			break;
	}
	free(buffer);

	return result;
}
