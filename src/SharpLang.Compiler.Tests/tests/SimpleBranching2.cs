// Cover OpCode.Brfalse, OpCode.Brtrue and OpCode.Br
public static class Program
{
    public static void Main()
    {
        bool flag2;
        bool flag = true;

        if (flag)
        {
            System.Console.WriteLine("Flag");
            flag2 = true;
        }
        else
        {
            System.Console.WriteLine("NotFlag");
            flag2 = false;
        }

        if (!flag2)
            System.Console.WriteLine("NotFlag2");
    }
}