public static class Program
{
    public class Test
    {
        public string A;
        public int B;
        public Data D;
    }

    public struct Data
    {
        public int C;
    }

    public static void Main()
    {
        var test = new Test();
        test.A = "Test";
        test.B = 32;

        // Emit Ldflda
        test.D.C++;

        System.Console.WriteLine(test.A);
        System.Console.WriteLine(test.B);
        System.Console.WriteLine(test.D.C);
    }
}