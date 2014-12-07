using System;
using System.Runtime.InteropServices;

public static class Program
{
    [DllImport("PInvokeTest.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern Color StructWithSmallFieldCdecl(Color color, int param1);

    [DllImport("PInvokeTest.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern Color StructWithSmallFieldStdCall(Color color, int param1);

    [DllImport("PInvokeTest.dll", CallingConvention = CallingConvention.FastCall)]
    public static extern Color StructWithSmallFieldFastCall(Color color, int param1);

    [DllImport("PInvokeTest.dll", CallingConvention = CallingConvention.FastCall)]
    public static extern Color StructWithSmallFieldThisCall(Color color, int param1);

    public struct Color
    {
        public byte R, G, B, A;
    }

    public unsafe static void Main()
    {
        var color = new Color { R = 1, G = 2, B = 3, A = 4 };

        Color result;

        // cdecl
        result = StructWithSmallFieldCdecl(color, 3);
        Console.WriteLine(result.R);
        Console.WriteLine(result.G);
        Console.WriteLine(result.B);
        Console.WriteLine(result.A);

        // stdcall
        //result = StructWithSmallFieldStdCall(color, 3);
        //Console.WriteLine(result.R);
        //Console.WriteLine(result.G);
        //Console.WriteLine(result.B);
        //Console.WriteLine(result.A);

        // fastcall
        //result = StructWithSmallFieldFastCall(color, 3);
        //Console.WriteLine(result.R);
        //Console.WriteLine(result.G);
        //Console.WriteLine(result.B);
        //Console.WriteLine(result.A);

        // thiscall
        //result = StructWithSmallFieldThisCall(color, 3);
        //Console.WriteLine(result.R);
        //Console.WriteLine(result.G);
        //Console.WriteLine(result.B);
        //Console.WriteLine(result.A);
    }
}