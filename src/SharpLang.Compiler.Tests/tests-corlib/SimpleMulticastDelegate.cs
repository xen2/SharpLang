using System;

public static class Program
{
    delegate void SimpleDelegate(string str);

    public class Test1
    {
        public void Method(string str)
        {
            Console.WriteLine("Instance");
            Console.WriteLine(str);
        }
    }

    public static void Method(string str)
    {
        Console.WriteLine("Static");
        Console.WriteLine(str);
    }
    
    public static void Main()
    {
        var test1 = new Test1();
        SimpleDelegate action1 = test1.Method;
        action1("Test1");

        action1 += Method;
        action1("Test2");

        action1 -= test1.Method;
        action1("Test3");
    }
}