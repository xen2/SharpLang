public static class Program
{
    public class A
    {
        
    }

    public class Base<T>
    {
        
    }

    public class A<T> : Base<T[]>
    {
    }

    public static void Main()
    {
        System.Console.WriteLine("Hello, World!");
        System.Console.WriteLine(32);

        var a1 = new A();
        var a2 = new A[1];
        var a3 = new A<int>();

        System.Console.WriteLine(a1.GetType().Name);
        System.Console.WriteLine(a2.GetType().Name);
        System.Console.WriteLine(a3.GetType().Name);
        System.Console.WriteLine(a3.GetType().FullName);

        System.Console.WriteLine(a1.GetType().BaseType.Name);
        System.Console.WriteLine(a2.GetType().BaseType.Name);
        System.Console.WriteLine(a3.GetType().BaseType.Name);
        System.Console.WriteLine(a3.GetType().BaseType.FullName);
    }
}