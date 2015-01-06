#include <stdint.h>
#include <stdlib.h>
#include <assert.h>
#include "RuntimeType.h"
#include "ConvertUTF.h"
#include "char-category-data.h"
#include "char-conversions.h"
#include "number-formatter.h"
#include <windows.h>

extern "C" void* System_SharpLangHelper__GetObjectPointer_System_Object_(Object* obj)
{
	return obj;
}

extern "C" Object* System_SharpLangHelper__GetObjectFromPointer_System_Void__(void* obj)
{
	return (Object*)obj;
}

extern "C" Object* System_Object__MemberwiseClone__(Object* obj)
{
	// Object size
	auto length = obj->eeType->objectSize;

	// Allocate new object of same size
	auto objCopy = (Object*)malloc(length);

	// Blindly copy data
	// TODO: Improve this with write barrier?
	memcpy(objCopy, obj, length);

	return objCopy;
}

// TODO: Implement this C# side
extern "C" Object* System_SharpLangModule__ResolveType_System_SharpLangEEType__(EEType* eeType);
extern "C" Object* System_Object__GetType__(Object* obj)
{
	return System_SharpLangModule__ResolveType_System_SharpLangEEType__(obj->eeType);
}

extern "C" bool System_Type__EqualsInternal_System_Type_(RuntimeType* a, RuntimeType* b)
{
	return a == b;
}

extern "C" Object* System_Type__internal_from_handle_System_IntPtr_(EEType* eeType)
{
	return System_SharpLangModule__ResolveType_System_SharpLangEEType__(eeType);
}

extern "C" bool System_Type__type_is_subtype_of_System_Type_System_Type_System_Boolean_(RuntimeType* a, RuntimeType* b, bool checkInterfaces)
{
	assert(!checkInterfaces);

	auto rttiA = a->runtimeEEType;
	auto rttiB = b->runtimeEEType;
	while (rttiA != NULL)
	{
		if (rttiA == rttiB)
			return true;

		rttiA = rttiA->base;
	}

	return false;
}

extern "C" bool System_Type__type_is_assignable_from_System_Type_System_Type_(RuntimeType* a, RuntimeType* b)
{
	// TODO: Check interfaces
	return System_Type__type_is_subtype_of_System_Type_System_Type_System_Boolean_(b, a, false);
}

extern "C" int32_t System_Array__GetLength_System_Int32_(ArrayBase* arr, int32_t dimension)
{
	// Only support 1-dimensional arrays for now
	// TODO: Need something better than assert (i.e. throw NotSupportedException, even on Release?)
	assert(dimension == 0);

	return arr->length;
}

extern "C" int32_t System_Array__GetRank__(ArrayBase* arr)
{
	// Only support 1-dimensional arrays for now
	return 1;
}

extern "C" int32_t System_Array__GetLowerBound_System_Int32_(ArrayBase* arr)
{
	// Only support 1-dimensional arrays for now
	return 0;
}

extern "C" void System_Array__ClearInternal_System_Array_System_Int32_System_Int32_(Array<uint8_t>* arr, int32_t index, int32_t length)
{
	int32_t elementSize = arr->eeType->elementSize;

	memset((void*)(arr->value + index * elementSize), 0, elementSize * length);
}

extern "C" bool System_Array__FastCopy_System_Array_System_Int32_System_Array_System_Int32_System_Int32_(Array<uint8_t>* source, int32_t sourceIndex, Array<uint8_t>* dest, int32_t destIndex, int32_t length)
{
	// TODO: Temporary implementation.
	// Later, we should perform additional checks (i.e. if element types are compatible, etc...)
	//if (source->base.eeType != dest->base.eeType)
	//	return false;

	// Check bounds
	if (sourceIndex + length > source->length
		|| destIndex + length > dest->length)
		return false;

	// Get element size
	int32_t elementSize = source->eeType->elementSize;

	memcpy((void*)(dest->value + destIndex * elementSize), (const void*)(source->value + sourceIndex * elementSize), elementSize * length);

	return true;
}

extern "C" RuntimeType* System_SharpLangType__MakeArrayType__(RuntimeType* elementType);
extern EEType System_Object___rtti;

extern "C" ArrayBase* System_Array__CreateInstanceImpl_System_Type_System_Int32___System_Int32___(RuntimeType* elementType, Array<int32_t>* lengths, Array<int32_t>* bounds)
{
	assert(lengths->length == 1);
	assert(bounds == NULL);

	auto length = lengths->value[0];

	auto arrayType = System_SharpLangType__MakeArrayType__(elementType);

	auto result = (Array<uint8_t>*)malloc(sizeof(Array<uint8_t>));
	result->eeType = arrayType->runtimeEEType;
	result->length = length;
	result->value = (uint8_t*) malloc(result->eeType->elementSize * length);

	return result;
}

extern "C" int32_t System_Environment__get_Platform__()
{
	// Windows
	return 2;
}

extern "C" int32_t System_Environment__get_ProcessorCount__()
{
	SYSTEM_INFO systemInfo;
	GetSystemInfo(&systemInfo);

	return systemInfo.dwNumberOfProcessors;
}

