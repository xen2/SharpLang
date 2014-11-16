// Cover OpCode.Brfalse, OpCode.Brtrue and OpCode.Br
public static class Program
{
    public static void Compare(int a, int b)
    {
        System.Console.WriteLine(a > b);
        System.Console.WriteLine(a >= b);
        System.Console.WriteLine(a < b);
        System.Console.WriteLine(a <= b);
        System.Console.WriteLine(a == b);
        System.Console.WriteLine(a != b);
    }

    public static void Compare(float a, float b)
    {
        System.Console.WriteLine(a > b);
        System.Console.WriteLine(a >= b);
        System.Console.WriteLine(a < b);
        System.Console.WriteLine(a <= b);
        System.Console.WriteLine(a == b);
        System.Console.WriteLine(a != b);
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