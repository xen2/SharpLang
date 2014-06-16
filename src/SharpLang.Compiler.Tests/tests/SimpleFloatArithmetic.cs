using System;

public static class Program
{
    static void TestSingle()
    {
        float a = 3.0f;
        Console.WriteLine(a);

        a += 32.0f;
        Console.WriteLine(a);

        a -= 502.0f;
        Console.WriteLine(a);

        a *= 123.0f;
        Console.WriteLine(a);

        a /= 21.0f;
        Console.WriteLine(a);
    }

    static void TestDouble()
    {
        double a = 3.0;
        Console.WriteLine(a);

        a += 32.0;
        Console.WriteLine(a);

        a -= 502.0;
        Console.WriteLine(a);

        a *= 123.0;
        Console.WriteLine(a);

        a /= 21.0;
        Console.WriteLine(a);
    }

    public static void Main()
    {
        TestSingle();
        TestDouble();
    }
}