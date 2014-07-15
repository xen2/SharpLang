// Temporary minimal runtime library for testing purpose.
// It should gradually be replaced by a real runtime.

#include <stdio.h>
#include <stdint.h>

// Match runtime String struct layout
typedef struct RuntimeTypeInfo
{
	struct RuntimeTypeInfo* base;
	uint32_t superTypeCount;
	uint32_t interfacesCount;
	struct RuntimeTypeInfo** superTypes;
	struct RuntimeTypeInfo** interfaceMap;
	void* interfaceMethodTable[19];
	void* virtualTable[0];
} RuntimeTypeInfo;

typedef struct Object
{
	struct RuntimeTypeInfo* runtimeTypeInfo;
} Object;

typedef struct String
{
	struct Object base;
	size_t length;
	char* value;
} String;

typedef struct Int32
{
	struct Object base;
	int32_t value;
} Int32;

// Empty functions that pretend to set culture on thread UI so that real .NET execute them.
// No need to worry anymore about culture when using Console.WriteLine.
void System_Void_System_Globalization_CultureInfo___ctor_System_String_(void* a, void* b)
{
	// Ignored
}

void System_Void_System_Threading_Thread__set_CurrentCulture_System_Globalization_CultureInfo_(void* a, void* b)
{
	// Ignored
}

void* System_Threading_Thread_System_Threading_Thread__get_CurrentThread__()
{
	// Ignored
	return NULL;
}

// System.Exception..ctor()
void System_Void_System_Exception___ctor__(void* exception)
{
}

// System.InvalidCastException..ctor()
void System_Void_System_InvalidCastException___ctor__(void* exception)
{
}

// void System.Console.WriteLine(string)
void System_Void_System_Console__WriteLine_System_String_(String* str)
{
	if (str == NULL)
		printf("\n");
	else
		printf("%.*s\n", (uint32_t)str->length, str->value);
}

// void System.Console.WriteLine(int)
void System_Void_System_Console__WriteLine_System_Int32_(int32_t i)
{
	printf("%i\n", i);
}

// void System.Console.WriteLine(uint)
void System_Void_System_Console__WriteLine_System_UInt32_(uint32_t i)
{
	printf("%u\n", i);
}

// void System.Console.WriteLine(int)
void System_Void_System_Console__WriteLine_System_Int64_(int64_t i)
{
	printf("%lli\n", i);
}

// void System.Console.WriteLine(uint)
void System_Void_System_Console__WriteLine_System_UInt64_(uint64_t i)
{
	printf("%llu\n", i);
}

// void System.Console.WriteLine(uint)
void System_Void_System_Console__WriteLine_System_Boolean_(uint8_t b)
{
	printf("%s\n", b != 0 ? "True" : "False");
}

// void System.Console.WriteLine(float)
void System_Void_System_Console__WriteLine_System_Single_(float f)
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
void System_Void_System_Console__WriteLine_System_Double_(double f)
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
void System_Void_System_Object___ctor__(void* obj)
{
}

// void System.ValueType.Equals(object)
uint8_t System_Boolean_System_ValueType__Equals_System_Object_(struct Object* boxedValue, struct Object* obj)
{
	if (obj == 0)
		return 0;

	if (boxedValue->runtimeTypeInfo != obj->runtimeTypeInfo)
		return 0;

	// Compare area after object header
	// TODO: Store actual size in RuntimeTypeInfo and use it for comparison size.
	// Currently using 4 for SimpleGenericConstrained.cs.
	return memcmp(boxedValue + 1, obj + 1, 4) == 0;
}

// bool System.Int32.Equals(object)
uint8_t System_Boolean_System_Int32__Equals_System_Object_(int32_t* i, void* obj)
{
	return *i == ((Int32*)obj)->value ? 1 : 0;
}