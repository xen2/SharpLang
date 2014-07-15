using System;

public static class Program
{
    enum Operation
    {
        Add,
        Sub,
        Mul,
    }

    static void TestOverflow(Operation op, int a, int b)
    {
        try
        {
            checked
            {
                switch (op)
                {
                    case Operation.Add:
                        System.Console.WriteLine(a + b);
                        break;
                    case Operation.Sub:
                        System.Console.WriteLine(a - b);
                        break;
                    case Operation.Mul:
                        System.Console.WriteLine(a * b);
                        break;
                }
            }
        }
        catch (System.OverflowException)
        {
            System.Console.WriteLine("Overflow");
        }
    }

    static void TestOverflow(Operation op, uint a, uint b)
    {
        try
        {
            checked
            {
                switch (op)
                {
                    case Operation.Add:
                        System.Console.WriteLine(a + b);
                        break;
                    case Operation.Sub:
                        System.Console.WriteLine(a - b);
                        break;
                    case Operation.Mul:
                        System.Console.WriteLine(a * b);
                        break;
                }
            }
        }
        catch (System.OverflowException)
        {
            System.Console.WriteLine("Overflow");
        }
    }

    public static void Main()
    {
        TestOverflow(Operation.Add, 32, 16);
        TestOverflow(Operation.Add, 0x70000000, 0x0FFFFFFF);
        TestOverflow(Operation.Add, 0x70000001, 0x0FFFFFFF);

        TestOverflow(Operation.Sub, 32U, 16U);
        TestOverflow(Operation.Sub, 16U, 32U);

        TestOverflow(Operation.Mul, 0x4321, 0x1234);
        TestOverflow(Operation.Mul, 0x10001, 0x10001);
    }
}