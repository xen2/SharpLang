// Cover OpCode.Brfalse, OpCode.Brtrue and OpCode.Br
public static class Program
{
    public static void Compare(int a, int b)
    {
        if (a > b)
            System.Console.WriteLine("Test1");

        if (a >= b)
            System.Console.WriteLine("Test2");

        if (a < b)
            System.Console.WriteLine("Test3");

        if (a <= b)
            System.Console.WriteLine("Test4");

        if (a == b)
            System.Console.WriteLine("Test5");

        if (a != b)
            System.Console.WriteLine("Test6");
    }

    public static void Compare(float a, float b)
    {
        if (a > b)
            System.Console.WriteLine("Test1");

        if (a >= b)
            System.Console.WriteLine("Test2");

        if (a < b)
            System.Console.WriteLine("Test3");

        if (a <= b)
            System.Console.WriteLine("Test4");

        if (a == b)
            System.Console.WriteLine("Test5");

        if (a != b)
            System.Console.WriteLine("Test6");
    }

    public static void Main()
    {
        int a = 3;
        int b = 2;

        float c = 3.0f;
        float d = 2.0f;


        Compare(a, a);
        Compare(a, b);
        Compare(c, c);
        Compare(c, d);
    }
}