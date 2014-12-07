using System;
using System.Runtime.InteropServices;

public static class Program
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CallbackType(int param1);

    [DllImport("PInvokeTest.dll", EntryPoint = "CallbackDelegateTest", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr CallbackDelegateTest(CallbackType callback, int param1);

    public unsafe static void Main()
    {
        // Static callback
        CallbackDelegateTest(x => Console.WriteLine(x), 16);

        // Non-static callback
        var i = 32;
        CallbackDelegateTest(x => Console.WriteLine(x + i), 22);
    }
}