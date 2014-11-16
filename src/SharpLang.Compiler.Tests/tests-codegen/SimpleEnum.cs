public static class Program
{
    public enum Enum1 : ushort
    {
        V1 = 0,
        V2 = 1,
        V3 = 8,
    }

    public static void Method1(Enum1 e)
    {
        System.Console.WriteLine((int)e);
    }

    public static void Main()
    {
        Enum1 e = Enum1.V1;
        System.Console.WriteLine(e == Enum1.V1);
        System.Console.WriteLine(e == Enum1.V2);
        if (e == Enum1.V2)
        {
            System.Console.WriteLine(true);
        }
        Method1(e);
    }
}