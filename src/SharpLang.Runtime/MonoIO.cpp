#include "RuntimeType.h"
#ifdef _WIN32
#include <windows.h>
#endif

extern "C" char16_t System_IO_MonoIO__get_VolumeSeparatorChar__()
{
	return u':';
}

extern "C" char16_t System_IO_MonoIO__get_DirectorySeparatorChar__()
{
	return u'\\';
}

extern "C" char16_t System_IO_MonoIO__get_AltDirectorySeparatorChar__()
{
	return u'/';
}

extern "C" char16_t System_IO_MonoIO__get_PathSeparator__()
{
	return u';';
}

#ifdef _WIN32
extern "C" void* System_IO_MonoIO__get_ConsoleOutput__()
{
	return GetStdHandle(STD_OUTPUT_HANDLE);
}

extern "C" void* System_IO_MonoIO__get_ConsoleInput__()
{
	return GetStdHandle(STD_INPUT_HANDLE);
}

extern "C" void* System_IO_MonoIO__get_ConsoleError__()
{
	return GetStdHandle(STD_ERROR_HANDLE);
}

extern "C" int32_t System_IO_MonoIO__GetFileAttributes_System_String_System_IO_MonoIOError__(String* path, int32_t* error)
{
	*error = ERROR_SUCCESS;

	auto result = GetFileAttributesW((LPCWSTR)path->value);

	if (result == -1)
		*error = GetLastError();

	// TODO: It seems FindFirstFile might be necessary in case file is already opened

	return result;
}

extern "C" int32_t System_IO_MonoIO__Write_System_IntPtr_System_Byte___System_Int32_System_Int32_System_IO_MonoIOError__(void* handle, Array<uint8_t>* src, int32_t src_offset, int32_t count, int32_t* error)
{
	*error = ERROR_SUCCESS;

	DWORD written;
	bool result = WriteFile(handle, src->value, count, &written, NULL);
    if (!result)
	{
		*error = GetLastError();
		written = -1;        
	}

	return written;
}

extern "C" uint32_t System_IO_MonoIO__GetFileType_System_IntPtr_System_IO_MonoIOError__(void* handle, int32_t* error)
{
	*error = ERROR_SUCCESS;

	uint32_t result = GetFileType(handle);
	if (result == FILE_TYPE_UNKNOWN)
	{
		*error = GetLastError();
	}

	return result;
}
#endif