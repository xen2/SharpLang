public static class Program
{
    public static void TestException(bool shouldThrow)
    {
        try
        {
            if (shouldThrow)
                throw new System.Exception("Test");
        }
        catch (System.Exception e)
        {
            System.Console.WriteLine(e.Message);
        }
    }

    public static void Main()
    {
        TestException(false);
        TestException(true);
    }
}