[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
class EmbedTestAttribute : System.Attribute
{
    public EmbedTestAttribute(System.Type type) { }
}

[EmbedTestAttribute(typeof(string[]))]
public static class Program
{
    public static void Main()
    {
        var testArray = new[] { "a", "b" };

        try
        {
            var test = (System.Collections.Generic.ICollection<string>)testArray;
            test.Clear();
        }
        catch (System.NotSupportedException)
        {
            System.Console.WriteLine("Exception");
        }
    }
}