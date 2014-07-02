public static class Program
{
    class Exception1 : System.Exception
    {
    }

    class Exception2 : Exception1
    {
    }

    public static void TestException(bool shouldThrow)
    {
        try
        {
            if (shouldThrow)
                throw new Exception2();
        }
        catch (Exception1 e)
        {
            System.Console.WriteLine("Exception caught");
        }
    }

    public static void Main()
    {
        TestException(false);
        TestException(true);
    }
}