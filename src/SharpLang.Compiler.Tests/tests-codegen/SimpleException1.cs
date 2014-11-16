public static class Program
{
    class Exception1 : System.Exception
    {
    }

    class Exception2 : Exception1
    {
    }

    class Exception3 : System.Exception
    {
    }

    public static void ThrowException(System.Exception exception)
    {
        throw exception;
    }

    public static void TestException(System.Exception exception)
    {
        try
        {
            System.Console.WriteLine("BeforeInner");
            try
            {
                if (exception != null)
                    ThrowException(exception);
            }
            catch (Exception2 e)
            {
                System.Console.WriteLine("Exception2 caught");
            }
            catch (Exception1 e)
            {
                System.Console.WriteLine("Exception1 caught");
            }
            finally
            {
                System.Console.WriteLine("Finally1");
            }
            System.Console.WriteLine("AfterInner");
        }
        catch (Exception3)
        {
            System.Console.WriteLine("Exception3 caught");
        }
        finally
        {
            System.Console.WriteLine("Finally2");
        }
    }

    public static void Main()
    {
        TestException(null);
        TestException(new Exception1());
        TestException(new Exception2());
        TestException(new Exception3());
    }
}