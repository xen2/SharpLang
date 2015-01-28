#ifdef _WIN32
#include <windows.h>
#endif
#include <stdint.h>
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
#ifdef _WIN32
	// Current module?
	if (strcmp(moduleName, "__Internal") == 0)
		return GetModuleHandle(NULL);

	return LoadLibrary(moduleName);
#else
	return NULL;
#endif
}

extern "C" void* PInvokeGetProcAddress(void* module, const char* procName, PInvokeAttributes pinvokeAttributes)
{
#ifdef _WIN32
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
	return result;
#else
	return NULL;
#endif
}
