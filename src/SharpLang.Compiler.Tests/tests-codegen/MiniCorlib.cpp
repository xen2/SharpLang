// Temporary minimal runtime library for testing purpose.
// It should gradually be replaced by a real runtime.

#include <stdio.h>
#include <stdint.h>
#include <string.h>

#include "../../SharpLang.Runtime/ConvertUTF.h"
#include "../../SharpLang.Runtime/RuntimeType.h"

struct Int32 : Object
{
	int32_t value;
};

// Empty functions that pretend to set culture on thread UI so that real .NET execute them.
// No need to worry anymore about culture when using Console.WriteLine.
extern "C" void System_Globalization_CultureInfo___ctor_System_String_(void* a, void* b)
{
	// Ignored
}

extern "C" void System_Threading_Thread__set_CurrentCulture_System_Globalization_CultureInfo_(void* a, void* b)
{
	// Ignored
}

extern "C" void* System_Threading_Thread__get_CurrentThread__()
{
	// Ignored
	return NULL;
}

// System.Exception..ctor()
extern "C" void System_Exception___ctor__(void* exception)
{
}

// System.InvalidCastException..ctor()
extern "C" void System_InvalidCastException___ctor__(void* exception)
{
}

// System.OverflowException..ctor()
extern "C" void System_OverflowException___ctor__(void* exception)
{
}

// System.NotSupportedException..ctor(string)
extern "C" void System_NotSupportedException___ctor_System_String_(void* exception, StringObject* str)
{
}

// System.IntPtr::op_Explicit(void*)
extern "C" void* System_IntPtr__op_Explicit_System_Void_(void* p)
{
	return p;
}

// System.UIntPtr::op_Explicit(ulong)
extern "C" void* System_UIntPtr__op_Explicit_System_UInt64(uint64_t p)
{
	return (void*)p;
}

// void System.Console.WriteLine(string)
extern "C" void System_Console__WriteLine_System_String_(StringObject* str)
{
	if (str == NULL)
		printf("\n");
	else
	{
		//printf("%.*s\n", str->length, &str->firstChar);
		uint32_t bufferLength = str->length * UNI_MAX_UTF8_BYTES_PER_CODE_POINT;
		uint8_t* buffer = (uint8_t*)malloc(bufferLength);
		const uint16_t* src = (const uint16_t*)&str->firstChar;
		uint8_t* dest = buffer;
		ConvertUTF16toUTF8(&src, src + str->length, &dest, dest + bufferLength, strictConversion);
		printf("%.*s\n", dest - buffer, buffer);
		free(buffer);
	}
}

// void System.Console.WriteLine(int)
extern "C" void System_Console__WriteLine_System_Int32_(int32_t i)
{
	printf("%i\n", i);
}

// void System.Console.WriteLine(uint)
extern "C" void System_Console__WriteLine_System_UInt32_(uint32_t i)
{
	printf("%u\n", i);
}

// void System.Console.WriteLine(int)
extern "C" void System_Console__WriteLine_System_Int64_(int64_t i)
{
	printf("%lli\n", i);
}

// void System.Console.WriteLine(uint)
extern "C" void System_Console__WriteLine_System_UInt64_(uint64_t i)
{
	printf("%llu\n", i);
}

// void System.Console.WriteLine(uint)
extern "C" void System_Console__WriteLine_System_Boolean_(uint8_t b)
{
	printf("%s\n", b != 0 ? "True" : "False");
}

// void System.Console.WriteLine(float)
extern "C" void System_Console__WriteLine_System_Single_(float f)
{
	// Not exact, but works for current test
	char buffer[64];
	int length = sprintf(buffer, "%.3f", f);

	// Remove trailing 0 (after .)
	if (strchr(buffer, '.') != NULL)
	{
		while (length > 0 && buffer[length - 1] == '0')
			buffer[--length] = '\0';
	}

	if (length > 0 && buffer[length - 1] == '.')
		buffer[--length] = '\0';

	printf("%s\n", buffer);
}

// void System.Console.WriteLine(double)
extern "C" void System_Console__WriteLine_System_Double_(double f)
{
	// Not exact, but works for current test
	char buffer[64];
	int length = sprintf(buffer, "%.11f", f);

	// Remove trailing 0 (after .)
	if (strchr(buffer, '.') != NULL)
	{
		while (length > 0 && buffer[length - 1] == '0')
			buffer[--length] = '\0';
	}

	if (length > 0 && buffer[length - 1] == '.')
		buffer[--length] = '\0';

	printf("%s\n", buffer);
}

// void System.Object..ctor()
extern "C" void System_Object___ctor__(void* obj)
{
}

// void System.ValueType.Equals(object)
extern "C" uint8_t System_ValueType__Equals_System_Object_(struct Object* boxedValue, struct Object* obj)
{
	if (obj == 0)
		return 0;

	if (boxedValue->eeType != obj->eeType)
		return 0;

	// Compare area after object header
	// TODO: Store actual size in EEType and use it for comparison size.
	// Currently using 4 for SimpleGenericConstrained.cs.
	return memcmp(boxedValue + 1, obj + 1, 4) == 0;
}

// bool System.Int32.Equals(object)
extern "C" uint8_t System_Int32__Equals_System_Object_(int32_t* i, void* obj)
{
	return *i == ((Int32*)obj)->value ? 1 : 0;
}

extern "C" Object* System_SharpLangModule__ResolveType_System_SharpLangEEType__(EEType* eeType)
{
	// Needed by System_Object__GetType__
	assert(false);
}

extern "C" ArrayBase* System_SharpLangType__MakeArrayType__(RuntimeType* elementType)
{
	// Needed by System_Array__CreateInstanceImpl_System_Type_System_Int32___System_Int32___
	assert(false);
}

extern "C" bool System_RuntimeType__IsSubclassOf_System_Type_(RuntimeType* a, RuntimeType* b)
{
	// Needed by System_RuntimeTypeHandle__CanCastTo_System_RuntimeType_System_RuntimeType_
	assert(false);
}

extern "C" bool System_RuntimeTypeHandle__IsInterface_System_RuntimeType_(RuntimeType* type)
{
	// Needed by System_RuntimeTypeHandle__CanCastTo_System_RuntimeType_System_RuntimeType_
	assert(false);
}

// Needed by AllocateObject
EEType System_Threading_Thread_rtti;

EEType System_Byte___rtti;

class Module;
extern "C" RuntimeType* System_SharpLangModule__ResolveType_System_Byte__System_Byte__(Module* module, const char* _namespace, const char* name)
{
	assert(false);
}

extern "C" FieldDesc* System_SharpLangEEType__FindField_System_Byte__(EEType* type, const char* name)
{
	assert(false);
}
