#ifndef SHARPLANG_RUNTIME_TYPE_H
#define SHARPLANG_RUNTIME_TYPE_H

#include <stdint.h>
#include <string.h>

class Object;
class RuntimeType;

class TypeDefinition
{
public:
	Object* sharpLangModule;
	uint32_t token;
};

class MethodTable;
typedef MethodTable EEType;

class MethodTable
{
public:
	EEType* base;

	uint8_t isConcreteType;

	TypeDefinition typeDef;
	EEType* elementType;
	RuntimeType* runtimeType;
	
	uint32_t superTypeCount;
	uint32_t interfacesCount;
	EEType** superTypes;
	EEType** interfaceMap;
	uint8_t initialized;
	uint32_t objectSize;
	uint32_t elementSize;
	void* interfaceMethodTable[19];
	uint32_t virtualTableSize;
	void* virtualTable[0];
};

class Object
{
public:
	Object(EEType* eeType) : eeType(eeType) {}

	EEType* eeType;
};

class RuntimeType : public Object
{
	void* implObsolete; // Type._impl should be removed
public:
	EEType* runtimeEEType;
};

class Exception : public Object
{
};

extern EEType System_String_rtti;

class StringObject : public Object
{
public:
	StringObject(uint32_t length) : Object(&System_String_rtti), length(length)
	{
		(&firstChar)[length] = 0;
	}

	StringObject(uint32_t length, const char16_t* str) : Object(&System_String_rtti), length(length)
	{
		memcpy(&firstChar, str, sizeof(char16_t) * length);
		(&firstChar)[length] = 0;
	}

	StringObject(uint32_t length, const char* str);

	static StringObject* NewString(uint32_t length);
	static StringObject* NewString(const char16_t* str, uint32_t length);
	static StringObject* NewString(const char* str, uint32_t length);
	static StringObject* NewString(const wchar_t* str, uint32_t length)
	{
		NewString((const char16_t*)str, length);
	}

	static StringObject* NewString(const char* str)
	{
		return NewString(str, strlen(str));
	}

	static StringObject* NewString(const char16_t* str);

	static StringObject* NewString(const wchar_t* str)
	{
		NewString((const char16_t*)str);
	}


	uint32_t length;
	char16_t firstChar;
};

class ArrayBase : public Object
{
public:
	size_t length;
};

template <class T>
class Array : public ArrayBase
{
public:
	T* value;
};

#endif
