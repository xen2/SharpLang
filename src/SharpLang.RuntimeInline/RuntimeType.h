#ifndef SHARPLANG_RUNTIME_TYPE_H
#define SHARPLANG_RUNTIME_TYPE_H

#include <stdint.h>

typedef struct RuntimeTypeInfo
{
	RuntimeTypeInfo* base;
	uint32_t superTypeCount;
	uint32_t interfacesCount;
	RuntimeTypeInfo** superTypes;
	RuntimeTypeInfo** interfaceMap;
	void* interfaceMethodTable[19];
	void* virtualTable[0];
} RuntimeTypeInfo;

typedef struct Object
{
	RuntimeTypeInfo* runtimeTypeInfo;
} Object;

#endif
