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

class FieldDescription
{
private:
	uint32_t data1;
	uint32_t data2;
public:
	uint32_t GetRowID()
	{
		return data1 & 0x00FFFFFF;
	}

	uint32_t GetOffset()
	{
		return data2 & 0x7FFFFFF;
	}

	uint32_t GetType()
	{
		return (data2 >> 27);
	}
};

class MethodTable
{
public:
	EEType* base;

	uint8_t isConcreteType;

	// Metadata
	TypeDefinition typeDef;
	EEType* elementType;
	RuntimeType* runtimeType;

	// Field infos
	uint16_t garbageCollectableFieldCount; // First entries in FieldDescriptions will be for the GC: instance fields of referencable types
	uint16_t fieldCount;
	FieldDescription* fieldDescriptions;
	
	// Concrete type info
	uint32_t superTypeCount;
	uint32_t interfacesCount;
	EEType** superTypes;
	EEType** interfaceMap;
	uint8_t initialized;
	uint32_t objectSize;
	uint32_t elementSize;

	// IMT
	void* interfaceMethodTable[19];

	// VTable
	uint32_t virtualTableSize;
	void* virtualTable[0];
};

extern "C" bool isInstInterface(const EEType* eeType, const EEType* expectedInterface);

struct AppDomain;

class Object
{
public:
	Object(EEType* eeType) : eeType(eeType) {}
    
    AppDomain* GetAppDomain() { return NULL; }

	EEType* eeType;
};

class RuntimeType : public Object
{
public:
	Object* m_keepalive;
	void* m_cache;
	void* m_handle;
	
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
		return NewString((const char16_t*)str, length);
	}

	static StringObject* NewString(const char* str)
	{
		return NewString(str, strlen(str));
	}

	static StringObject* NewString(const char16_t* str);

	static StringObject* NewString(const wchar_t* str)
	{
		return NewString((const char16_t*)str);
	}

	wchar_t* GetBuffer() { return (wchar_t*)&firstChar; }
    
    uint32_t GetStringLength() { return length; }
    
    void RefInterpretGetStringValuesDangerousForGC(wchar_t **chars, int *length)
    {
        *length = GetStringLength();
        *chars  = GetBuffer();
    }

	uint32_t length;
	char16_t firstChar;
};

class ArrayBase : public Object
{
public:
    inline size_t GetNumComponents() { return length; }
    inline size_t GetComponentSize() { return eeType->elementSize; }
    
    inline uint8_t* GetDataPtr() { return (uint8_t*)(this + 1); }

	size_t length;
};

template <class T>
class Array : public ArrayBase
{
public:
	T* value;
    
    const T* GetDirectConstPointerToNonObjectElements() const
    {
        return value;
    }
    
    T* GetDirectPointerToNonObjectElements()
    { 
        return value;
    }
};

extern EEType System_Threading_Thread_rtti;

class Thread : public Object
{
public:
	Thread() : Object(&System_Threading_Thread_rtti) {}
};

class StringBufferObject : public Object
{
  private:
    Array<char16_t>* m_ChunkChars;
    StringBufferObject* m_ChunkPrevious;
    uint32_t m_ChunkLength;
    uint32_t m_ChunkOffset;
    int32_t m_MaxCapacity;
    
    // TODO
};

#endif
