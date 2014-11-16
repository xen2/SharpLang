// Cover OpCode.Brfalse
public static class Program
{
    public static void Main()
    {
        bool flag = true;

        if (flag)
            System.Console.WriteLine("True");

        flag = false;

        if (flag)
            System.Console.WriteLine("False");
    }
}