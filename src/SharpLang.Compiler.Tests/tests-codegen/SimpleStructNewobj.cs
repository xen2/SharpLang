public static class Program
{
    public struct Test
    {
        public Test(int a)
        {
            A = a;
        }

        public int A;
    }

    public static Test CreateTest(int a)
    {
        // Force Newobj on value type
        return new Test(a);
    }

    public static void Main()
    {
        var test = CreateTest(32);

        System.Console.WriteLine(test.A);
    }
}