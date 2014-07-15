// Cover OpCode.Brfalse
public static class Program
{
    class A
    {
    }

    class B : A
    {
    }

    static void Test(A a)
    {
        // This should generate stack merging between A and B
        a = a ?? new B();

        System.Console.WriteLine(a is A);
        System.Console.WriteLine(a is B);
    }

    public static void Main()
    {
        Test(new A());
        Test(null);
    }
}