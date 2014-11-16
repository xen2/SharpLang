public static class Program
{
    public class Test
    {
        public string A;
    }

    public class Test2 : Test
    {
        public int B;
    }

    public static void Main()
    {
        var test = new Test2();
        test.A = "Test";
        test.B = 32;

        System.Console.WriteLine(test.A);
        System.Console.WriteLine(test.B);
    }
}