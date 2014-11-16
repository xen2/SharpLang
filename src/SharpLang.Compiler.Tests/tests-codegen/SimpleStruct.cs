public static class Program
{
    public struct Test
    {
        public string A;
        public int B;

        public int C { get; set; }
    }

    public static void Main()
    {
        var test = new Test();
        test.A = "Test";
        test.B = 32;
        test.C = 64;

        System.Console.WriteLine(test.A);
        System.Console.WriteLine(test.B);
        System.Console.WriteLine(test.C);
    }
}