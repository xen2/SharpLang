using System;
using System.Runtime.InteropServices;

public static class Program
{
    [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
    public static extern IntPtr memcpy(IntPtr dest, IntPtr src, UIntPtr count);

    public unsafe static void Main()
    {
        string str1 = "abcd";
        string str2 = "efgh";

        fixed (char* c1 = (string)str1)
        fixed (char* c2 = (string)str2)
        {
            memcpy((IntPtr)c1, (IntPtr)c2, (UIntPtr)(sizeof(char) * str1.Length));
        }

        System.Console.WriteLine(str1);
        System.Console.WriteLine(str2);
    }
}