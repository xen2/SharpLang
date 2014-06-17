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
            return "Test1";
        }
    }

    public static void Main()
    {
        ITest<string> test1 = new Test2();

        System.Console.WriteLine(test1.A());
    }
}