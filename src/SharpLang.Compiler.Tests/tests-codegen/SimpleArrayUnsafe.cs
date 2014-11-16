public static class Program
{
    public unsafe static void Main()
    {
        var testArray = new byte[] { 32, 15 };

        fixed (byte* testArrayPtr = testArray)
        {
            System.Console.WriteLine(testArrayPtr[0]);
            System.Console.WriteLine(testArrayPtr[1]);

            testArrayPtr[1] = 68;
        }

        System.Console.WriteLine(testArray[1]);
    }
}