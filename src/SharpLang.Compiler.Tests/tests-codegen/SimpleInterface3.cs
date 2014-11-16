public static class Program
{
    public interface ITest<T>
    {
        T A();
    }

    public class Test1 : ITest<string>
    {
        string ITest<string>.A()
        {
            return "Test4";
        }
    }

    public static void Main()
    {
        ITest<string> test1 = new Test1();

        System.Console.WriteLine(test1.A());
    }
}