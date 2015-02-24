#include <stdint.h>
#include <stdlib.h>
#include <assert.h>
#include "RuntimeType.h"
#include "ConvertUTF.h"
#include "char-category-data.h"
#include "char-conversions.h"
#include "number-formatter.h"
#ifdef _WIN32
#include <windows.h>
#else
#include <thread>
#include <sys/utsname.h>
#endif

static Object* AllocateObject(EEType* eeType)
{
	auto objectSize = eeType->objectSize;
	Object* object = (Object*) malloc(objectSize);

	// TODO: Maybe we could avoid zero-ing memory in various cases?
	memset(object, 0, objectSize);

	object->eeType = eeType;
	return object;
}

// TODO: Emit IL directly?
extern "C" Object* System_SharpLangHelper__UnsafeCast_System_Object_System_Object_(Object* obj)
{
	return obj;
}

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

extern "C" int32_t System_Runtime_CompilerServices_RuntimeHelpers__GetHashCode_System_Object_(Object* obj)
{
	// We don't have a GC yet, so use object address
	return (int32_t)(intptr_t)obj;
}

extern "C" bool System_Type__EqualsInternal_System_Type_(RuntimeType* a, RuntimeType* b)
{
	return a == b;
}

extern "C" Object* System_Type__internal_from_handle_System_IntPtr_(EEType* eeType)
{
	return System_SharpLangModule__ResolveType_System_SharpLangEEType__(eeType);
}

extern "C" bool System_RuntimeType__IsSubclassOf_System_Type_(RuntimeType* a, RuntimeType* b);
extern "C" bool System_RuntimeTypeHandle__IsInterface_System_RuntimeType_(RuntimeType* type);

extern "C" bool System_RuntimeTypeHandle__CanCastTo_System_RuntimeType_System_RuntimeType_(RuntimeType* type, RuntimeType* target)
{
	// TODO: Covariance/contravariance
	// TODO: type is interface?
	if (System_RuntimeTypeHandle__IsInterface_System_RuntimeType_(target))
	{
		// Currently not supported
		return isInstInterface(type->runtimeEEType, target->runtimeEEType);
	}

	// Note: using EE type (when available) is probably faster than having to resolve System.Type at every step
	return System_RuntimeType__IsSubclassOf_System_Type_(target, type);
}

