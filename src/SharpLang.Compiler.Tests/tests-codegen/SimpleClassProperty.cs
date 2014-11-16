public static class Program
{
    public class Test
    {
        public string A { get; set; }
        public int B { get; set; }
    }

    public static void Main()
    {
        var test = new Test();
        test.A = "Test";
        test.B = 32;

        System.Console.WriteLine(test.A);
        System.Console.WriteLine(test.B);
    }
}