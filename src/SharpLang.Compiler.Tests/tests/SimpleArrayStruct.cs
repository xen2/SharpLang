public static class Program
{
    struct Test
    {
        public int A;
    }

    public static void Main()
    {
        var testArray = new Test[]
        {
            new Test { A = 32 },
            new Test { A = 16 },
        };

        System.Console.WriteLine(testArray[1].A);

        var test = testArray[0];
        System.Console.WriteLine(test.A);
    }
}