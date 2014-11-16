public static class Program
{
    public class Test1
    {
        public virtual string A()
        {
            return "Test1";
        }
    }

    public interface ITest
    {
    }

    public class Test2 : Test1, ITest
    {
        public override string A()
        {
            return "Test2";
        }
    }

    class Exception1 : System.Exception
    {
    }
    
    public static void Main()
    {
        object test1 = new Test1();
        object test2 = new Test2();
        object test3 = null;

        // Test valid casts
        System.Console.WriteLine((Test1)test1 != null);
        System.Console.WriteLine((Test1)test2 != null);
        System.Console.WriteLine((Test2)test2 != null);
        System.Console.WriteLine((ITest)test2 != null);

        System.Console.WriteLine((Test1)test3 != null);
        System.Console.WriteLine((Test2)test3 != null);
        System.Console.WriteLine((ITest)test3 != null);

        try
        {
            System.Console.WriteLine((Test2)test1 != null);
        }
        catch (System.InvalidCastException e)
        {
            System.Console.WriteLine("InvalidCast");
        }
        
        try
        {
            System.Console.WriteLine((ITest)test1 != null);
        }
        catch (System.InvalidCastException e)
        {
            System.Console.WriteLine("InvalidCast");
        }
    }
}