// Temporary minimal runtime library for testing purpose.
// It should gradually be replaced by a real runtime.

#include <stdio.h>
#include <stdint.h>

// Match runtime String struct layout
typedef struct String
{
	size_t length;
	char* value;
} String;

// void System.Console.WriteLine(string)
void System_Void_System_Console__WriteLine_System_String_(String str)
{
	printf("%.*s\n", (uint32_t)str.length, str.value);
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