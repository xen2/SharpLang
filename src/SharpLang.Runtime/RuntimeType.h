#ifndef SHARPLANG_RUNTIME_TYPE_H
#define SHARPLANG_RUNTIME_TYPE_H

#include <stdint.h>

struct Object;
struct RuntimeType;

struct TypeDefinition
{
	Object* sharpLangModule;
	uint32_t token;
};

struct EEType
{
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

struct Object
{
	Object(EEType* eeType) : eeType(eeType) {}

	EEType* eeType;
};

struct RuntimeType : Object
{
	void* implObsolete; // Type._impl should be removed
	EEType* runtimeEEType;
};

struct Exception : Object
{
};

extern EEType System_String_rtti;

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
	T* value;
};

#endif
