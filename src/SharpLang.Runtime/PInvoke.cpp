#include <windows.h>

extern "C" void* PInvokeOpenLibrary(const char* moduleName)
{
	return LoadLibrary(moduleName);
}

extern "C" void* PInvokeGetProcAddress(void* module, const char* procName)
{
	return (void*)GetProcAddress((HMODULE)module, procName);
}
