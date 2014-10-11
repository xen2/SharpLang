#ifndef SHARPLANG_RUNTIME_TYPE_H
#define SHARPLANG_RUNTIME_TYPE_H

#include <stdint.h>

struct RuntimeTypeInfo
{
	RuntimeTypeInfo* base;
	uint32_t superTypeCount;
	uint32_t interfacesCount;
	RuntimeTypeInfo** superTypes;
	RuntimeTypeInfo** interfaceMap;
	uint8_t initialized;
	uint32_t objectSize;
	uint32_t elementSize;
	void* interfaceMethodTable[19];
	void* virtualTable[0];
};

struct Object
{
	Object(RuntimeTypeInfo* runtimeTypeInfo) : runtimeTypeInfo(runtimeTypeInfo) {}

	RuntimeTypeInfo* runtimeTypeInfo;
};

struct Exception : Object
{
};

extern RuntimeTypeInfo System_String_rtti;

struct String : Object
{
	String(uint32_t length, const char16_t* value) : Object(&System_String_rtti), length(length), value(value) {}

	uint32_t length;
	const char16_t* value;
};

struct ArrayBase : Object
{
	size_t length;
};

template <class T>
struct Array : ArrayBase
{
	const T* value;
};

#endif
