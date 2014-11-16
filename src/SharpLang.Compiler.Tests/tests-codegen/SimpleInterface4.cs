public static class Program
{
    public interface ITest<T>
    {
        T A();
    }

    public abstract class Test1<T2> : ITest<T2>
    {
        public abstract T2 A();
    }

    public class Test2 : Test1<string>
    {
        public override string A()
        {
            return "Test2";
        }
    }

    public class Test3 : ITest<string>
    {
        public string A()
        {
            return "Test3";
        }
    }

    public static void Main()
    {
        ITest<string> test2 = new Test2();
        ITest<string> test3 = new Test3();

        System.Console.WriteLine(test2.A());
        System.Console.WriteLine(test3.A());
    }
}