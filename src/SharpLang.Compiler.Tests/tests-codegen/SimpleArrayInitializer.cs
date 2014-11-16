class EmbedTestAttribute : System.Attribute
{
    public EmbedTestAttribute(System.Type type) { }
}

[EmbedTestAttribute(typeof(byte[]))]
public static class Program
{
    private static byte[] Test = new byte[] {1, 2, 3, 4, 5, 6, 7, 8};

    public static void Main()
    {
        for (int i = 0; i < Test.Length; ++i)
            System.Console.WriteLine(Test[i]);
    }
}