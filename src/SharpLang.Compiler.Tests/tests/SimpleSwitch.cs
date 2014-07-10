// Cover OpCode.Brfalse, OpCode.Brtrue and OpCode.Br
public static class Program
{
    public static void Test(int a)
    {
        switch (a)
        {
            case 0:
                System.Console.WriteLine(a);
                break;
            case 1:
                System.Console.WriteLine(a);
                return;
            default:
                System.Console.WriteLine("Default");
                break;
        }

        System.Console.WriteLine("After");
    }

    public static void Main()
    {
        Test(0);
        Test(1);
        Test(2);
    }
}