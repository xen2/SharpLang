using System;

public static class Program
{
    public class Test
    {
        public static string TestField;

        public static int C;

        public static Data Data;

        public static void TestMethod()
        {
            Console.WriteLine(TestField);
        }
    }

    public struct Data
    {
        public int Value;
    }

    public static void Main()
    {
        Test.TestField = "Test1";
        Console.WriteLine(Test.TestField);

        Test.TestField = "Test2";
        Test.TestMethod();

        Test.C++;
        Console.WriteLine(Test.C);

        Test.Data.Value++;
        Console.WriteLine(Test.Data.Value);
    }
}