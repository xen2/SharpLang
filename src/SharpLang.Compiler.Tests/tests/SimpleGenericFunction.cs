using System;

public static class Program
{
    public static T Test<T>(T test)
    {
        return test;
    }

    public static void Main()
    {
        System.Console.WriteLine(Test(32));
        System.Console.WriteLine(Test("Test1"));
    }
}