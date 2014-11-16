public static class Program
{
    public class Test<T>
    {
        public T A;
        public T B { get; set; }
    }

    public static void Main()
    {
        var test1 = new Test<int> { A = 32, B = 64 };
        var test2 = new Test<string> { A = "Test1", B = "Test2" };

        System.Console.WriteLine(test1.A);
        System.Console.WriteLine(test1.B);
        System.Console.WriteLine(test2.A);
        System.Console.WriteLine(test2.B);
    }
}