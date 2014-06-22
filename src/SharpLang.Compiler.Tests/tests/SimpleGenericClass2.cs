public static class Program
{
    public class Test1<T>
    {
        public T Value { get; set; }

        public T Test()
        {
            return Value;
        }
    }

    public class Test2<T>
    {
        public Test1<T> Item;

        public T Test()
        {
            return Item.Test();
        }
    }

    public static void Main()
    {
        var test2 = new Test2<int> { Item = new Test1<int> { Value = 32 } };

        System.Console.WriteLine(test2.Test());
    }
}