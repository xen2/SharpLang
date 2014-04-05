// Cover OpCode.Brfalse and OpCode.Br
public static class Program
{
    public static void Main()
    {
        bool flag2;
        bool flag = true;

        if (flag)
            flag2 = true;
        else
            flag2 = false;

        if (flag2)
            System.Console.WriteLine("True");
    }
}