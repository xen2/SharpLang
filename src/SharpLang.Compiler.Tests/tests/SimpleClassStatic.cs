using System;

public static class Program
{
    public class Test
    {
        public static string TestField;

        public static void TestMethod()
        {
            Console.WriteLine(TestField);
        }
    }

    public static void Main()
    {
        Test.TestField = "Test1";
        Console.WriteLine(Test.TestField);

        Test.TestField = "Test2";
        Test.TestMethod();
    }
}