extern "C" String* System_Environment__GetOSVersionString__()
{
	OSVERSIONINFOEX versionInfo;
	versionInfo.dwOSVersionInfoSize = sizeof(versionInfo);
	if (!GetVersionEx((OSVERSIONINFO*)&versionInfo))
		memset(&versionInfo, 0, sizeof(versionInfo));

	// Reserve string with enough space
	char buffer[64];
	auto length = sprintf(buffer, "%i.%i.%i.%i",
		(int32_t)versionInfo.dwMajorVersion, (int32_t)versionInfo.dwMinorVersion,
		(int32_t)versionInfo.dwBuildNumber, (int32_t)(versionInfo.wServicePackMajor << 16));

	assert(length < sizeof(buffer));

	// We are not expecting any non ASCII characters, so we can use sprintf size as is.
	auto str = (String*)malloc(sizeof(String));
	str->length = length;
	str->value = (char16_t*)malloc(sizeof(char16_t) * length);

	auto strStart = str->value;
	ConvertUTF8toUTF16((const UTF8**)&buffer, (UTF8*)buffer + length, (UTF16**)&strStart, (UTF16*)strStart + length, strictConversion);

	return str;
}

extern "C" void System_Threading_Monitor__Enter_System_Object_(Object* object)
{
	// Not implemented yet
}

extern "C" void System_Threading_Monitor__Exit_System_Object_(Object* object)
{
	// Not implemented yet
}

extern "C" void System_Threading_Monitor__try_enter_with_atomic_var_System_Object_System_Int32_System_Boolean__(Object* object, int32_t millisecondsTimeout, bool& lockTaken)
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
	static String newline = String(2, u"\r\n");
	return &newline;
}

extern "C" void System_String___ctor_System_Char___(String* str, Array<char16_t>* value)
{
	auto length = value->length;
	str->length = length;
	str->value = (char16_t*)malloc(sizeof(char16_t) * length);
	memcpy((void*)str->value, (void*)value->value, length * sizeof(char16_t));
	const_cast<char16_t*>(str->value)[str->length] = 0;
}

extern "C" void System_String___ctor_System_Char___System_Int32_System_Int32_(String* str, Array<char16_t>* value, int startIndex, int length)
{
	str->length = length;
	str->value = (char16_t*)malloc(sizeof(char16_t) * length);
	memcpy((void*)str->value, (void*)(value->value + startIndex), length * sizeof(char16_t));
	const_cast<char16_t*>(str->value)[str->length] = 0;
}

extern "C" String* System_String__InternalAllocateStr_System_Int32_(int32_t length)
{
	auto str = (String*)malloc(sizeof(String));
	str->eeType = &System_String_rtti;
	str->length = length;
	str->value = (char16_t*)malloc(sizeof(char16_t) * length);
	const_cast<char16_t*>(str->value)[str->length] = 0;
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
	static String locale = String(0, u"");
	return &locale;
}

extern "C" Object* System_Threading_Thread__CurrentInternalThread_internal__()
{
	return NULL;
}

extern "C" int32_t System_Threading_Thread__GetDomainID__()
{
	// For now, we only support one AppDomain
	return 1;
}

// int System.Runtime.CompilerServices.RuntimeHelpers.get_OffsetToStringData()
extern "C" int32_t System_Runtime_CompilerServices_RuntimeHelpers__get_OffsetToStringData__()
{
	return 0;
}

extern "C" void System_Runtime_CompilerServices_RuntimeHelpers__InitializeArray_System_Array_System_IntPtr_(Array<uint8_t>* arr, uint8_t* fieldHandle)
{
	memcpy((void*)arr->value, (const void*)fieldHandle, arr->length * arr->eeType->elementSize);
}

static Object* AllocateObject(EEType* eeType)
{
	auto objectSize = eeType->objectSize;
	Object* object = (Object*)malloc(objectSize);

	// TODO: Maybe we could avoid zero-ing memory in various cases?
	memset(object, 0, objectSize);

	object->eeType = eeType;
	return object;
}

extern "C" void System_GC__SuppressFinalize_System_Object_(Object* obj)
{
}

extern "C" Object* System_GC__get_ephemeron_tombstone__()
{
	return NULL;
}

extern "C" bool System_Buffer__BlockCopyInternal_System_Array_System_Int32_System_Array_System_Int32_System_Int32_(Array<uint8_t>* src, int32_t src_offset, Array<uint8_t>* dest, int32_t dest_offset, int32_t count)
{
	auto srcB = src->value + src_offset;
	auto destB = dest->value + dest_offset;

	if (src == dest) // Move inside same array
		memmove((void*)destB, (void*)srcB, count);
	else
		memcpy((void*)destB, (void*)srcB, count);

	return true;
}

extern "C" double System_Math__Floor_System_Double_(double d)
{
	return floor(d);
}

extern "C" double System_Math__Round_System_Double_(double d)
{
	// TODO: Math.Round is different from C++ round, need to make a better implementation
	return round(d);
}

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

extern "C" bool System_Security_SecurityManager__get_SecurityEnabled__()
{
	return false;
}

extern "C" String* System_Environment__internalGetEnvironmentVariable_System_String_(String* variable)
{
	// Query length first
	auto valueLength = GetEnvironmentVariableW((LPCWSTR)variable->value, NULL, 0);
	if (valueLength == 0 && GetLastError() == ERROR_ENVVAR_NOT_FOUND)
		return NULL;

	// Allocate string
	auto value = (String*)malloc(sizeof(String));
	value->value = (char16_t*)malloc(sizeof(char16_t) * valueLength);
	value->length = valueLength - 1; // GetEnvironmentVariable will add null-terminating character, but we shouldn't count this in length

	// Read actual value
	auto actualValueLength = GetEnvironmentVariableW((LPCWSTR) variable->value, (LPWSTR)value->value, valueLength);

	// TODO: Check value didn't change behind our back? (actualValueLength changed)

	return value;
}

extern "C" Object* System_Threading_Interlocked__CompareExchange_System_Object_T__T_T_(Object** location1, Object* value, Object* comparand)
{
	auto result = *location1;
	if (*location1 == comparand)
		*location1 = value;

	return result;
}

