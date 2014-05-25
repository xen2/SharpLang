public static class Program
{
    public class Test<T>
    {
        public T A;
    }

    public static void Main()
    {
        var test1 = new Test<int> { A = 32 };
        var test2 = new Test<string> { A = "Test" };

        System.Console.WriteLine(test1.A);
        System.Console.WriteLine(test1.A);
    }
}