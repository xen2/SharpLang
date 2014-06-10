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
        SimpleDelegate action1 = new Test1().Method;
        action1("Test1");

        // TODO: Delegate to static members are not supported yet
        SimpleDelegate action2 = Method;
        action2("Test2");
    }
}