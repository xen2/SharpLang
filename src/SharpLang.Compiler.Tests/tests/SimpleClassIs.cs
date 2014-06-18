public static class Program
{
    public class Test1
    {
        public virtual string A()
        {
            return "Test1";
        }
    }

    public class Test2 : Test1
    {
        public override string A()
        {
            return "Test2";
        }
    }

    public static void Main()
    {
        object test1 = new Test1();
        object test2 = new Test2();

        System.Console.WriteLine(test1 is Test1 ? "True" : "False");
        System.Console.WriteLine(test1 is Test2 ? "True" : "False");
        System.Console.WriteLine(test2 is Test1 ? "True" : "False");
        System.Console.WriteLine(test2 is Test2 ? "True" : "False");

        System.Console.WriteLine(test1 as Test1 != null ? "True" : "False");
        System.Console.WriteLine(test1 as Test2 != null ? "True" : "False");
        System.Console.WriteLine(test2 as Test1 != null ? "True" : "False");
        System.Console.WriteLine(test2 as Test2 != null ? "True" : "False");
    }
}