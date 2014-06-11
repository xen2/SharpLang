// Interface with 20+ functions, to force at least 1 IMT collision (current default size is 19)
public static class Program
{
    public interface ITest
    {
        string A01();
        string A02();
        string A03();
        string A04();
        string A05();
        string A06();
        string A07();
        string A08();
        string A09();
        string A10();
        string A11();
        string A12();
        string A13();
        string A14();
        string A15();
        string A16();
        string A17();
        string A18();
        string A19();
        string A20();
    }

    public class Test1 : ITest
    {
        public virtual string A01() { return "Test01"; }
        public virtual string A02() { return "Test02"; }
        public virtual string A03() { return "Test03"; }
        public virtual string A04() { return "Test04"; }
        public virtual string A05() { return "Test05"; }
        public virtual string A06() { return "Test06"; }
        public virtual string A07() { return "Test07"; }
        public virtual string A08() { return "Test08"; }
        public virtual string A09() { return "Test09"; }
        public virtual string A10() { return "Test10"; }
        public virtual string A11() { return "Test11"; }
        public virtual string A12() { return "Test12"; }
        public virtual string A13() { return "Test13"; }
        public virtual string A14() { return "Test14"; }
        public virtual string A15() { return "Test15"; }
        public virtual string A16() { return "Test16"; }
        public virtual string A17() { return "Test17"; }
        public virtual string A18() { return "Test18"; }
        public virtual string A19() { return "Test19"; }
        public virtual string A20() { return "Test20"; }
    }

    public static void Main()
    {
        ITest test1 = new Test1();

        System.Console.WriteLine(test1.A01());
        System.Console.WriteLine(test1.A02());
        System.Console.WriteLine(test1.A03());
        System.Console.WriteLine(test1.A04());
        System.Console.WriteLine(test1.A05());
        System.Console.WriteLine(test1.A06());
        System.Console.WriteLine(test1.A07());
        System.Console.WriteLine(test1.A08());
        System.Console.WriteLine(test1.A09());
        System.Console.WriteLine(test1.A10());
        System.Console.WriteLine(test1.A11());
        System.Console.WriteLine(test1.A12());
        System.Console.WriteLine(test1.A13());
        System.Console.WriteLine(test1.A14());
        System.Console.WriteLine(test1.A15());
        System.Console.WriteLine(test1.A16());
        System.Console.WriteLine(test1.A17());
        System.Console.WriteLine(test1.A18());
        System.Console.WriteLine(test1.A19());
        System.Console.WriteLine(test1.A20());
    }
}