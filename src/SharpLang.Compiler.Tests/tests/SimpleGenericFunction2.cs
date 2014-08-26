using System;

public class B<T>
{
    public T Value;
}

public class A<T>
{
    public static B<U> Test<U>(U input)
    {
        return new B<U> { Value = input };
    }
}

public static class Program
{
    public static void Main()
    {
        System.Console.WriteLine(A<int>.Test<string>("Test1").Value);
    }
}