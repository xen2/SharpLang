public static class Program
{
    public class Test1
    {
        static Test1()
        {
            System.Console.WriteLine("Test1: Static ctor");
        }

        public static void Method1()
        {
            System.Console.WriteLine("Test1: Static method");
        }
    }

    public class Test2
    {
        static Test2()
        {
            System.Console.WriteLine("Test2: Static ctor");
        }
    }
    
    public static void Main()
    {
        Test1.Method1();

        var test2 = new Test2();
    }
}