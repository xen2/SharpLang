#include <stdint.h>
#include <stdlib.h>
#include "RuntimeType.h"
#include "char-category-data.h"
#include "char-conversions.h"
#include "number-formatter.h"
#include <windows.h>

extern "C" int32_t System_Environment__get_Platform__()
{
	// Windows
	return 2;
}

extern "C" void System_Threading_Monitor__Enter_System_Object_(Object* object)
{
	// Not implemented yet
}

extern "C" void System_Threading_Monitor__Exit_System_Object_(Object* object)
{
	// Not implemented yet
}

extern "C" void System_Threading_Monitor__try_enter_with_atomic_var_System_Object_System_Int32_System_Boolean__(Object* object)
{
	// Not implemented yet
}

extern "C" String* System_Text_Encoding__InternalCodePage_System_Int32__(int32_t* code_page)
{
	// ASCII
	*code_page = 1;
	return NULL;
}

extern "C" String* System_Environment__GetNewLine__()
{
	// TODO: String RTTI
	static String newline = { { NULL }, 2, u"\r\n" };
	return &newline;
}

extern "C" void System_String___ctor_System_Char___System_Int32_System_Int32_(String* str, Array<char16_t>* value, int startIndex, int length)
{
	str->length = length;
	str->value = (char16_t*)malloc(sizeof(char16_t) * length);
	memcpy((void*)str->value, (void*)(value->value + startIndex), length * sizeof(char16_t));
}

extern "C" String* System_String__InternalAllocateStr_System_Int32_(int32_t length)
{
	auto str = (String*)malloc(sizeof(String));
	str->length = length;
	str->value = (char16_t*)malloc(sizeof(char16_t) * length);    
	return str;
}

extern "C" int32_t System_String__GetLOSLimit__()
{
	return INT32_MAX;
}

extern "C" void System_Char__GetDataTablePointers_System_Int32_System_Byte___System_UInt16___System_Byte___System_Double___System_UInt16___System_UInt16___System_UInt16___System_UInt16___(
					    int category_data_version, uint8_t const **category_data, uint16_t const **category_astral_index,
					    uint8_t const **numeric_data, double const **numeric_data_values,
					    uint16_t const **to_lower_data_low, uint16_t const **to_lower_data_high,
					    uint16_t const **to_upper_data_low, uint16_t const **to_upper_data_high)
{
	*category_data = CategoryData;
	*numeric_data = NumericData;
	*numeric_data_values = NumericDataValues;
	*to_lower_data_low = ToLowerDataLow;
	*to_lower_data_high = ToLowerDataHigh;
	*to_upper_data_low = ToUpperDataLow;
	*to_upper_data_high = ToUpperDataHigh;
}

extern "C" void System_NumberFormatter__GetFormatterTables_System_UInt64___System_Int32___System_Char___System_Char___System_Int64___System_Int32___ (
					uint64_t const **mantissas,
					int32_t const **exponents,
					char16_t const **digitLowerTable,
					char16_t const **digitUpperTable,
					int64_t const **tenPowersList,
					int32_t const **decHexDigits)
{
	*mantissas = Formatter_MantissaBitsTable;
	*exponents = Formatter_TensExponentTable;
	*digitLowerTable = Formatter_DigitLowerTable;
	*digitUpperTable = Formatter_DigitUpperTable;
	*tenPowersList = Formatter_TenPowersList;
	*decHexDigits = Formatter_DecHexDigits;
}

extern "C" String* System_Globalization_CultureInfo__get_current_locale_name__()
{
	// Redirect to invariant culture by using an empty string ("")
	// TODO: mechanism to setup VTable
	static String locale = { { NULL }, 0, u"" };
	return &locale;
}

extern "C" Object* System_Threading_Thread__CurrentInternalThread_internal__()
{
	return NULL;
}

// int System.Runtime.CompilerServices.RuntimeHelpers.get_OffsetToStringData()
extern "C" int32_t System_Runtime_CompilerServices_RuntimeHelpers__get_OffsetToStringData__()
{
	return 0;
}

extern "C" void System_GC__SuppressFinalize_System_Object_(Object* obj)
{
}

extern "C" Object* System_GC__get_ephemeron_tombstone__()
{
	return NULL;
}

extern "C" bool System_Buffer__BlockCopyInternal_System_Array_System_Int32_System_Array_System_Int32_System_Int32_(Object* src, int32_t src_offset, Object* dest, int32_t dest_offset, int32_t count)
{
	auto srcB = ((Array<uint8_t>*)src)->value + src_offset;
	auto destB = ((Array<uint8_t>*)dest)->value + dest_offset;

	if (srcB == destB) // Move inside same array
		memmove((void*)destB, (void*)srcB, count);
	else
		memcpy((void*)destB, (void*)srcB, count);

	return true;
}

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
