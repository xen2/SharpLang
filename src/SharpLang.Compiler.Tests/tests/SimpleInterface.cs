public static class Program
{
    public interface ITest
    {
        string A();
    }

    public class Test1 : ITest
    {
        public virtual string A()
        {
            return "Test1";
        }
    }

    public class Test2 : Test1
    {
    }

    public class Test3 : Test1
    {
        public override string A()
        {
            return "Test3";
        }
    }

    public class Test4 : Test1, ITest
    {
        string ITest.A()
        {
            return "Test4";
        }
    }

    public static void Main()
    {
        ITest test1 = new Test1();
        ITest test2 = new Test2();
        ITest test3 = new Test3();
        ITest test4 = new Test4();

        System.Console.WriteLine(test1.A());
        System.Console.WriteLine(test2.A());
        System.Console.WriteLine(test3.A());
        System.Console.WriteLine(test4.A());
    }
}