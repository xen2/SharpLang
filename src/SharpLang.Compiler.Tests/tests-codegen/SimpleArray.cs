public static class Program
{
    public static void Main()
    {
        var testArray = new[] { "a", "b" };

        System.Console.WriteLine(testArray.Length);
        System.Console.WriteLine(testArray[0]);
        System.Console.WriteLine(testArray[1]);

        testArray[1] = "c";
        System.Console.WriteLine(testArray[1]);
    }
}