using System;

public static class Program
{
    class Test1
    {
        public override bool Equals(object obj)
        {
            return this == obj;
        }
    }

    struct Test2
    {
        public int I;
    }

    public static void Test<T>(T t1, T t2)
    {
        System.Console.WriteLine(t1.Equals(t2));
    }

    public static void Main()
    {
        object obj1 = new Test1();
        object obj2 = new Test1();

        // Case 1: reference type
        Test(obj1, obj1);
        Test(obj1, obj2);

        // Case2: value type that implements the method
        Test(32, 32);
        Test(32, 48);

        // Case3: value type that doesn't implement the method
        Test2 value1 = new Test2 { I = 16 };
        Test2 value2 = new Test2 { I = 32 };
        Test(value1, value1);
        Test(value1, value2);
    }
}