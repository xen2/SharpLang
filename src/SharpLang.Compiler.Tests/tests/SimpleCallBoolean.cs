public static class Program
{
    public static void Method1(bool flag)
    {
        if (flag)
            System.Console.WriteLine("True");
        else
            System.Console.WriteLine("False");
    }

    public static void Main()
    {
        Method1(true);
        Method1(false);
    }
}