#include <stdint.h>

int main()
{
	return 0;
}

// Struct passed by value, with smaller than register fields
// We expect it to end up packed in i32
// Note: it wasn't working with LLVM when calling with aggregate type,
//       so it was changed to use pointer byval instead.
struct Color
{
	uint8_t r, g, b, a;
};

extern "C" __declspec(dllexport) Color _cdecl StructWithSmallFieldCdecl(Color color, int param1)
{
	color.b += param1;
	color.a -= param1;
	return color;
}

extern "C" __declspec(dllexport) Color __stdcall StructWithSmallFieldStdCall(Color color, int param1)
{
	color.b += param1;
	color.a -= param1;
	return color;
}

extern "C" __declspec(dllexport) Color __fastcall StructWithSmallFieldFastCall(Color color, int param1)
{
	color.b += param1;
	color.a -= param1;
	return color;
}

extern "C" __declspec(dllexport) Color __thiscall StructWithSmallFieldThisCall(Color color, int param1)
{
	color.b += param1;
	color.a -= param1;
	return color;
}

// Callback test
typedef void(*CallbackType)(int param1);

// For SimplePInvokeCallbackDelegate.cs
extern "C" __declspec(dllexport) void CallbackDelegateTest(CallbackType callback, int param1)
{
	callback(param1);
}