extern "C" Object* System_RuntimeTypeHandle__CreateInstance_System_RuntimeType_System_Boolean_System_Boolean_System_Boolean__System_RuntimeMethodHandleInternal__System_Boolean__(RuntimeType* type, bool publicOnly, bool noCheck, bool* canBeCached, void* ctor, bool* needSecurityCheck)
{
	*canBeCached = false;

	// TODO: Find and execute ctor!
	return AllocateObject(type->runtimeEEType);
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

extern "C" void System_Array__Clear_System_Array_System_Int32_System_Int32_(Array<uint8_t>* arr, int32_t index, int32_t length)
{
	int32_t elementSize = arr->eeType->elementSize;

	memset((void*)(arr->value + index * elementSize), 0, elementSize * length);
}

extern "C" bool System_Array__Copy_System_Array_System_Int32_System_Array_System_Int32_System_Int32_System_Boolean_(Array<uint8_t>* source, int32_t sourceIndex, Array<uint8_t>* dest, int32_t destIndex, int32_t length, bool reliable)
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

extern "C" int32_t System_String__IndexOf_System_Char_System_Int32_System_Int32_(StringObject* str, char16_t value, int32_t startIndex, int32_t count)
{
	int32_t lastIndex = startIndex + count;
	for (int32_t i = startIndex; i < lastIndex; ++i)
	{
		if ((&str->firstChar)[i] == value)
			return i;
	}

	return -1;
}

extern "C" int32_t System_String__LastIndexOf_System_Char_System_Int32_System_Int32_(StringObject* str, char16_t value, int32_t startIndex, int32_t count)
{
	int32_t lastIndex = startIndex - count + 1;
	for (int32_t i = startIndex; i >= lastIndex; --i)
	{
		if ((&str->firstChar)[i] == value)
			return i;
	}

	return -1;
}

extern "C" int32_t System_ParseNumbers__StringToInt_System_String_System_Int32_System_Int32_System_Int32__(StringObject* str, int32_t radix, int32_t flags, int32_t* curPos)
{
	// TODO: Use coreclr implementation
	char buffer[64];
	auto sourceStart = &str->firstChar + *curPos;
	auto destStart = buffer;
	ConvertUTF16toUTF8((const UTF16**)&sourceStart, (UTF16*)sourceStart + str->length, (UTF8**)&destStart, (UTF8*)destStart + 64, strictConversion);

	char* endptr;
	auto result = strtol(buffer, &endptr, radix);
	*curPos += endptr - buffer;
	return result;
}

extern "C" int64_t System_ParseNumbers__StringToLong_System_String_System_Int32_System_Int32_System_Int32__(StringObject* str, int32_t radix, int32_t flags, int32_t* curPos)
{
	// TODO: Use coreclr implementation
	char buffer[64];
	auto sourceStart = &str->firstChar + *curPos;
	auto destStart = buffer;
	ConvertUTF16toUTF8((const UTF16**) &sourceStart, (UTF16*) sourceStart + str->length, (UTF8**) &destStart, (UTF8*) destStart + 64, strictConversion);

	char* endptr;
	auto result = strtoll(buffer, &endptr, radix);
	*curPos += endptr - buffer;
	return result;
}

extern "C" StringObject* System_Environment__GetOSVersionString__()
{
#ifdef _WIN32
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
	return StringObject::NewString(buffer, length);
#else
	struct utsname name;
	if (uname(&name) == 0)
		return StringObject::NewString(name.release);
	return StringObject::NewString("0.0.0.1");
#endif
}

extern "C" void System_Threading_Monitor__ReliableEnter_System_Object_System_Boolean__(Object* object, bool& lockTaken)
{
	// Not implemented yet
}

extern "C" void System_Threading_Monitor__Exit_System_Object_(Object* object)
{
	// Not implemented yet
}

extern "C" void System_Threading_Thread__MemoryBarrier__()
{
	// Not implemented yet
}

extern "C" Object* System_Threading_Interlocked__CompareExchange_System_Object__System_Object_System_Object_(Object** location1, Object* value, Object* comparand)
{
	return (Object*) InterlockedCompareExchangePointer((PVOID*) location1, value, comparand);
}

extern "C" StringObject* System_Text_Encoding__InternalCodePage_System_Int32__(int32_t* code_page)
{
	// ASCII
	*code_page = 1;
	return NULL;
}

extern "C" StringObject* System_Environment__GetNewLine__()
{
	// TODO: String RTTI
	static StringObject* newline = StringObject::NewString(u"\r\n");
	return newline;
}

extern "C" int32_t System_String__get_Length__(StringObject* str)
{
	// TODO: Make it an intrinsic for optimization
	return str->length;
}

extern "C" char16_t System_String__get_Chars_System_Int32_(StringObject* str, int32_t index)
{
	return (&str->firstChar)[index];
}

extern "C" StringObject* System_String__FastAllocateString_System_Int32_(int32_t length)
{
	return StringObject::NewString(length);
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

extern "C" RuntimeType* System_Type__GetTypeFromHandle_System_RuntimeTypeHandle_(RuntimeType* runtimeType)
{
	return runtimeType;
}

extern "C" StringObject* System_Globalization_CultureInfo__get_current_locale_name__()
{
	// Redirect to invariant culture by using an empty string ("")
	// TODO: mechanism to setup VTable
	static StringObject* locale = StringObject::NewString(u"");
	return locale;
}

extern "C" Object* System_Threading_Thread__GetCurrentThreadNative__()
{
	static Thread* thread = (Thread*)AllocateObject(&System_Threading_Thread_rtti);
	return thread;
}

extern "C" int32_t System_AppDomain__GetId__()
{
	// For now, we only support one AppDomain
	return 1;
}

extern "C" void System_Runtime_CompilerServices_RuntimeHelpers__InitializeArray_System_Array_System_RuntimeFieldHandle_(Array<uint8_t>* arr, uint8_t* fieldHandle)
{
	memcpy((void*)arr->value, (const void*)fieldHandle, arr->length * arr->eeType->elementSize);
}

extern "C" void System_Runtime_CompilerServices_RuntimeHelpers__ProbeForSufficientStack__()
{
}

extern "C" Object* System_GC__get_ephemeron_tombstone__()
{
	return NULL;
}

extern "C" void System_GC___SuppressFinalize_System_Object_(Object* obj)
{
}

extern "C" void System_Buffer__InternalBlockCopy_System_Array_System_Int32_System_Array_System_Int32_System_Int32_(Array<uint8_t>* src, int32_t srcOffset, Array<uint8_t>* dst, int32_t dstOffset, int32_t count)
{
	auto srcB = src->value + srcOffset;
	auto destB = dst->value + dstOffset;

	if (src == dst) // Move inside same array
		memmove((void*)destB, (void*)srcB, count);
	else
		memcpy((void*)destB, (void*)srcB, count);
}

extern "C" void System_Buffer__BlockCopy_System_Array_System_Int32_System_Array_System_Int32_System_Int32_(Array<uint8_t>* src, int32_t srcOffset, Array<uint8_t>* dst, int32_t dstOffset, int32_t count)
{
	System_Buffer__InternalBlockCopy_System_Array_System_Int32_System_Array_System_Int32_System_Int32_(src, srcOffset, dst, dstOffset, count);
}

extern "C" bool System_Security_SecurityManager__get_SecurityEnabled__()
{
	return false;
}

extern "C" StringObject* System_Environment__internalGetEnvironmentVariable_System_String_(StringObject* variable)
{
#if _WIN32
	// Query length first
	auto valueLength = GetEnvironmentVariableW((LPCWSTR) variable->firstChar, NULL, 0);
	if (valueLength == 0 && GetLastError() == ERROR_ENVVAR_NOT_FOUND)
		return NULL;

	// Allocate string
	// GetEnvironmentVariable will add null-terminating character, but we shouldn't count this in length
	auto value = StringObject::NewString(valueLength - 1);

	// Read actual value
	auto actualValueLength = GetEnvironmentVariableW((LPCWSTR)variable->firstChar, (LPWSTR)&value->firstChar, valueLength);

	// TODO: Check value didn't change behind our back? (actualValueLength changed)

	return value;
#else
	assert(false);
#endif
}

// ThunkPointers is defined by LLVM
extern void* ThunkPointers[4096];
void* ThunkTargets[4096];

uint32_t ThunkCurrentId;

extern "C" void** SharpLang_Marshalling_MarshalHelper__GetThunkTargets__()
{
	return ThunkTargets;
}

extern "C" void** SharpLang_Marshalling_MarshalHelper__GetThunkPointers__()
{
	return ThunkPointers;
}

extern "C" uint32_t SharpLang_Marshalling_MarshalHelper__GetThunkCurrentId__()
{
	return ThunkCurrentId;
}

extern "C" StringObject* System_Number__FormatInt32_System_Int32_System_String_System_Globalization_NumberFormatInfo_(int32_t value, StringObject* format, Object* info)
{
	char buffer[64];
	sprintf(buffer, "%i", value);
	return StringObject::NewString(buffer);
}

// QCall
extern "C" __declspec(dllexport) bool __stdcall InternalUseRandomizedHashing()
{
	return false;
}

extern "C" __declspec(dllexport) int32_t __stdcall GetProcessorCount()
{
#ifdef _WIN32
	SYSTEM_INFO systemInfo;
	GetSystemInfo(&systemInfo);
	return systemInfo.dwNumberOfProcessors;
#else
	return std::thread::hardware_concurrency();
#endif
}


extern "C" __declspec(dllexport) bool __stdcall InternalGetDefaultLocaleName(int localType, StringObject** localeString)
{
	return false;
}

extern "C" __declspec(dllexport) bool __stdcall InternalGetUserDefaultUILanguage(char16_t* localeString)
{
	return false;
}

extern "C" __declspec(dllexport) void* __stdcall NativeInternalInitSortHandle(char16_t* localeName, void** handleOrigin)
{
	handleOrigin = NULL;
	return NULL;
}