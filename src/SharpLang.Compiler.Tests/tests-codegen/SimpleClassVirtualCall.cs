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
        Test1 test1 = new Test1();
        Test1 test2 = new Test2();

        System.Console.WriteLine(test1.A());
        System.Console.WriteLine(test2.A());
    }
}