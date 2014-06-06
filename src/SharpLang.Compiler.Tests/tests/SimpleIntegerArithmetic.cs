using System;

public static class Program
{
    public static void Main()
    {
        int a = 3;
        Console.WriteLine(a);

        a += 32;
        Console.WriteLine(a);

        a -= 502;
        Console.WriteLine(a);

        a *= 123;
        Console.WriteLine(a);

        a /= 21;
        Console.WriteLine(a);

        a >>= 5;
        Console.WriteLine(a);

        a <<= 2;
        Console.WriteLine(a);

        uint b = 0xF1E31643;
        b |= 0x8F0F0F0F;
        Console.WriteLine(b);

        b &= 0x88888888;
        Console.WriteLine(b);

        b ^= 0xF0F0F0F0;
        Console.WriteLine(b);
    }
}